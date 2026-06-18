using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Rnd = Aion.GameServer.Commons.Utils.Rnd;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>
/// Java parity: instance/pvp/IdgelDomeInstance (Ritsu, Estrayl) : BasicPvpInstance. 1:1.
/// </summary>
[InstanceID(301310000)]
public class IdgelDomeInstance : BasicPvpInstance
{
    private const int MAX_PLAYERS_PER_FACTION = 6;
    private readonly List<WorldPosition> chestPositions = new List<WorldPosition>();

    public IdgelDomeInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        instanceScore = new PvpInstanceScore<PvpInstancePlayerReward>(5600, 1120, 3360); // No info found for draws, so let's guess
        base.OnInstanceCreate();
    }

    protected override void SpawnFactionRelatedNpcs()
    {
        Spawn((raceStartPosition == 0 ? 802384 : 802383), 254.9255f, 179.3691f, 80.3904f, (byte)90); // Quest Npcs
        Spawn((raceStartPosition == 0 ? 802383 : 802384), 274.5722f, 338.7958f, 80.6454f, (byte)31);
        SpawnAndSetRespawn((raceStartPosition == 0 ? 702300 : 702301), 286.2041f, 338.7794f, 79.8274f, (byte)105, 20); // Defense Turrets
        SpawnAndSetRespawn((raceStartPosition == 0 ? 702300 : 702301), 279.9443f, 332.1660f, 79.8274f, (byte)105, 20);
        SpawnAndSetRespawn((raceStartPosition == 0 ? 702300 : 702301), 263.9707f, 341.6699f, 79.7848f, (byte)15, 20);
        SpawnAndSetRespawn((raceStartPosition == 0 ? 702300 : 702301), 277.0629f, 355.2583f, 79.7844f, (byte)75, 20);
        SpawnAndSetRespawn((raceStartPosition == 0 ? 702301 : 702300), 243.0305f, 179.2297f, 79.8241f, (byte)45, 20);
        SpawnAndSetRespawn((raceStartPosition == 0 ? 702301 : 702300), 249.3263f, 185.8511f, 79.8241f, (byte)45, 20);
        SpawnAndSetRespawn((raceStartPosition == 0 ? 702301 : 702300), 252.1385f, 162.8889f, 79.7711f, (byte)15, 20);
        SpawnAndSetRespawn((raceStartPosition == 0 ? 702301 : 702300), 265.3730f, 176.5772f, 79.7719f, (byte)75, 20);
        SpawnAndSetRespawn((raceStartPosition == 0 ? 802548 : 802549), 199.187f, 191.761f, 80.7466f, (byte)15, 180); // Lever
        SpawnAndSetRespawn((raceStartPosition == 0 ? 802549 : 802548), 329.799f, 326.113f, 81.8731f, (byte)75, 180);
    }

    protected override void OnStart()
    {
        UpdateProgress(InstanceProgressionType.START_PROGRESS);
        instance.ForEachDoor(door => door.SetOpen(true));
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() => Spawn(287249, 264.4382f, 258.58527f, 88.452042f, (byte)31), 600000L));
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() => OnStop(false), 1200000L));
        SpawnChest();
        SpawnChest();
    }

    protected override void SetAndDistributeRewards(Player player, PvpInstancePlayerReward reward, Race winningRace, bool isBossKilled)
    {
        int scorePoints = instanceScore.GetPointsByRace(reward.GetRace());
        if (reward.GetRace() == winningRace)
        {
            reward.SetBaseAp(instanceScore.GetWinnerApReward() + (isBossKilled ? 2000 : 0));
            reward.SetBonusAp(2 * scorePoints / MAX_PLAYERS_PER_FACTION);
            reward.SetBaseGp(50);
            reward.SetReward1(186000242, 3, 0); // Custom: Ceramium Medal
            reward.SetReward2(188053030, 1, 0); // Idgel Dome Reward Box
            if (isBossKilled)
            {
                int mythicKunaxEqItemId = 0;
                if (Rnd.Chance() < 20)
                    mythicKunaxEqItemId = reward.GetMythicKunaxEquipment(player);
                reward.SetReward3(mythicKunaxEqItemId, mythicKunaxEqItemId == 0 ? 0 : 1);
                reward.SetReward4(188053032, 1); // Idgel Dome Tribute Box
            }
        }
        else
        {
            reward.SetBaseAp(instanceScore.GetLoserApReward());
            reward.SetBonusAp(scorePoints / MAX_PLAYERS_PER_FACTION);
            reward.SetBaseGp(10);
            reward.SetReward1(186000242, 1, 0); // Custom: Ceramium Medal
            reward.SetReward2(188053031, 1, 0); // Idgel Dome Consolation Prize
            if (winningRace == Race.NONE)
                reward.SetBaseAp(instanceScore.GetDrawApReward()); // Base AP are overridden in a draw case
        }
        DistributeRewards(player, reward);
    }

    public override void OnSpawn(VisibleObject obj)
    {
        if (obj is Npc npc)
        {
            switch (npc.GetNpcId())
            {
                case 287249: // Kunax
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5_FORTRESS_RE_BOSS_SPAWN());
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_FORTRESS_RE_BOSSSPAWN());
                    break;
                case 802548:
                case 802549:
                    foreach (Player p in instance.GetPlayersInside().Where(p => p.GetRace() != npc.GetRace()))
                        SetFlameVentNoInteraction(p, obj);
                    break;
            }
        }
    }

    private void SetFlameVentNoInteraction(Player player, VisibleObject flameVent)
    {
        PacketSendUtility.SendPacket(player, new SM_CUSTOM_SETTINGS(flameVent.GetObjectId(), 0, CreatureType.PEACE.GetId(), 0));
    }

    public override void OnDie(Npc npc)
    {
        base.OnDie(npc);
        Player player = npc.GetAggroList().GetMostPlayerDamage(); // Checked with retail, maybe the player making the last hit gets the points
        if (player == null || instanceScore.GetInstanceProgressionType() != InstanceProgressionType.START_PROGRESS)
            return;

        int points = 0;
        bool isKunaxKilled = false;
        switch (npc.GetNpcId())
        {
            case 234186:
            case 234187:
            case 234189:
                points = 120;
                break;
            case 234751:
            case 234752:
            case 234753:
                points = 200;
                break;
            case 287249:
                points = 6000;
                isKunaxKilled = true;
                break;
        }
        if (points > 0)
            UpdatePoints(player, player.GetRace(), npc.GetObjectTemplate().GetL10n(), points);
        if (isKunaxKilled)
            OnStop(true);
    }

    private void SpawnChest()
    {
        WorldPosition p = Rnd.Get(chestPositions);
        if (p != null)
        {
            chestPositions.Remove(p);
            Spawn(702581 + Rnd.Get(0, 2), p.GetX(), p.GetY(), p.GetZ(), p.GetHeading());
        }
    }

    private void ScheduleChestRespawn()
    {
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(SpawnChest, 120000L));
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 702581:
            case 702582:
            case 702583:
                chestPositions.Add(npc.GetPosition());
                ScheduleChestRespawn();
                break;
            case 802548:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_FORTRESS_RE_FIRESPAWN_A());
                break;
            case 802549:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_FORTRESS_RE_FIRESPAWN_B());
                break;
        }
    }

    public override void OnEnterInstance(Player player)
    {
        Npc forbiddenFlameVent = GetNpc(player.GetRace() == Race.ASMODIANS ? 802548 : 802549);
        if (forbiddenFlameVent != null)
            SetFlameVentNoInteraction(player, forbiddenFlameVent);

        base.OnEnterInstance(player);
    }

    public override void PortToStartPosition(Player player)
    {
        if (player.GetRace() == Race.ELYOS && raceStartPosition == 0 || player.GetRace() == Race.ASMODIANS && raceStartPosition != 0)
            Aion.GameServer.Services.Teleport.TeleportService.TeleportTo(player, instance.GetMapId(), instance.GetInstanceId(), 269.76874f, 348.35953f, 79.44365f, (byte)105);
        else
            Aion.GameServer.Services.Teleport.TeleportService.TeleportTo(player, instance.GetMapId(), instance.GetInstanceId(), 259.3971f, 169.18243f, 79.430855f, (byte)45);
    }

    public override bool IsBoss(Npc npc)
    {
        return npc.GetNpcId() == 287249;
    }
}
