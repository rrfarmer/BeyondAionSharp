using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.Autogroup;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Templates.Rewards;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Rnd = Aion.GameServer.Commons.Utils.Rnd;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>
/// Java parity: instance/pvparenas/PvPArenaInstance (xTz, Estrayl) : GeneralInstanceHandler. Abstract base for
/// Chaos/Discipline/Harmony TrainingGrounds + ArenaOfGlory. Future→ScheduledTask; TemporaryPlayerTeam&lt;?&gt;→non-generic
/// TemporaryPlayerTeam base; DamageInfo&lt;?&gt; collection branched on shouldMergeGroupDamage (creature vs team damages);
/// SkillEngine/PlayerReviveService/TeleportService/ItemService/AbyssPointsService/GloryPointsService fully-qualified;
/// record BaseRewards. 1:1.
/// </summary>
public abstract class PvPArenaInstance : GeneralInstanceHandler
{
    private const int START_DELAY = 120000;
    private const int ROUND_DURATION = 180000;
    protected const float RANK_REWARD_RATE = 0.7f;
    protected const float SCORE_REWARD_RATE = 0.3f;

    protected int pointsPerKill = 800;
    protected int pointsPerDeath = -150;

    protected PvPArenaScore instanceScore;
    private ScheduledTask instanceTask;
    private bool isInterrupted;

    public PvPArenaInstance(WorldMapInstance instance) : base(instance)
    {
    }

    protected virtual PvPArenaScore CreateNewArenaScore()
    {
        return new PvPArenaScore(instance);
    }

    protected virtual void SetScoreCaps()
    {
        instanceScore.SetLowerScoreCap(7000);
        instanceScore.SetUpperScoreCap(50000);
        instanceScore.SetMaxScoreGap(43000);
    }

    public override void OnInstanceCreate()
    {
        instanceScore = CreateNewArenaScore();
        SetScoreCaps();
        instanceScore.SetInstanceProgressionType(InstanceProgressionType.PREPARING);
        instanceScore.SetInstanceStartTime();
        SpawnRings();
        instanceTask = ThreadPoolManager.GetInstance().Schedule(StartInstance, (long)START_DELAY);
    }

    private void StartInstance()
    {
        if (instanceScore.IsRewarded())
            return;

        if (!CanStart())
        {
            isInterrupted = true;
            EndInstance();
            return;
        }

        instance.ForEachDoor(door => door.SetOpen(true));
        instanceScore.SetInstanceProgressionType(InstanceProgressionType.START_PROGRESS);
        BroadcastUpdate(InstanceScoreType.UPDATE_INSTANCE_PROGRESS);

        instanceTask = ThreadPoolManager.GetInstance().Schedule(StartNextRound, (long)ROUND_DURATION);
    }

    private void StartNextRound()
    {
        if (instanceScore.IsRewarded())
            return;
        if (IsFinalRound())
        {
            EndInstance();
            return;
        }

        instanceScore.IncrementRound();
        instanceScore.SetRndZone();
        BroadcastUpdate(InstanceScoreType.UPDATE_INSTANCE_PROGRESS);
        ChangeZone();

        if (IsFinalRound())
            SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_START_SCOREMOD());

        instanceTask = ThreadPoolManager.GetInstance().Schedule(StartNextRound, (long)ROUND_DURATION);
    }

    private void EndInstance()
    {
        instance.ForEachNpc(npc => npc.GetController().Delete());
        instanceScore.SetInstanceProgressionType(InstanceProgressionType.END_PROGRESS);
        instanceTask = ThreadPoolManager.GetInstance().Schedule(() => instance.ForEachPlayer(LeaveInstance), 60000L);

        CalculateRewards();
        Reward();
        BroadcastResults();
    }

    public override bool OnDie(Player victim, Creature lastAttacker)
    {
        PvPArenaPlayerReward victimReward = GetStatReward(victim);
        PvPArenaPlayerReward winnerReward = null;
        if (lastAttacker != victim && lastAttacker is Player winner)
        {
            winnerReward = GetStatReward(winner);
            winnerReward.AddPvPKill();
            QuestEngine.QuestEngine.GetInstance().OnKillInWorld(new QuestEnv(victim, winner, 0), winner.GetWorldId()); // Notify Kill-Quests
        }
        CalculateAndUpdatePoints(victim, JavaMath.Round(pointsPerKill * GetRunnerUpScoreMod(victimReward, winnerReward)));
        UpdatePoints(victimReward, victim, lastAttacker, pointsPerDeath, false); // Update victim points after rewarding the winner
        ApplyMoraleBoost(victim);
        return true;
    }

    public override void OnDie(Npc npc)
    {
        int npcId = npc.GetNpcId();
        if (npcId == 701187 || npcId == 701188)
            ThreadPoolManager.GetInstance().Schedule(SpawnRndRelics, 30000L);

        int points = GetPoints(npcId);
        if (points == 0)
            return;

        CalculateAndUpdatePoints(npc, points);
    }

    private void CalculateAndUpdatePoints(Creature victim, int points)
    {
        if (!instanceScore.IsStartProgress())
            return;

        DamageList damageList = victim.GetAggroList().GetFinalDamageList();
        int totalDamage = damageList.GetTotalDamage();
        if (ShouldMergeGroupDamage())
        {
            foreach (DamageInfo<AionObject> damageInfo in damageList.ToTeamDamages().GetCreatureOrTeamDamages())
                DistributePoints(damageInfo.GetAttacker(), damageInfo.GetDamage(), points, totalDamage, victim);
        }
        else
        {
            foreach (DamageInfo<Creature> damageInfo in damageList.GetCreatureDamages())
                DistributePoints(damageInfo.GetAttacker(), damageInfo.GetDamage(), points, totalDamage, victim);
        }
    }

    private void DistributePoints(AionObject attacker, int damage, int points, int totalDamage, Creature victim)
    {
        int rewardPoints = points * damage / totalDamage;
        if (attacker is Player player)
        {
            UpdatePoints(GetStatReward(player), player, victim, rewardPoints);
        }
        else if (attacker is TemporaryPlayerTeam team)
        {
            Player leader = team.GetLeaderObject();
            UpdatePoints(GetStatReward(leader), leader, victim, rewardPoints);
            team.GetOnlineMembers().Where(p => !p.Equals(leader)).ToList().ForEach(p => SendSystemMsg(p, victim, rewardPoints));
        }
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        PvPArenaPlayerReward arenaReward = GetStatReward(player);
        if (!instanceScore.IsStartProgress() || arenaReward == null)
            return;

        int rewardPoints = GetPoints(npc.GetNpcId());
        int skill = instanceScore.GetNpcBonusSkill(npc.GetNpcId());
        if (skill != 0)
            UseSkill(npc, player, skill >> 8, skill & 0xFF);

        UpdatePoints(arenaReward, player, npc, rewardPoints);
    }

    protected virtual void UpdatePoints(PvPArenaPlayerReward receiver, Player player, VisibleObject victim, int rewardPoints)
    {
        UpdatePoints(receiver, player, victim, rewardPoints, true);
    }

    protected virtual void UpdatePoints(PvPArenaPlayerReward receiver, Player player, VisibleObject victim, int rewardPoints, bool broadcastPackets)
    {
        if (receiver == null)
            return;

        receiver.AddPoints(rewardPoints, instanceScore);

        if (broadcastPackets)
        {
            BroadcastUpdate(InstanceScoreType.UPDATE_INSTANCE_BUFFS_AND_SCORE);
            BroadcastUpdate(player, InstanceScoreType.UPDATE_PLAYER_BUFF_STATUS);
        }
        SendSystemMsg(player, victim, rewardPoints);

        if (instanceScore.ReachedScoreGap())
            EndInstance();
    }

    protected virtual bool IsFinalRound()
    {
        return instanceScore.GetRound() == 3;
    }

    protected virtual void SendSystemMsg(Player player, VisibleObject creature, int rewardPoints)
    {
        if (rewardPoints <= 0) // Checked with retail, only net gain is sent as msg
            return;
        if (creature is Player)
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_GET_SCORE_FOR_ENEMY(rewardPoints));
        else
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_GET_SCORE(creature.GetObjectTemplate().GetL10n(), rewardPoints));
    }

    private int CalcAndFloor(int number, float rate)
    {
        return (int)(number * rate);
    }

    private int CalcAndFloor(int number, float rate, float configRate)
    {
        return (int)(number * rate * configRate);
    }

    protected virtual ICollection<PvPArenaPlayerReward> GetArenaRewards()
    {
        return instanceScore.GetPlayerRewards();
    }

    private int GetPoints(int npcId)
    {
        switch (npcId)
        {
            case 701187: // Blessed Relics
            case 701188: // Cursed Relics
                return 1750;
            case 218690: // Pale Carmina
            case 218703: // Pale Carmina
            case 218716: // Pale Carmina
            case 218691: // Corrupt Casus
            case 218704: // Corrupt Casus
            case 218717: // Corrupt Casus
                return 1500;
            case 218682: // MuMu Rake Gatherer
            case 218695: // MuMu Rake Gatherer
            case 218708: // MuMu Rake Gatherer
                return 1250;
            case 701181: // Cursed Relics
            case 701195: // Cursed Relics
            case 701209: // Cursed Relics
            case 218689: // Casus Manor Noble
            case 218702: // Casus Manor Noble
            case 218715: // Casus Manor Noble
                return 750;
            case 218701: // Casus Manor Butler
            case 218688: // Casus Manor Butler
            case 218714: // Casus Manor Butler
                return 650;
            case 701172: // Plaza Flame Thrower
            case 701171: // Plaza Flame Thrower
            case 701170: // Plaza Flame Thrower
            case 701169: // Plaza Flame Thrower
            case 218806: // Casus Manor Chief Maid
            case 218807: // Casus Manor Chief Maid
            case 218808: // Casus Manor Chief Maid
            case 701317: // Blessed Relics
            case 701318: // Blessed Relics
            case 701319: // Blessed Relics
                return 500;
            case 207102: // Recovery Relics
            case 207116: // 1st Floor Stunner
            case 219277: // Roaming Volcanic Petrahulk Lv 50
            case 219278: // Roaming Volcanic Petrahulk Lv 55
            case 219279: // Roaming Volcanic Petrahulk Lv 60
            case 219481: // Volcanic Heart Crystal Lv 50
            case 219485: // Volcanic Heart Crystal Lv 55
            case 219486: // Volcanic Heart Crystal Lv 60
            case 219648: // Roaming Volcanic Petrahulk Lv 65
            case 219652: // Volcanic Heart Crystal Lv 65
                return 400;
            case 218685: // Casus Manor Guard
            case 218698: // Casus Manor Guard
            case 218711: // Casus Manor Guard
            case 218687: // Casus Manor Maid
            case 218700: // Casus Manor Maid
            case 218713: // Casus Manor Maid
            case 218686: // Casus Manor Maidservant
            case 218699: // Casus Manor Maidservant
            case 218712: // Casus Manor Maidservant
                return 250;
            case 207099: // 2nd Floor Bomb
                return 200;
            case 218683: // Black Claw Scratcher
            case 218696: // Black Claw Scratcher
            case 218709: // Black Claw Scratcher
            case 218684: // Mutated Drakan Fighter
            case 218697: // Mutated Drakan Fighter
            case 218710: // Mutated Drakan Fighter
            case 218693: // Red Sand Tog
            case 218706: // Red Sand Tog
            case 218719: // Red Sand Tog
            case 219280: // Heated Negotiator Grangvolkan Lv 50
            case 219281: // Heated Negotiator Grangvolkan Lv 55
            case 219282: // Heated Negotiator Grangvolkan Lv 60
            case 219649: // Heated Negotiator Grangvolkan Lv 65
            case 233058: // Red Sand Tog
            case 701215: // Blesed Relic
            case 701220: // Blesed Relic
            case 701225: // Blesed Relic
            case 701216: // Blesed Relic
            case 701221: // Blesed Relic
            case 701226: // Blesed Relic
                return 100;
            case 219283: // Lurking Fangwing Lv 50
            case 219284: // Lurking Fangwing Lv 55
            case 219285: // Lurking Fangwing Lv 60
            case 219650: // Lurking Fangwing Lv 65
            case 219328: // Plaza Wall
                return 50;
            default:
                return 0;
        }
    }

    private void ChangeZone()
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            foreach (Player player in instance.GetPlayersInside())
            {
                if (player.IsDead())
                {
                    Aion.GameServer.Services.Players.PlayerReviveService.Revive(player, 100, 100, false, 0);
                    player.GetGameStats().UpdateStatsAndSpeedVisually();
                }
                instanceScore.PortToPosition(player);
                BroadcastUpdate(player, InstanceScoreType.UPDATE_PLAYER_BUFF_STATUS);
            }
        }, 1000L);
    }

    public override void OnEnterInstance(Player player)
    {
        if (instanceScore.RegPlayerReward(player))
        {
            instanceScore.SetRndPosition(player.GetObjectId());
        }
        else
        {
            instanceScore.PortToPosition(player);
            GetPlayerSpecificReward(player).UpdateBonusTime();
        }
        SendEntryPacket(player);
    }

    public override void OnPlayerLogout(Player player)
    {
        GetPlayerSpecificReward(player).UpdateLogoutTime();
    }

    private void ClearDebuffs(Player player)
    {
        foreach (Effect ef in player.GetEffectController().GetAbnormalEffects())
        {
            switch (ef.GetSkillTemplate().GetDispelCategory())
            {
                case DispelCategoryType.DEBUFF:
                case DispelCategoryType.DEBUFF_MENTAL:
                case DispelCategoryType.DEBUFF_PHYSICAL:
                case DispelCategoryType.ALL:
                    ef.EndEffect();
                    break;
            }
        }
    }

    protected virtual void UseSkill(Npc npc, Player player, int skillId, int level)
    {
        Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc, skillId, level, player).UseNoAnimationSkill();
    }

    public override bool OnReviveEvent(Player player)
    {
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME());
        Aion.GameServer.Services.Players.PlayerReviveService.Revive(player, 100, 100, false, 0);
        player.GetGameStats().UpdateStatsAndSpeedVisually();
        instanceScore.PortToPosition(player);
        return true;
    }

    public override void LeaveInstance(Player player)
    {
        if (player.GetWorldMapInstance().Equals(instance))
            Aion.GameServer.Services.Teleport.TeleportService.MoveToInstanceExit(player, mapId, player.GetRace());
    }

    public override void OnLeaveInstance(Player player)
    {
        base.OnLeaveInstance(player);
        ClearDebuffs(player);
        PvPArenaPlayerReward playerReward = GetPlayerSpecificReward(player);
        if (playerReward != null)
        {
            EndMoraleBoost(player);
            instanceScore.ClearPosition(playerReward.GetPosition(), false);
            instanceScore.RemovePlayerReward(playerReward);
            BroadcastUpdate(InstanceScoreType.UPDATE_INSTANCE_BUFFS_AND_SCORE);
        }
    }

    public override void OnInstanceDestroy()
    {
        instanceScore.Clear();
        if (instanceTask != null && !instanceTask.IsCancelled)
            instanceTask.Cancel(true);
    }

    private void ApplyMoraleBoost(Player player)
    {
        PvPArenaPlayerReward reward = GetPlayerSpecificReward(player);
        if (reward == null)
            return;

        reward.ApplyBoostMoraleEffect(player, GetBoostMoraleEffectDuration(instanceScore.GetRank(reward)));
        BroadcastUpdate(player, InstanceScoreType.UPDATE_PLAYER_BUFF_STATUS);
    }

    private void EndMoraleBoost(Player player)
    {
        PvPArenaPlayerReward reward = GetPlayerSpecificReward(player);
        if (reward != null && reward.HasBoostMorale())
        {
            reward.EndBoostMoraleEffect(player);
        }
        BroadcastUpdate(player, InstanceScoreType.UPDATE_PLAYER_BUFF_STATUS);
    }

    private void SpawnRndRelics()
    {
        if (instanceScore != null && !instanceScore.IsRewarded())
            Spawn(Rnd.Get(1, 2) == 1 ? 701187 : 701188, 1841.951f, 1733.968f, 300.242f, (byte)0);
    }

    public override InstanceScore GetInstanceScore()
    {
        return instanceScore;
    }

    /// <summary>
    /// Overridden in HarmonyTrainingGroundsInstance to differentiate between player and group specific reward objects
    /// </summary>
    protected virtual PvPArenaPlayerReward GetStatReward(Player player)
    {
        return GetPlayerSpecificReward(player);
    }

    protected virtual PvPArenaPlayerReward GetPlayerSpecificReward(Player player)
    {
        return instanceScore.GetPlayerReward(player.GetObjectId());
    }

    protected virtual bool CanStart()
    {
        return instance.GetPlayersInside().Count > 1;
    }

    protected virtual void SpawnRings()
    {
    }

    protected virtual float GetRunnerUpScoreMod(PvPArenaPlayerReward victim, PvPArenaPlayerReward winner)
    {
        if (winner == null || !IsFinalRound())
            return 1f;

        int victimRank = instanceScore.GetRank(victim);
        return victimRank < instanceScore.GetRank(winner) ? GetRunnerUpScoreMod(victimRank) : 1f;
    }

    protected virtual int GetBoostMoraleEffectDuration(int rank)
    {
        return 15000; // Retail Default
    }

    protected virtual float GetRunnerUpScoreMod(int victimRank)
    {
        return 3f;
    }

    protected virtual bool ShouldMergeGroupDamage()
    {
        return false;
    }

    protected virtual BaseRewards GetBaseRewardsPerPlayer(int difficultyId)
    {
        return new BaseRewards(0, 0, 0, 0);
    }

    protected virtual BaseRewards GetBaseRewards(int difficultyId)
    {
        return new BaseRewards(0, 0, 0, 0);
    }

    protected virtual float GetRewardRate(int rank, int difficultyId)
    {
        return 0f;
    }

    protected virtual void SetRewardItems(PvPArenaPlayerReward reward, int rank, int difficultyId)
    {
    }

    protected virtual float GetConfigRate(Player player)
    {
        return 1f;
    }

    protected virtual void CalculateRewards()
    {
        int difficultyId = instanceScore.GetDifficultyId();
        BaseRewards baseValues = GetBaseRewardsPerPlayer(difficultyId);

        int playerCount = instanceScore.GetPlayerRewards().Count;
        int totalAp = baseValues.Ap * playerCount;
        int totalGp = baseValues.Gp * playerCount;
        int totalCrucibleInsignia = baseValues.CrucibleInsignia * playerCount;
        int totalCourageInsignia = baseValues.CourageInsignia * playerCount;

        int rankAp = CalcAndFloor(totalAp, RANK_REWARD_RATE);
        int rankGp = CalcAndFloor(totalGp, RANK_REWARD_RATE);
        int rankCrucibleInsignia = CalcAndFloor(totalCrucibleInsignia, RANK_REWARD_RATE);
        int rankCourageInsignia = CalcAndFloor(totalCourageInsignia, RANK_REWARD_RATE);

        int scoreAp = CalcAndFloor(totalAp, SCORE_REWARD_RATE);
        int scoreGp = CalcAndFloor(totalGp, SCORE_REWARD_RATE);
        int scoreCrucibleInsignia = CalcAndFloor(totalCrucibleInsignia, SCORE_REWARD_RATE);
        int scoreCourageInsignia = CalcAndFloor(totalCourageInsignia, SCORE_REWARD_RATE);

        int totalPoints = instanceScore.GetTotalPoints();

        foreach (PvPArenaPlayerReward reward in GetArenaRewards())
        {
            if (reward.IsRewarded())
                continue;

            Player player = instance.GetPlayer(reward.GetOwnerId());
            if (!(reward is HarmonyGroupReward) && player == null) // player left
                continue;

            float configRate = GetConfigRate(player);

            int score = reward.GetScorePoints();
            // Score Formula to verify: floor(scoreAp * winnerKills * winnerPoints / (winnerKills * winnerPoints + loserKills * loserPoints))
            float scoreRate = score / (float)totalPoints;

            int rank = instanceScore.GetRank(reward);
            if (isInterrupted)
                rank = reward is HarmonyGroupReward ? 1 : instance.GetMaxPlayers() - 1;
            float rankRewardRate = GetRewardRate(rank, difficultyId);

            int rankRewardAp = CalcAndFloor(rankAp, rankRewardRate, configRate);
            int rankRewardGp = CalcAndFloor(rankGp, rankRewardRate, 1f);
            int rankRewardCrucibleInsignia = CalcAndFloor(rankCrucibleInsignia, rankRewardRate, configRate);
            int rankRewardCourageInsignia = CalcAndFloor(rankCourageInsignia, rankRewardRate, configRate);

            int scoreRewardAp = CalcAndFloor(scoreAp, scoreRate, configRate);
            int scoreRewardGp = CalcAndFloor(scoreGp, scoreRate, 1f);
            int scoreRewardCrucibleInsignia = CalcAndFloor(scoreCrucibleInsignia, scoreRate, configRate);
            int scoreRewardCourageInsignia = CalcAndFloor(scoreCourageInsignia, scoreRate, configRate);

            BaseRewards baseRewards = GetBaseRewards(difficultyId);
            reward.SetAp(new ArenaRewardItem(0, baseRewards.Ap, rankRewardAp, scoreRewardAp));
            reward.SetGp(new ArenaRewardItem(0, baseRewards.Gp, rankRewardGp, scoreRewardGp));

            ArenaRewardItem crucibleInsignia = new ArenaRewardItem(186000130, baseRewards.CrucibleInsignia, rankRewardCrucibleInsignia,
                scoreRewardCrucibleInsignia);
            ArenaRewardItem courageInsignia = new ArenaRewardItem(186000137, baseRewards.CourageInsignia, rankRewardCourageInsignia,
                scoreRewardCourageInsignia);

            if (crucibleInsignia.GetTotalCount() > 0)
                reward.SetCrucibleInsignia(crucibleInsignia);
            if (courageInsignia.GetTotalCount() > 0)
                reward.SetCourageInsignia(courageInsignia);

            SetRewardItems(reward, rank, difficultyId);

            if (reward is HarmonyGroupReward harmonyGroupReward)
            {
                SplitGroupRewards(harmonyGroupReward);
            }
        }
    }

    private void SplitGroupRewards(HarmonyGroupReward groupReward)
    {
        List<AGPlayer> associatedPlayers = groupReward.GetAssociatedPlayers();
        if (associatedPlayers.Count == 0)
            return;

        int size = associatedPlayers.Count;

        foreach (AGPlayer agPlayer in associatedPlayers)
        {
            PvPArenaPlayerReward apr = instanceScore.GetPlayerReward(agPlayer.ObjectId);
            Player player = instance.GetPlayer(agPlayer.ObjectId);
            if (apr == null || player == null)
                continue;

            float configRate = GetConfigRate(player);
            apr.SetAp(CalculateIndividualReward(groupReward.GetAp(), size, configRate));
            apr.SetGp(CalculateIndividualReward(groupReward.GetGp(), size, 1f));
            apr.SetCrucibleInsignia(CalculateIndividualReward(groupReward.GetCrucibleInsignia(), size, configRate));
            apr.SetCourageInsignia(CalculateIndividualReward(groupReward.GetCourageInsignia(), size, configRate));
            apr.SetRewardItem1(groupReward.GetRewardItem1());
            apr.SetRewardItem2(groupReward.GetRewardItem2());
        }
    }

    private ArenaRewardItem CalculateIndividualReward(ArenaRewardItem rewardItem, int size, float configRate)
    {
        int baseCount = JavaMath.Round(rewardItem.BaseCount * configRate / size);
        int rankingCount = JavaMath.Round(rewardItem.RankingCount * configRate / size);
        int scoreCount = JavaMath.Round(rewardItem.ScoreCount * configRate / size);
        return new ArenaRewardItem(rewardItem.ItemId, baseCount, rankingCount, scoreCount);
    }

    private void Reward()
    {
        foreach (Player player in instance.GetPlayersInside())
        {
            if (player.IsDead())
                Aion.GameServer.Services.Players.PlayerReviveService.DuelRevive(player);
            PvPArenaPlayerReward reward = GetPlayerSpecificReward(player);
            if (!reward.IsRewarded())
            {
                reward.SetRewarded();

                ArenaRewardItem apReward = reward.GetAp();
                if (apReward.GetTotalCount() > 0)
                    Aion.GameServer.Services.Abyss.AbyssPointsService.AddAp(player, apReward.GetTotalCount());

                ArenaRewardItem gpReward = reward.GetGp();
                if (gpReward.GetTotalCount() > 0)
                    Aion.GameServer.Services.Abyss.GloryPointsService.AddGp(player.GetObjectId(), gpReward.GetTotalCount());

                ArenaRewardItem courageInsigniaReward = reward.GetCourageInsignia();
                if (courageInsigniaReward.GetTotalCount() > 0)
                    Aion.GameServer.Services.Items.ItemService.AddItem(player, courageInsigniaReward.ItemId, courageInsigniaReward.GetTotalCount());

                ArenaRewardItem crucibleInsigniaReward = reward.GetCrucibleInsignia();
                if (crucibleInsigniaReward.GetTotalCount() > 0)
                    Aion.GameServer.Services.Items.ItemService.AddItem(player, crucibleInsigniaReward.ItemId, crucibleInsigniaReward.GetTotalCount());

                RewardItem rewardItem1 = reward.GetRewardItem1();
                if (rewardItem1 != null)
                    Aion.GameServer.Services.Items.ItemService.AddItem(player, rewardItem1.GetId(), rewardItem1.GetCount());

                RewardItem rewardItem2 = reward.GetRewardItem2();
                if (rewardItem2 != null)
                    Aion.GameServer.Services.Items.ItemService.AddItem(player, rewardItem2.GetId(), rewardItem2.GetCount());
            }
        }
    }

    protected virtual Npc GetNpc(float x, float y, float z)
    {
        foreach (Npc npc in instance.GetNpcs())
        {
            SpawnTemplate st = npc.GetSpawn();
            if (st.GetX() == x && st.GetY() == y && st.GetZ() == z)
                return npc;
        }
        return null;
    }

    protected virtual void SendEntryPacket(Player player)
    {
        SendPacket(null, null);
    }

    protected virtual void BroadcastUpdate(Player target, InstanceScoreType scoreType)
    {
        SendPacket(null, null);
    }

    protected virtual void BroadcastUpdate(InstanceScoreType scoreType)
    {
        SendPacket(null, null);
    }

    protected virtual void BroadcastResults()
    {
        instance.ForEachPlayer(player => SendPacket(player, InstanceScoreType.SHOW_REWARD));
    }

    protected virtual void SendPacket(Player receiver, InstanceScoreType? scoreType)
    {
    }

    public override bool AllowSelfReviveBySkill()
    {
        return false;
    }

    public override bool AllowSelfReviveByItem()
    {
        return false;
    }

    protected record BaseRewards(int Ap, int Gp, int CrucibleInsignia, int CourageInsignia);
}
