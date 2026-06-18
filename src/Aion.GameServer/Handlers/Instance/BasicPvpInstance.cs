using System;
using System.Collections.Generic;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Instanceinfo;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Rnd = Aion.GameServer.Commons.Utils.Rnd;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>
/// Java parity: instance/pvp/BasicPvpInstance (Estrayl) : GeneralInstanceHandler. Super class for all newer PvP instances
/// introduced by 4.x updates: Idgel Dome, Engulfed Ophidan Bridge, Kamar Battlefield, Iron Wall Warfront.
/// Future&lt;?&gt;→ScheduledTask; AbyssPointsService/GloryPointsService fully-qualified Services.Abyss; ItemService ns Services.Item;
/// PlayerReviveService ns Services.Player. 1:1.
/// </summary>
public class BasicPvpInstance : GeneralInstanceHandler
{
    protected readonly List<ScheduledTask> tasks = new List<ScheduledTask>();
    protected readonly int raceStartPosition = Rnd.NextInt(2);
    protected PvpInstanceScore<PvpInstancePlayerReward> instanceScore;
    protected long startTime;

    public BasicPvpInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        if (instanceScore == null)
            return;

        instanceScore.AddPointsByRace(Race.ELYOS, 1000);
        instanceScore.AddPointsByRace(Race.ASMODIANS, 1000);
        UpdateProgress(InstanceProgressionType.REINFORCE_MEMBER);
        SpawnFactionRelatedNpcs();
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(StartPreparation, GetReinforceMemberPhaseDelay()));
    }

    protected virtual void SpawnFactionRelatedNpcs()
    {
    }

    private void StartPreparation()
    {
        UpdateProgress(InstanceProgressionType.PREPARING);
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(OnStart, 60000L));
    }

    protected virtual void OnStart()
    {
    }

    protected virtual void OnStop(bool isBossKilled)
    {
        CancelTasks();
        UpdateProgress(InstanceProgressionType.END_PROGRESS);

        Race winningRace = instanceScore.GetRaceWithHighestPoints();
        instance.ForEachPlayer(p => SetAndDistributeRewards(p, instanceScore.GetPlayerReward(p.GetObjectId()), winningRace, isBossKilled));
        instance.ForEachNpc(npc => npc.GetController().Delete());
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() => instance.GetPlayersInside().ForEach(RevivePlayerOnEnd), 10000L));
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() => instance.GetPlayersInside().ForEach(LeaveInstance), 60000L));
    }

    protected virtual void SetAndDistributeRewards(Player player, PvpInstancePlayerReward reward, Race winningRace, bool isBossKilled)
    {
    }

    protected virtual void DistributeRewards(Player player, PvpInstancePlayerReward reward)
    {
        PacketSendUtility.SendPacket(player, new SM_INSTANCE_SCORE(instance.GetMapId(),
            new PvpInstanceScoreWriter(instanceScore, InstanceScoreType.SHOW_REWARD, player.GetObjectId(), 0), GetTime()));
        Aion.GameServer.Services.Abyss.AbyssPointsService.AddAp(player, (int)Rates.AP_DREDGION.CalcResult(player, reward.GetBaseAp() + reward.GetBonusAp()));
        int gpToAdd = reward.GetBaseGp() + reward.GetBonusGp();
        if (gpToAdd > 0)
            Aion.GameServer.Services.Abyss.GloryPointsService.AddGp(player.GetObjectId(), (int)Rates.GP.CalcResult(player, gpToAdd));
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

    public override void LeaveInstance(Player player)
    {
        if (instance.Equals(player.GetWorldMapInstance()))
            Aion.GameServer.Services.Teleport.TeleportService.MoveToInstanceExit(player, mapId, player.GetRace());
    }

    public override void OnLeaveInstance(Player player)
    {
        base.OnLeaveInstance(player);
        PvpInstancePlayerReward reward = instanceScore.GetPlayerReward(player.GetObjectId());
        if (reward != null && instanceScore.GetInstanceProgressionType() != InstanceProgressionType.END_PROGRESS)
            instanceScore.RemovePlayerReward(reward);
        SendPacket(new SM_INSTANCE_SCORE(instance.GetMapId(),
            new PvpInstanceScoreWriter(instanceScore, InstanceScoreType.PLAYER_QUIT, player.GetObjectId(), 0), GetTime()));
    }

    public override bool OnDie(Player victim, Creature lastAttacker)
    {
        SendPacket(new SM_INSTANCE_SCORE(instance.GetMapId(),
            new PvpInstanceScoreWriter(instanceScore, InstanceScoreType.UPDATE_PLAYER_BUFF_STATUS, victim.GetObjectId(), 60), GetTime()));
        if (lastAttacker is Player killer && killer.GetRace() != victim.GetRace())
        {
            int killPoints = 200;
            if (instanceScore.IsStartProgress() && GetTime() <= 600000 && victim.GetRace() != instanceScore.GetRaceWithHighestPoints())
                killPoints += 100; // After 10 minutes the outplayed faction gets bonus points

            if (victim.GetAbyssRank().Rank - killer.GetAbyssRank().Rank >= 4)
                killPoints = (int)(killPoints * 1.6f);

            UpdatePoints(killer, killer.GetRace(), null, killPoints, false, true);
        }

        UpdatePoints(victim, victim.GetRace(), null, -100, true, false);
        return true;
    }

    public override bool OnReviveEvent(Player player)
    {
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME());
        Aion.GameServer.Services.Players.PlayerReviveService.Revive(player, 100, 100, false, 0);
        player.GetGameStats().UpdateStatsAndSpeedVisually();
        PortToStartPosition(player);
        SendPacket(new SM_INSTANCE_SCORE(instance.GetMapId(),
            new PvpInstanceScoreWriter(instanceScore, InstanceScoreType.UPDATE_PLAYER_BUFF_STATUS, player.GetObjectId(), 0), GetTime()));
        return true;
    }

    public override void OnEnterInstance(Player player)
    {
        if (!instanceScore.ContainsPlayer(player.GetObjectId()))
            instanceScore.AddPlayerReward(new PvpInstancePlayerReward(player.GetObjectId(), player.GetRace()));

        SendPacket(new SM_INSTANCE_SCORE(instance.GetMapId(),
            new PvpInstanceScoreWriter(instanceScore, InstanceScoreType.INIT_PLAYER, player.GetObjectId(), 0), GetTime()));
        SendPacket(new SM_INSTANCE_SCORE(instance.GetMapId(),
            new PvpInstanceScoreWriter(instanceScore, InstanceScoreType.UPDATE_INSTANCE_BUFFS_AND_SCORE, instance.GetPlayersInside()), GetTime()));
        SendPacket(new SM_INSTANCE_SCORE(instance.GetMapId(),
            new PvpInstanceScoreWriter(instanceScore, InstanceScoreType.UPDATE_PLAYER_BUFF_STATUS, player.GetObjectId(), 0), GetTime()));
    }

    public override void OnInstanceDestroy()
    {
        CancelTasks();
    }

    protected virtual int GetReinforceMemberPhaseDelay()
    {
        return 60000;
    }

    protected virtual int GetTime()
    {
        int current = (int)(System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime);
        return instanceScore.GetInstanceProgressionType() switch
        {
            InstanceProgressionType.REINFORCE_MEMBER => 120000 - current,
            InstanceProgressionType.PREPARING => 60000 - current,
            InstanceProgressionType.START_PROGRESS => 1200000 - current,
            InstanceProgressionType.END_PROGRESS => 1200000 - current,
            _ => 1200000 - current,
        };
    }

    protected virtual void UpdateProgress(InstanceProgressionType progressionType)
    {
        instanceScore.SetInstanceProgressionType(progressionType);
        startTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); // Reset start time
        SendPacket(
            new SM_INSTANCE_SCORE(instance.GetMapId(), new PvpInstanceScoreWriter(instanceScore, InstanceScoreType.UPDATE_INSTANCE_PROGRESS), GetTime()));
        SendPacket(new SM_INSTANCE_SCORE(instance.GetMapId(),
            new PvpInstanceScoreWriter(instanceScore, InstanceScoreType.UPDATE_ALL_PLAYER_INFO, instance.GetPlayersInside()), GetTime()));
        SendPacket(new SM_INSTANCE_SCORE(instance.GetMapId(),
            new PvpInstanceScoreWriter(instanceScore, InstanceScoreType.UPDATE_INSTANCE_BUFFS_AND_SCORE, instance.GetPlayersInside()), GetTime()));
    }

    protected virtual void UpdatePoints(Player player, Race race, string npcL10n, int points)
    {
        UpdatePoints(player, race, npcL10n, points, false, false);
    }

    protected virtual void UpdatePoints(Player player, Race race, string npcL10n, int points, bool isVictim, bool shouldRewardPvpKill)
    {
        if (!instanceScore.IsStartProgress())
            return;

        instanceScore.AddPointsByRace(race, points);
        if (player != null)
        {
            PvpInstancePlayerReward pReward = instanceScore.GetPlayerReward(player.GetObjectId());
            pReward.AddPoints(points);
            if (isVictim)
            {
                SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_LOSE_SCORE_ENEMY(player.GetName(), race.GetL10n(), points));
            }
            else
            {
                if (shouldRewardPvpKill)
                {
                    instanceScore.IncrementKillsByRace(race);
                    pReward.AddPvPKill();
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_GET_SCORE_FOR_ENEMY(points));
                }
                else if (npcL10n != null)
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_GET_SCORE(npcL10n, points));
                }
            }
        }
        SendPacket(
            new SM_INSTANCE_SCORE(instance.GetMapId(), new PvpInstanceScoreWriter(instanceScore, InstanceScoreType.UPDATE_FACTION_SCORE, race), GetTime()));
        SendPacket(new SM_INSTANCE_SCORE(instance.GetMapId(),
            new PvpInstanceScoreWriter(instanceScore, InstanceScoreType.UPDATE_ALL_PLAYER_INFO, instance.GetPlayersInside()), GetTime()));
    }

    protected virtual void RevivePlayerOnEnd(Player player)
    {
        if (player.IsDead())
            Aion.GameServer.Services.Players.PlayerReviveService.DuelRevive(player);
    }

    protected virtual void SendPacket(AionServerPacket packet)
    {
        PacketSendUtility.BroadcastToMap(instance, packet);
    }

    protected virtual void CancelTasks()
    {
        foreach (ScheduledTask task in tasks)
            if (task != null && !task.IsCancelled)
                task.Cancel(true);
    }

    public override InstanceScore GetInstanceScore()
    {
        return instanceScore;
    }
}
