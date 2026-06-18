using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Network.Aion.Instanceinfo;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Rnd = Aion.GameServer.Commons.Utils.Rnd;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>
/// Java parity: instance/dredgion/DredgionInstance (xTz) : GeneralInstanceHandler. Base for Chantra/Baranath/Terath Dredgion.
/// AtomicInteger→AtomicInteger; AtomicBoolean→bool field guarded; Future→ScheduledTask; ItemService ns Services.Items;
/// PlayerReviveService ns Services.Players; AbyssPointsService fully-qualified Services.Abyss. 1:1.
/// </summary>
public class DredgionInstance : GeneralInstanceHandler
{
    protected readonly List<DredgionRoom> dredgionRooms = new List<DredgionRoom>();
    protected readonly AtomicInteger killedSurkanas = new AtomicInteger();
    protected AtomicBoolean isInstanceStarted = new AtomicBoolean();
    protected PvpInstanceScore<PvpInstancePlayerReward> instanceScore;
    private const int MAX_PLAYERS_PER_FACTION = 6;
    private readonly int raceStartPosition = Rnd.NextInt(2);
    private ScheduledTask instanceTask;
    private long instanceTime;
    private float losingGroupMultiplier = 1;
    private bool isInstanceDestroyed;

    public DredgionInstance(WorldMapInstance instance) : base(instance)
    {
    }

    protected virtual PvpInstancePlayerReward GetPlayerReward(Player player)
    {
        if (instanceScore.GetPlayerReward(player.GetObjectId()) == null)
        {
            AddPlayerToReward(player);
        }
        return instanceScore.GetPlayerReward(player.GetObjectId());
    }

    protected virtual void CaptureRoom(Race race, int roomId)
    {
        foreach (DredgionRoom dredgionRoom in dredgionRooms)
        {
            if (dredgionRoom.GetRoomId() == roomId)
            {
                dredgionRoom.CaptureRoom(race);
            }
        }
    }

    private void AddPlayerToReward(Player player)
    {
        instanceScore.AddPlayerReward(new PvpInstancePlayerReward(player.GetObjectId(), player.GetRace()));
    }

    protected virtual void StartInstanceTask()
    {
        instanceTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            OpenFirstDoors();
            instanceScore.SetInstanceProgressionType(InstanceProgressionType.START_PROGRESS);
            SendPacket();
        }, 120000L);
        instanceTask = ThreadPoolManager.GetInstance().Schedule(() => StopInstance(instanceScore.GetRaceWithHighestPoints()), 2520000L);
    }

    public override void OnEnterInstance(Player player)
    {
        if (!instanceScore.ContainsPlayer(player.GetObjectId()))
            instanceScore.AddPlayerReward(new PvpInstancePlayerReward(player.GetObjectId(), player.GetRace()));
        SendPacket();
    }

    public override void OnInstanceCreate()
    {
        InitializeInstance(4500, 2500, 3750);
    }

    protected virtual void InitializeInstance(int winnerAp, int loserAp, int drawAp)
    {
        instanceScore = new PvpInstanceScore<PvpInstancePlayerReward>(winnerAp, loserAp, drawAp);
        instanceScore.SetInstanceProgressionType(InstanceProgressionType.PREPARING);
        for (int i = 1; i < 15; i++)
        {
            dredgionRooms.Add(new DredgionRoom(i));
        }
    }

    protected virtual void StopInstance(Race winnerRace)
    {
        StopInstanceTask();
        instanceScore.SetInstanceProgressionType(InstanceProgressionType.END_PROGRESS);
        instance.ForEachPlayer(p => DoReward(p, GetPlayerReward(p), winnerRace));
        instance.ForEachNpc(npc => npc.GetController().Delete());
        ThreadPoolManager.GetInstance().Schedule(() => instance.GetPlayersInside().ForEach(RevivePlayerOnEnd), 10000L);
        ThreadPoolManager.GetInstance().Schedule(() => instance.GetPlayersInside().ForEach(LeaveInstance), 60000L);
        SendPacket();
    }

    public virtual void DoReward(Player player, PvpInstancePlayerReward reward, Race winningRace)
    {
        int scorePoints = instanceScore.GetPointsByRace(reward.GetRace());
        if (reward.GetRace() == winningRace)
        {
            reward.SetBaseAp(instanceScore.GetWinnerApReward());
            reward.SetBonusAp(2 * scorePoints / MAX_PLAYERS_PER_FACTION);
        }
        else
        {
            reward.SetBaseAp(instanceScore.GetLoserApReward());
            reward.SetBonusAp(scorePoints / MAX_PLAYERS_PER_FACTION);
            if (winningRace == Race.NONE)
                reward.SetBaseAp(instanceScore.GetDrawApReward()); // Base AP are overridden in a draw case
        }
        DistributeRewards(player, reward);
    }

    private void DistributeRewards(Player player, PvpInstancePlayerReward reward)
    {
        QuestEnv env = new QuestEnv(null, player, 0);
        QuestEngine.QuestEngine.GetInstance().OnDredgionReward(env);
        Aion.GameServer.Services.Abyss.AbyssPointsService.AddAp(player, Rates.AP_DREDGION.CalcResult(player, reward.GetBaseAp() + reward.GetBonusAp()));
        if (reward.GetReward1ItemId() > 0)
            Aion.GameServer.Services.Items.ItemService.AddItem(player, reward.GetReward1ItemId(), reward.GetReward1Count() + reward.GetReward1BonusCount(), true);
        if (reward.GetReward2ItemId() > 0)
            Aion.GameServer.Services.Items.ItemService.AddItem(player, reward.GetReward2ItemId(), reward.GetReward2Count() + reward.GetReward2BonusCount(), true);
        if (reward.GetReward3ItemId() > 0)
            Aion.GameServer.Services.Items.ItemService.AddItem(player, reward.GetReward3ItemId(), reward.GetReward3Count(), true);
        if (reward.GetReward4ItemId() > 0)
            Aion.GameServer.Services.Items.ItemService.AddItem(player, reward.GetReward4ItemId(), reward.GetReward4Count(), true);
        if (reward.GetBonusRewardItemId() > 0)
            Aion.GameServer.Services.Items.ItemService.AddItem(player, reward.GetBonusRewardItemId(), reward.GetBonusRewardCount(), true);
    }

    public override bool OnReviveEvent(Player player)
    {
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME());
        Aion.GameServer.Services.Players.PlayerReviveService.Revive(player, 100, 100, false, 0);
        player.GetGameStats().UpdateStatsAndSpeedVisually();
        PortToStartPosition(player);
        return true;
    }

    protected virtual void RevivePlayerOnEnd(Player player)
    {
        if (player.IsDead())
            Aion.GameServer.Services.Players.PlayerReviveService.DuelRevive(player);
    }

    public override bool OnDie(Player player, Creature lastAttacker)
    {
        int points = 60;
        if (lastAttacker is Player killer && killer.GetRace() != player.GetRace())
        {
            if (killer.GetRace() != instanceScore.GetRaceWithHighestPoints())
                points = (int)(points * losingGroupMultiplier);
            else if (losingGroupMultiplier == 10 || GetPlayerReward(player).GetPoints() == 0)
                points = 0;

            if (player.GetAbyssRank().Rank - killer.GetAbyssRank().Rank >= 4)
                points = (int)(points * 1.6f);

            UpdateScore((Player)lastAttacker, player, points, true);
        }
        UpdateScore(player, player, -points, false);
        return true;
    }

    private void AddPointToPlayer(Player player, int points)
    {
        GetPlayerReward(player).AddPoints(points);
    }

    private void AddPvPKillToPlayer(Player player)
    {
        GetPlayerReward(player).AddPvPKill();
    }

    private void AddBalaurKillToPlayer(Player player)
    {
        GetPlayerReward(player).AddMonsterKillToPlayer();
    }

    protected virtual void UpdateScore(Player player, Creature target, int points, bool pvpKill)
    {
        if (points == 0)
            return;

        // group score
        instanceScore.AddPointsByRace(player.GetRace(), points);

        // player score
        List<Player> playersToGainScore = new List<Player>();

        if (target != null && player.IsInGroup())
        {
            foreach (Player member in player.GetPlayerGroup().GetOnlineMembers())
            {
                if (member.IsDead())
                {
                    continue;
                }
                if (PositionUtil.IsInRange(member, target, GroupConfig.GROUP_MAX_DISTANCE))
                {
                    playersToGainScore.Add(member);
                }
            }
        }
        else
        {
            playersToGainScore.Add(player);
        }

        foreach (Player playerToGainScore in playersToGainScore)
        {
            AddPointToPlayer(playerToGainScore, points / playersToGainScore.Count);
            if (target is Npc)
            {
                PacketSendUtility.SendPacket(playerToGainScore, SM_SYSTEM_MESSAGE.STR_MSG_GET_SCORE(((Npc)target).GetObjectTemplate().GetL10n(), points));
            }
            else if (target is Player)
            {
                PacketSendUtility.SendPacket(playerToGainScore, SM_SYSTEM_MESSAGE.STR_MSG_GET_SCORE(target.GetName(), points));
            }
        }

        // recalculate point multiplier
        int pointDifference = instanceScore.GetAsmodiansPoints() - instanceScore.GetElyosPoints();
        if (pointDifference < 0)
        {
            pointDifference *= -1;
        }
        if (pointDifference >= 3000)
        {
            losingGroupMultiplier = 10;
        }
        else if (pointDifference >= 1000)
        {
            losingGroupMultiplier = 1.5f;
        }
        else
        {
            losingGroupMultiplier = 1;
        }

        // pvpKills for pvp and balaurKills for pve
        if (pvpKill && points > 0)
        {
            AddPvPKillToPlayer(player);
        }
        else if (target is Npc && target.GetRace() == Race.DRAKAN)
        {
            AddBalaurKillToPlayer(player);
        }
        SendPacket();
    }

    public override void OnDie(Npc npc)
    {
        Player mostPlayerDamage = npc.GetAggroList().GetMostPlayerDamage();
        if (mostPlayerDamage == null)
            return;
        int hpGauge = npc.GetObjectTemplate().GetHpGauge();
        if (hpGauge <= 5)
        {
            UpdateScore(mostPlayerDamage, npc, 12, false);
        }
        else if (hpGauge <= 9)
        {
            UpdateScore(mostPlayerDamage, npc, 32, false);
        }
        else
        {
            UpdateScore(mostPlayerDamage, npc, 42, false);
        }
    }

    public override void OnInstanceDestroy()
    {
        StopInstanceTask();
        isInstanceDestroyed = true;
        instanceScore.Clear();
    }

    protected virtual void OpenFirstDoors()
    {
    }

    private void SendPacket()
    {
        PacketSendUtility.BroadcastToMap(instance,
            new SM_INSTANCE_SCORE(instance.GetMapId(), new DredgionScoreWriter(instanceScore, instance.GetPlayersInside(), dredgionRooms), GetTime()));
    }

    private int GetTime()
    {
        long result = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - instanceTime;
        if (result < 120000)
        {
            return (int)(120000 - result);
        }
        else if (result < 2520000)
        {
            return (int)(2400000 - (result - 120000));
        }
        return 0;
    }

    protected virtual void Sp(int npcId, float x, float y, float z, byte h, int time)
    {
        Sp(npcId, x, y, z, h, 0, time);
    }

    protected virtual void Sp(int npcId, float x, float y, float z, byte h, int staticId, int time)
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (!isInstanceDestroyed)
            {
                Spawn(npcId, x, y, z, h, staticId);
            }
        }, (long)time);
    }

    protected virtual void Sp(int npcId, float x, float y, float z, byte h, int time, string walkerId)
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (!isInstanceDestroyed)
            {
                Npc npc = (Npc)Spawn(npcId, x, y, z, h);
                npc.GetSpawn().SetWalkerId(walkerId);
                WalkManager.StartWalking((NpcAI)npc.GetAi());
            }
        }, (long)time);
    }

    protected virtual void SendMsgByRace(int msg, Race race, int time)
    {
        ThreadPoolManager.GetInstance().Schedule(() => instance.ForEachPlayer(player =>
        {
            if (player.GetRace() == race || race == Race.PC_ALL)
            {
                PacketSendUtility.SendPacket(player, new SM_SYSTEM_MESSAGE(msg));
            }
        }), (long)time);
    }

    private void StopInstanceTask()
    {
        if (instanceTask != null)
        {
            instanceTask.Cancel(true);
        }
    }

    public override InstanceScore GetInstanceScore()
    {
        return instanceScore;
    }

    public override void LeaveInstance(Player player)
    {
        Aion.GameServer.Services.Teleport.TeleportService.MoveToInstanceExit(player, mapId, player.GetRace());
    }

    public override void PortToStartPosition(Player player)
    {
        if (player.GetRace() == Race.ELYOS && raceStartPosition == 0 || player.GetRace() == Race.ASMODIANS && raceStartPosition != 0)
        {
            Aion.GameServer.Services.Teleport.TeleportService.TeleportTo(player, instance.GetMapId(), instance.GetInstanceId(), 570.468f, 166.897f, 432.28986f);
        }
        else
        {
            Aion.GameServer.Services.Teleport.TeleportService.TeleportTo(player, instance.GetMapId(), instance.GetInstanceId(), 400.741f, 166.713f, 432.290f);
        }
    }
}
