using System;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Rnd = Aion.GameServer.Commons.Utils.Rnd;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>
/// Java parity: instance/pvp/IronWallWarfrontInstance (Tibald) : BasicPvpInstance. 1:1.
/// </summary>
[InstanceID(301220000)]
public class IronWallWarfrontInstance : BasicPvpInstance
{
    private const int MAX_PLAYERS_PER_FACTION = 24;

    public IronWallWarfrontInstance(WorldMapInstance instance) : base(instance)
    {
    }

    protected override void OnStart()
    {
        UpdateProgress(InstanceProgressionType.PREPARING);
        instance.GetPlayersInside().ForEach(PortToStartPosition); // split groups
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(EndPreparingAndStart, 60000L));
    }

    private void EndPreparingAndStart()
    {
        UpdateProgress(InstanceProgressionType.START_PROGRESS);
        OpenFirstDoors();
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() => OnStop(false), 2400000L));
    }

    protected override void SetAndDistributeRewards(Player player, PvpInstancePlayerReward reward, Race winningRace, bool isBossKilled)
    {
        int scorePoints = instanceScore.GetPointsByRace(reward.GetRace());
        if (reward.GetRace() == winningRace)
        {
            reward.SetBaseAp(instanceScore.GetWinnerApReward() + (isBossKilled ? 3850 : 0)); // increased by 3850 if pashid is killed
            reward.SetBonusAp(2 * scorePoints / MAX_PLAYERS_PER_FACTION);
            reward.SetBaseGp(100);
            reward.SetReward1(186000243, 9, 0); // Fragmented Ceramium
            if (isBossKilled)
            {
                reward.SetReward2(188052729, 1, 0); // Eternal Bastion Warfront Reward Chest
                if (Rnd.Chance() < 5)
                    reward.SetReward3(188950020, 1); // CUSTOM: Special Courier Pass (Abyss Mythic/Lv. 61-65)
            }
        }
        else
        {
            reward.SetBaseAp(instanceScore.GetLoserApReward());
            reward.SetBonusAp(scorePoints / MAX_PLAYERS_PER_FACTION);
            reward.SetBaseGp(10);
            if (winningRace == Race.NONE)
                reward.SetBaseAp(instanceScore.GetDrawApReward()); // Base AP are overridden in a draw case
        }
        DistributeRewards(player, reward);
    }

    protected override void UpdatePoints(Player player, Race race, string npcL10n, int points)
    {
        base.UpdatePoints(player, race, npcL10n, points);

        int diff = Math.Abs(instanceScore.GetAsmodiansPoints() - instanceScore.GetElyosPoints());
        if (diff >= 30000)
            OnStop(false);
    }

    public override void OnDie(Npc npc)
    {
        Player player = npc.GetAggroList().GetMostPlayerDamage();
        if (player == null)
        {
            return;
        }
        int points = 0;
        switch (npc.GetNpcId())
        {
            case 233473:
                points = 100;
                break;
            case 702042:
                points = 500;
                break;
            case 233491: // random position boss
            case 701943: // elyos power generator
            case 701944: // asmodian power generator
            case 701945: // balaur power generator
                points = 5000;
                break;
            case 233494:
                points = 30000;
                break;
        }
        if (points > 0)
        {
            UpdatePoints(player, player.GetRace(), npc.GetObjectTemplate().GetL10n(), points);
        }
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        if (player == null)
        {
            return;
        }
        int points = 0;
        switch (npc.GetNpcId())
        {
            case 801903:
                points = 1500;
                break;
            case 801772:
                points = 525;
                break;
            case 801766:
            case 801767:
            case 801818:
            case 801819:
            case 801820:
            case 801821:
                points = 255;
                break;
            case 730861:
            case 730878:
            case 730879:
            case 730880:
                UpdatePoints(player, player.GetRace(), npc.GetObjectTemplate().GetL10n(), 200);
                if (player.GetRace() == Race.ELYOS)
                {
                    Spawn(701900, npc.GetX(), npc.GetY(), npc.GetZ(), npc.GetHeading());
                }
                else
                {
                    Spawn(701901, npc.GetX(), npc.GetY(), npc.GetZ(), npc.GetHeading());
                    npc.GetController().Delete();
                }
                break;
        }
        if (points > 0)
        {
            UpdatePoints(player, player.GetRace(), npc.GetObjectTemplate().GetL10n(), points);
            npc.GetController().Delete();
        }
    }

    private void OpenFirstDoors()
    {
        instance.SetDoorState(177, true);
        instance.SetDoorState(176, true);
    }

    protected override int GetReinforceMemberPhaseDelay()
    {
        return 120000;
    }

    public override void OnInstanceCreate()
    {
        instanceScore = new PvpInstanceScore<PvpInstancePlayerReward>(8400, 1680, 5040); // No info found for draws, so let's guess
        base.OnInstanceCreate();
    }

    public void PortToPosition(Player player, WorldMapInstance instance)
    {
        bool useAlternativePos = player.IsInAlliance()
            && (player.GetPlayerAllianceGroup().GetObjectId() == 1001 || player.GetPlayerAllianceGroup().GetObjectId() == 1003);
        if (player.GetRace() == Race.ELYOS && raceStartPosition == 0 || player.GetRace() == Race.ASMODIANS && raceStartPosition != 0)
        {
            if (useAlternativePos)
                Aion.GameServer.Services.Teleport.TeleportService.TeleportTo(player, instance, 274.143f, 384.335f, 239.973f, (byte)14);
            else
                Aion.GameServer.Services.Teleport.TeleportService.TeleportTo(player, instance, 342.138f, 616.856f, 248.197f, (byte)35);
        }
        else
        {
            if (useAlternativePos)
                Aion.GameServer.Services.Teleport.TeleportService.TeleportTo(player, instance, 598.229f, 712.984f, 223.306f, (byte)73);
            else
                Aion.GameServer.Services.Teleport.TeleportService.TeleportTo(player, instance, 711.403f, 621.797f, 213.276f, (byte)31);
        }
    }
}
