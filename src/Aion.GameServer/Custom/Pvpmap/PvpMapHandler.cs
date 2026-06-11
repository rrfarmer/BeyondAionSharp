using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Actions;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Cron;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;
using Aion.GameServer.World.Knownlist;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Custom.Pvpmap;

/// <summary>Java parity: custom/pvpmap/PvpMapHandler (Yeats) : GeneralInstanceHandler. HashMap→Dictionary; Future&lt;?&gt;→ScheduledTask (cancel(true)→Cancel(), isCancelled()→IsCancelled); schedule/scheduleAtFixedRate (incl TimeUnit.MINUTES)→Schedule/ScheduleAtFixedRateTask(TimeSpan, ct=>{...;return ValueTask.CompletedTask;}); anonymous ItemUseObserver{abort}→nested PvpMapTeleportObserver; synchronized methods→lock(this); Math.toRadians→*Math.PI/180; Float.isNaN→float.IsNaN; Map.get→indexer/Remove(out); switch-expr on string→C# switch expr; enum.name()→ToString(); method ref→lambda; currentTimeMillis→UtcNow.ToUnixTimeMilliseconds. Base members + CronService/SpawnEngine/services/SM_* red-tolerated.</summary>
public class PvpMapHandler : GeneralInstanceHandler
{
    private const int SHUGO_SPAWN_RATE = 30;
    private static readonly int[] RANDOM_BOSS_NPC_IDS = { 231196, 233740, 235759, 235765, 235763, 235767, 235771, 235619, 235620, 235621, 855822, 855843,
        230857, 230858, 297189, 855776, 219933, 219934, 235975, 855263, 231304 };
    private readonly Dictionary<int, WorldPosition> origins = new Dictionary<int, WorldPosition>();
    private readonly Dictionary<int, long> joinOrLeaveTime = new Dictionary<int, long>();
    private readonly Dictionary<Race, List<WorldPosition>> respawnLocations = new Dictionary<Race, List<WorldPosition>>();
    private readonly List<WorldPosition> treasurePositions = new List<WorldPosition>();
    private readonly List<WorldPosition> supplyPositions = new List<WorldPosition>();
    private readonly List<WorldPosition> keymasterPositions = new List<WorldPosition>();
    private readonly List<ScheduledTask> tasks = new List<ScheduledTask>();
    private ScheduledTask supplyTask, despawnTask;
    private int currentRandomBossObjId;

    public PvpMapHandler(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        StaticDoorSpawnManager.SpawnTemplate(instance);
        instance.ForEachDoor(door => door.SetOpen(true));
        AddRespawnLocations();
        StartSupplyTask();
        SpawnKeymasters();
        SpawnTreasureChests();
        SpawnNpcs();
        StartRandomBossTask();
    }

    private void SpawnShugo(Player player)
    {
        if (CustomConfig.PVP_MAP_ENABLED && Rnd.Chance() < SHUGO_SPAWN_RATE)
        {
            DeleteAliveNpcs(833543);
            double radian = PositionUtil.ConvertHeadingToAngle(player.GetHeading()) * Math.PI / 180;
            float x = player.GetX() + (float)(Math.Cos(radian) * 2);
            float y = player.GetY() + (float)(Math.Sin(radian) * 2);
            float z = GeoService.GetInstance().GetZ(player.GetWorldId(), x, y, player.GetZ(), instance.GetInstanceId());
            if (float.IsNaN(z))
                z = player.GetZ() + 0.5f;
            byte heading = PositionUtil.GetHeadingTowards(x, y, player.GetX(), player.GetY());
            SpawnTemplate template = SpawnEngine.NewSingleTimeSpawn(mapId, 833543, x, y, z, heading, null, "customcdreset");
            SpawnEngine.SpawnObject(template, instance.GetInstanceId());
        }
    }

    // spawns a supply chest every ~6min if there are enough players on the map
    private void StartSupplyTask()
    {
        supplyTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct => { ScheduleSupplySpawn(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(120000), TimeSpan.FromMilliseconds(400000));
    }

    private void ScheduleSupplySpawn()
    {
        if (SpawnAllowed())
        {
            if (supplyPositions.Count == 0)
            {
                AddSupplyPositions();
            }
            WorldPosition pos = Rnd.Get(supplyPositions);
            supplyPositions.Remove(pos);
            Spawn(831980, pos.GetX(), pos.GetY(), pos.GetZ(), pos.GetHeading()); // flag
            SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_GUARDLIGHTHERO_SPAWN_IDLDF5_UNDER_01_WAR(), 0);
            ScheduleSupplyDespawn();
            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                Spawn(233192, pos.GetX(), pos.GetY(), pos.GetZ(), pos.GetHeading()); // chest
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(30000));
        }
    }

    private void SpawnKeymasters()
    {
        AddKeymasterPositions();
        int[] npcIds = { 219218, 219218, 219218, 219191, 219191, 219192, 219192, 219193 };
        foreach (int id in npcIds)
        {
            SpawnKeymasterOrTreasureChest(id, true);
        }
    }

    private void SpawnKeymasterOrTreasureChest(int npcId, bool isKeymaster)
    {
        WorldPosition pos;
        if (isKeymaster)
        {
            pos = Rnd.Get(keymasterPositions);
            keymasterPositions.Remove(pos);
        }
        else
        {
            pos = Rnd.Get(treasurePositions);
            treasurePositions.Remove(pos);
        }
        Spawn(npcId, pos.GetX(), pos.GetY(), pos.GetZ(), pos.GetHeading());
    }

    private void ScheduleRespawn(int npcId, int time, bool isKeymaster)
    {
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(ct => { SpawnKeymasterOrTreasureChest(npcId, isKeymaster); return ValueTask.CompletedTask; }, TimeSpan.FromMinutes(time)));
    }

    private void SpawnTreasureChests()
    {
        AddTreasurePositions();
        int[] npcIds = { 701388, 701388, 701388, 701388, 701388, 701389, 701389, 701389, 701390, 701390 };
        foreach (int id in npcIds)
        {
            SpawnKeymasterOrTreasureChest(id, false);
        }
    }

    private void StartRandomBossTask()
    {
        CronService.GetInstance().Schedule(() =>
        {
            if (!CustomConfig.PVP_MAP_ENABLED)
                return;
            int bonus = World.GetInstance().GetAllPlayers().Count * 2;
            bonus = Math.Min(bonus, 30);
            if (Rnd.Chance() < (CustomConfig.PVP_MAP_RANDOM_BOSS_BASE_RATE + bonus))
            {
                int npcId = Rnd.Get(RANDOM_BOSS_NPC_IDS);
                NpcTemplate template = DataManager.NPC_DATA.GetNpcTemplate(npcId);
                SpawnTemplate spawn = SpawnEngine.NewSingleTimeSpawn(mapId, npcId, 744.337f, 292.986f, 233.697f, (byte)43, null,
                    "modified_iron_wall_aggressive");
                Npc npc = new Npc(new NpcController(), spawn, template);
                npc.SetKnownlist(new NpcKnownList(npc));
                npc.SetEffectController(new EffectController(npc));
                SpawnEngine.BringIntoWorld(npc, mapId, instance.GetInstanceId(), spawn.GetX(), spawn.GetY(), spawn.GetZ(), spawn.GetHeading());
                currentRandomBossObjId = npc.GetObjectId();
                ScheduleRandomBossDespawn();
                World.GetInstance().ForEachPlayer(p => PvpMapService.GetInstance().NotifyBossSpawn(p));
            }
        }, CustomConfig.PVP_MAP_RANDOM_BOSS_SCHEDULE);
    }

    private void ScheduleRandomBossDespawn()
    {
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            Npc boss = (Npc)instance.GetObject(currentRandomBossObjId);
            if (boss != null && !boss.GetLifeStats().IsAboutToDie() && !boss.IsDead())
            {
                boss.GetController().Delete();
                currentRandomBossObjId = 0;
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMinutes(50)));
    }

    private void ScheduleSupplyDespawn()
    {
        despawnTask = ThreadPoolManager.GetInstance().Schedule(ct => { DeleteAliveNpcs(831980, 233192); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(120000));
    }

    public void Join(Player p)
    {
        if (CanJoin(p))
        {
            StartTeleportation(p, false);
        }
    }

    public void Leave(Player p)
    {
        if (!CheckState(p) || p.GetController().HasScheduledTask(TaskId.SKILL_USE))
        {
            PacketSendUtility.SendMessage(p, "You cannot leave the PvP-Map in your current state.");
            return;
        }
        StartTeleportation(p, true);
    }

    private void StartTeleportation(Player p, bool isLeaving)
    {
        ActionObserver observer = GetAllObserver(p);
        PacketSendUtility.BroadcastPacket(p, new SM_BIND_POINT_TELEPORT(1, p.GetObjectId(), 1, 0), true);
        p.GetObserveController().Attach(observer);

        p.GetController().AddTask(TaskId.SKILL_USE, ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            PacketSendUtility.BroadcastPacket(p, new SM_BIND_POINT_TELEPORT(3, p.GetObjectId(), 1, 0), true);
            ThreadPoolManager.GetInstance().Schedule(ct2 =>
            {
                p.GetObserveController().RemoveObserver(observer);
                p.GetController().CancelTask(TaskId.SKILL_USE);
                if (!p.GetController().IsInCombat() && !p.GetLifeStats().IsAboutToDie() && !p.IsDead())
                {
                    if (isLeaving)
                    {
                        RemovePlayer(p);
                    }
                    else
                    {
                        UpdateOrigin(p);
                        UpdateJoinOrLeaveTime(p);
                        instance.Register(p.GetObjectId());
                        WorldPosition pos = Rnd.Get(respawnLocations[p.GetRace()]);
                        TeleportService.TeleportTo(p, instance, pos.GetX(), pos.GetY(), pos.GetZ(), pos.GetHeading(), TeleportAnimation.BATTLEGROUND);
                    }
                }
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(1000));
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(10000)));
    }

    private ActionObserver GetAllObserver(Player p)
    {
        return new PvpMapTeleportObserver(p);
    }

    // Java parity: anonymous ItemUseObserver subclass overriding abort().
    private sealed class PvpMapTeleportObserver : ItemUseObserver
    {
        private readonly Player p;

        public PvpMapTeleportObserver(Player p)
        {
            this.p = p;
        }

        public override void Abort()
        {
            BindPointTeleportService.CancelTeleport(p, 1);
        }
    }

    private bool CanJoin(Player p)
    {
        if (!p.IsStaff())
        {
            if (!CustomConfig.PVP_MAP_ENABLED)
            {
                PacketSendUtility.SendMessage(p, "The PvP-Map is currently disabled.");
                return false;
            }
            else if (p.GetLevel() < 60)
            {
                PacketSendUtility.SendMessage(p, "The PvP-Map is for players level 60 and above.");
                return false;
            }
            else if (p.IsInInstance() || p.GetWorldId() == 400030000)
            {
                PacketSendUtility.SendMessage(p, "You cannot enter the PvP-Map while in an instance.");
                return false;
            }
            else if (p.GetController().IsInCombat())
            {
                PacketSendUtility.SendMessage(p, "You cannot enter the PvP-Map while in combat.");
                return false;
            }
            else if (!CheckState(p) || p.GetController().HasScheduledTask(TaskId.SKILL_USE))
            {
                PacketSendUtility.SendMessage(p, "You cannot enter the PvP-Map in your current state.");
                return false;
            }
            else if (joinOrLeaveTime.ContainsKey(p.GetObjectId()) && ((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - joinOrLeaveTime[p.GetObjectId()]) < 120000))
            {
                int timeInSeconds = (int)Math.Ceiling((120000 - (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - joinOrLeaveTime[p.GetObjectId()])) / 1000f);
                PacketSendUtility.SendMessage(p, "You can reenter the PvP-Map in " + timeInSeconds + " second" + (timeInSeconds > 1 ? "s." : "."));
                return false;
            }
        }
        return true;
    }

    private bool CheckState(Player p)
    {
        return !p.GetController().IsInCombat() && !p.GetLifeStats().IsAboutToDie() && !p.IsDead() && !p.IsLooting() && !p.IsInGlidingState()
            && !p.IsFlying() && !p.IsUsingFlightTransporterOrWindstream() && !p.IsInPlayerMode(PlayerMode.RIDE) && !p.HasStore()
            && p.GetCastingSkill() == null && !p.GetEffectController().IsInAnyAbnormalState(AbnormalState.CANT_ATTACK_STATE)
            && !p.GetEffectController().IsInAnyAbnormalState(AbnormalState.ROOT);
    }

    private void UpdateOrigin(Player p)
    {
        lock (this)
        {
            origins[p.GetObjectId()] = p.GetPosition();
        }
    }

    private void UpdateJoinOrLeaveTime(Player p)
    {
        lock (this)
        {
            if (!p.IsStaff())
                joinOrLeaveTime[p.GetObjectId()] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    public override bool OnReviveEvent(Player player)
    {
        Revive(player);
        if (!CustomConfig.PVP_MAP_ENABLED || respawnLocations.Count == 0)
        {
            if (instance.GetPlayer(player.GetObjectId()) != null)
            {
                RemovePlayer(player);
            }
        }
        else
        {
            WorldPosition pos = Rnd.Get(respawnLocations[player.GetRace()]);
            TeleportService.TeleportTo(player, instance, pos.GetX(), pos.GetY(), pos.GetZ(), pos.GetHeading(), TeleportAnimation.BATTLEGROUND);
        }
        return true;
    }

    public override bool OnDie(Player player, Creature lastAttacker)
    {
        if (CustomConfig.PVP_MAP_ENABLED)
        {
            if (lastAttacker is Player && !lastAttacker.Equals(player))
            {
                SpawnShugo((Player)lastAttacker);
            }
            PvpService.GetInstance().DoReward(player, CustomConfig.PVP_MAP_AP_MULTIPLIER);
            AnnounceDeath(player);
        }
        return true;
    }

    public override void OnDie(Npc npc)
    {
        if (npc.GetObjectId() == currentRandomBossObjId)
        {
            currentRandomBossObjId = 0;
            return;
        }
        switch (npc.GetNpcId())
        {
            case 219218: // keymaster chookuri
                keymasterPositions
                    .Add(new WorldPosition(mapId, npc.GetSpawn().GetX(), npc.GetSpawn().GetY(), npc.GetSpawn().GetZ(), npc.GetSpawn().GetHeading()));
                ScheduleRespawn(npc.GetNpcId(), Rnd.Get(10, 15), true);
                break;
            case 219191: // keymaster zumita
            case 219192: // keymaster niksi
                keymasterPositions
                    .Add(new WorldPosition(mapId, npc.GetSpawn().GetX(), npc.GetSpawn().GetY(), npc.GetSpawn().GetZ(), npc.GetSpawn().GetHeading()));
                ScheduleRespawn(npc.GetNpcId(), Rnd.Get(30, 50), true);
                break;
            case 219193: // keymaster dabra
                keymasterPositions
                    .Add(new WorldPosition(mapId, npc.GetSpawn().GetX(), npc.GetSpawn().GetY(), npc.GetSpawn().GetZ(), npc.GetSpawn().GetHeading()));
                ScheduleRespawn(npc.GetNpcId(), Rnd.Get(110, 180), true);
                break;
        }
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 701388:
                treasurePositions
                    .Add(new WorldPosition(mapId, npc.GetSpawn().GetX(), npc.GetSpawn().GetY(), npc.GetSpawn().GetZ(), npc.GetSpawn().GetHeading()));
                ScheduleRespawn(npc.GetNpcId(), Rnd.Get(10, 20), false);
                break;
            case 701389:
                treasurePositions
                    .Add(new WorldPosition(mapId, npc.GetSpawn().GetX(), npc.GetSpawn().GetY(), npc.GetSpawn().GetZ(), npc.GetSpawn().GetHeading()));
                ScheduleRespawn(npc.GetNpcId(), Rnd.Get(30, 60), false);
                break;
            case 701390:
                treasurePositions
                    .Add(new WorldPosition(mapId, npc.GetSpawn().GetX(), npc.GetSpawn().GetY(), npc.GetSpawn().GetZ(), npc.GetSpawn().GetHeading()));
                ScheduleRespawn(npc.GetNpcId(), Rnd.Get(120, 200), false);
                break;
        }
    }

    private void AnnounceDeath(Player player)
    {
        if (!player.IsStaff() && player.GetAbyssRank() != null)
        {
            string zoneNameL10n = GetZoneNameL10n(player);
            if (zoneNameL10n != null)
                PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_ABYSS_ORDER_RANKER_DIE(player, zoneNameL10n));
            else
                PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_ABYSS_ORDER_RANKER_DIE(player));
        }
    }

    public override void OnEnterInstance(Player player)
    {
        if (!player.IsStaff())
        {
            UpdateJoinOrLeaveTime(player);
            instance.ForEachPlayer(p =>
            {
                if (!p.Equals(player))
                    PacketSendUtility.SendMessage(p, "A new player has joined!", ChatType.BRIGHT_YELLOW_CENTER);
            });
            PacketSendUtility.BroadcastToWorld(new SM_MESSAGE(0, null, "An enemy has entered the PvP-Map!", ChatType.BRIGHT_YELLOW_CENTER),
                p => p.GetLevel() >= 60 && !p.IsInInstance() && p.GetRace() != player.GetRace());
        }
    }

    public override void OnLeaveInstance(Player player)
    {
        base.OnLeaveInstance(player);
        UpdateJoinOrLeaveTime(player);
    }

    public override void OnPlayerLogout(Player player)
    {
        RemovePlayer(player);
    }

    public override void OnInstanceDestroy()
    {
        PvpMapService.GetInstance().OnInstanceDestroy();
        CancelTasks();
    }

    private void CancelTasks()
    {
        if (supplyTask != null && !supplyTask.IsCancelled)
        {
            supplyTask.Cancel();
        }
        if (despawnTask != null && !despawnTask.IsCancelled)
        {
            despawnTask.Cancel();
        }
        foreach (ScheduledTask task in tasks.Where(task => task != null && !task.IsCancelled))
            task.Cancel();
    }

    private bool SpawnAllowed()
    {
        if (!CustomConfig.PVP_MAP_ENABLED)
            return false;
        byte asmodians = 0;
        byte elyos = 0;
        foreach (Player player in instance.GetPlayersInside())
        {
            if (player.IsStaff())
            {
                continue;
            }
            else if (player.GetRace() == Race.ASMODIANS)
            {
                asmodians++;
            }
            else
            {
                elyos++;
            }
            if (asmodians > 1 && elyos > 1)
            {
                return true;
            }
        }
        return false;
    }

    public int GetParticipantsSize()
    {
        int playerCount = 0;
        foreach (Player p in instance.GetPlayersInside())
        {
            if (!p.IsStaff())
            {
                playerCount++;
            }
        }
        return playerCount;
    }

    private void RemovePlayer(Player p)
    {
        lock (this)
        {
            UpdateJoinOrLeaveTime(p);
            if (p.IsDead())
                Revive(p);
            origins.Remove(p.GetObjectId(), out WorldPosition position);
            if (position != null && !IsAtVulnerableFortress(position.GetMapId(), position.GetX(), position.GetY(), position.GetZ()))
            {
                TeleportService.TeleportTo(p, position);
            }
            else
            {
                TeleportService.MoveToBindLocation(p);
            }
        }
    }

    private void Revive(Player player)
    {
        PlayerReviveService.Revive(player, 100, 100, false, 0);
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME());
        player.GetGameStats().UpdateStatsAndSpeedVisually();
        player.UnsetResPosState();
    }

    public bool IsAtVulnerableFortress(int worldId, float x, float y, float z)
    {
        FortressLocation fortress = SiegeService.GetInstance().FindFortress(worldId, x, y, z);
        return fortress != null && fortress.IsVulnerable();
    }

    public bool IsOnMap(Creature creature)
    {
        return instance != null && instance.GetObject(creature.GetObjectId()) != null;
    }

    public bool IsRandomBoss(int objectId)
    {
        return currentRandomBossObjId == objectId;
    }

    public bool IsRandomBossAlive()
    {
        Npc boss = (Npc)instance.GetObject(currentRandomBossObjId);
        return boss != null && !boss.IsDead();
    }

    private void SpawnNpcs()
    {
        SpawnAndSetRespawn(218544, 739.08954f, 743.7691f, 194.63808f, (byte)69, 295);
        SpawnAndSetRespawn(218544, 790.8744f, 522.2378f, 228.78705f, (byte)90, 295);
        SpawnAndSetRespawn(218544, 823.72784f, 548.57434f, 235.62047f, (byte)77, 295);
        SpawnAndSetRespawn(218544, 793.6772f, 596.01904f, 240.03558f, (byte)8, 295);
        SpawnAndSetRespawn(218544, 795.2162f, 584.24866f, 239.5659f, (byte)0, 295);
        SpawnAndSetRespawn(218544, 357.8682f, 479.70685f, 237.4225f, (byte)99, 295);
        SpawnAndSetRespawn(218544, 367.3182f, 485.55405f, 237.4225f, (byte)108, 295);
        SpawnAndSetRespawn(218544, 353.6212f, 427.49295f, 233.09781f, (byte)96, 295);
        SpawnAndSetRespawn(218544, 757.8844f, 748.44446f, 195.72215f, (byte)71, 295);
        SpawnAndSetRespawn(218544, 752.23975f, 757.58105f, 195.71138f, (byte)69, 295);
        SpawnAndSetRespawn(218544, 430.81192f, 758.72595f, 203.42834f, (byte)14, 295);
        SpawnAndSetRespawn(218544, 433.25076f, 779.33185f, 203.42834f, (byte)108, 295);
        SpawnAndSetRespawn(218544, 471.17935f, 762.66144f, 201.68672f, (byte)3, 295);
        SpawnAndSetRespawn(218544, 769.0259f, 781.72473f, 198.76245f, (byte)79, 295);
        SpawnAndSetRespawn(218544, 772.4939f, 762.43164f, 198.0455f, (byte)70, 295);
        SpawnAndSetRespawn(218544, 568.91833f, 508.94455f, 217.75f, (byte)118, 295);
        SpawnAndSetRespawn(218544, 595.42993f, 504.22293f, 217.66063f, (byte)69, 295);
        SpawnAndSetRespawn(218544, 656.7253f, 219.83476f, 238.48415f, (byte)46, 295);
        SpawnAndSetRespawn(218544, 653.7101f, 370.04272f, 239.61528f, (byte)103, 295);
        SpawnAndSetRespawn(218544, 762.97675f, 386.17636f, 242.0815f, (byte)23, 295);
        SpawnAndSetRespawn(218544, 826.2748f, 351.18307f, 243.75453f, (byte)46, 295);
        SpawnAndSetRespawn(218544, 781.19916f, 330.00568f, 253.43387f, (byte)76, 295);
        SpawnAndSetRespawn(218544, 698.00366f, 262.089f, 253.43388f, (byte)15, 295);
        SpawnAndSetRespawn(218544, 644.2379f, 408.7981f, 242.47498f, (byte)103, 295);
        SpawnAndSetRespawn(218544, 644.08527f, 290.91754f, 225.69778f, (byte)9, 295);
        SpawnAndSetRespawn(218544, 549.149f, 424.63623f, 222.66476f, (byte)18, 295);
        SpawnAndSetRespawn(218544, 655.04407f, 242.13554f, 225.69778f, (byte)22, 295);
        SpawnAndSetRespawn(218544, 457.68298f, 276.2121f, 246.71693f, (byte)75, 295);
        SpawnAndSetRespawn(218544, 684.6678f, 308.9507f, 225.69778f, (byte)61, 295);
        SpawnAndSetRespawn(218544, 449.93518f, 284.11102f, 245.73611f, (byte)26, 295);
        SpawnAndSetRespawn(218544, 401.42535f, 259.03088f, 253.28592f, (byte)20, 295);
        SpawnAndSetRespawn(218544, 423.5366f, 269.79514f, 247.5003f, (byte)97, 295);
        SpawnAndSetRespawn(218544, 798.90045f, 366.7033f, 230.98207f, (byte)80, 295);
        SpawnAndSetRespawn(218544, 438.80832f, 618.10834f, 214.52452f, (byte)42, 295);
        SpawnAndSetRespawn(218544, 429.44916f, 614.982f, 214.52452f, (byte)40, 295);
        SpawnAndSetRespawn(218544, 422.44595f, 647.92f, 214.52452f, (byte)95, 295);
        SpawnAndSetRespawn(218544, 576.42346f, 716.5756f, 205.78198f, (byte)54, 295);
        SpawnAndSetRespawn(218544, 317.8458f, 684.38007f, 212.99036f, (byte)67, 295);
        SpawnAndSetRespawn(218544, 309.91922f, 682.13055f, 212.75505f, (byte)6, 295);
        SpawnAndSetRespawn(218544, 239.23927f, 757.8234f, 201.60623f, (byte)113, 295);
        SpawnAndSetRespawn(218544, 252.59685f, 769.42523f, 201.95093f, (byte)93, 295);
        SpawnAndSetRespawn(218544, 546.30115f, 686.9413f, 205.5f, (byte)46, 295);
        SpawnAndSetRespawn(218544, 603.9977f, 876.9201f, 192.91196f, (byte)89, 295);
        SpawnAndSetRespawn(218544, 603.13055f, 864.87823f, 192.65533f, (byte)89, 295);
        SpawnAndSetRespawn(218544, 690.58f, 759.3537f, 182.375f, (byte)55, 295);
        SpawnAndSetRespawn(218544, 685.9591f, 746.5975f, 182.375f, (byte)55, 295);
        SpawnAndSetRespawn(218544, 721.9566f, 723.7783f, 189.375f, (byte)67, 295);
        SpawnAndSetRespawn(218544, 694.7766f, 730.0601f, 188.96326f, (byte)0, 295);
        SpawnAndSetRespawn(218544, 714.0073f, 739.8774f, 189.24994f, (byte)75, 295);
        SpawnAndSetRespawn(218544, 717.8495f, 731.7268f, 189.3656f, (byte)70, 295);
        SpawnAndSetRespawn(218547, 757.2226f, 709.5766f, 194.62617f, (byte)43, 295);
        SpawnAndSetRespawn(218547, 815.31647f, 537.01685f, 229.99895f, (byte)70, 295);
        SpawnAndSetRespawn(218547, 808.0359f, 568.60266f, 239.5f, (byte)25, 295);
        SpawnAndSetRespawn(218547, 813.6234f, 583.6792f, 239.39505f, (byte)115, 295);
        SpawnAndSetRespawn(218547, 340.38742f, 444.74323f, 234.125f, (byte)10, 295);
        SpawnAndSetRespawn(218547, 455.97104f, 778.36456f, 202.01328f, (byte)96, 295);
        SpawnAndSetRespawn(218547, 517.13416f, 764.3607f, 195.46646f, (byte)118, 295);
        SpawnAndSetRespawn(218547, 727.2865f, 779.1507f, 194.5f, (byte)108, 295);
        SpawnAndSetRespawn(218547, 607.4919f, 490.93564f, 217.90976f, (byte)46, 295);
        SpawnAndSetRespawn(218547, 607.90454f, 508.69162f, 218.125f, (byte)68, 295);
        SpawnAndSetRespawn(218547, 607.9011f, 520.136f, 217.875f, (byte)14, 295);
        SpawnAndSetRespawn(218547, 597.78455f, 480.55164f, 218.0f, (byte)98, 295);
        SpawnAndSetRespawn(218547, 633.9897f, 228.40494f, 238.07529f, (byte)22, 295);
        SpawnAndSetRespawn(218547, 620.4969f, 311.71567f, 236.74612f, (byte)114, 295);
        SpawnAndSetRespawn(218547, 640.37787f, 304.753f, 236.92535f, (byte)54, 295);
        SpawnAndSetRespawn(218547, 740.5545f, 388.09833f, 242.1149f, (byte)33, 295);
        SpawnAndSetRespawn(218547, 796.8819f, 396.5597f, 242.01622f, (byte)83, 295);
        SpawnAndSetRespawn(218547, 748.0393f, 322.6207f, 249.28568f, (byte)43, 295);
        SpawnAndSetRespawn(218547, 714.6477f, 285.88177f, 249.28185f, (byte)42, 295);
        SpawnAndSetRespawn(218547, 547.2966f, 433.86728f, 222.75f, (byte)112, 295);
        SpawnAndSetRespawn(218547, 638.9781f, 426.24887f, 242.47498f, (byte)90, 295);
        SpawnAndSetRespawn(218547, 673.12494f, 221.4442f, 225.69778f, (byte)33, 295);
        SpawnAndSetRespawn(218547, 461.76224f, 266.64352f, 246.5f, (byte)60, 295);
        SpawnAndSetRespawn(218547, 737.5307f, 352.08997f, 230.94298f, (byte)43, 295);
        SpawnAndSetRespawn(218547, 396.4398f, 282.85092f, 253.6672f, (byte)85, 295);
        SpawnAndSetRespawn(218547, 445.27808f, 268.4044f, 246.47473f, (byte)79, 295);
        SpawnAndSetRespawn(218547, 241.67007f, 743.7223f, 201.54707f, (byte)22, 295);
        SpawnAndSetRespawn(218547, 274.27524f, 735.2498f, 205.57104f, (byte)22, 295);
        SpawnAndSetRespawn(218547, 535.81146f, 693.7665f, 205.38959f, (byte)3, 295);
        SpawnAndSetRespawn(218547, 594.31903f, 854.5406f, 192.18222f, (byte)101, 295);
        SpawnAndSetRespawn(218547, 696.12354f, 705.2198f, 194.81421f, (byte)8, 295);
        SpawnAndSetRespawn(219189, 671.8091f, 737.7017f, 178.73135f, (byte)43, 295);
        SpawnAndSetRespawn(219189, 593.70734f, 547.63635f, 219.09225f, (byte)7, 295);
        SpawnAndSetRespawn(219189, 565.2322f, 812.2663f, 188.3649f, (byte)105, 295);
        SpawnAndSetRespawn(219189, 622.64264f, 825.0713f, 188.46423f, (byte)69, 295);
        SpawnAndSetRespawn(219166, 588.5865f, 546.58716f, 219.3217f, (byte)68, 295);
        SpawnAndSetRespawn(219190, 808.65955f, 464.9168f, 228.91623f, (byte)48, 295);
        SpawnAndSetRespawn(219190, 648.5453f, 381.37613f, 228.625f, (byte)60, 295);
        SpawnAndSetRespawn(219190, 669.57f, 402.74194f, 228.61024f, (byte)32, 295);
        SpawnAndSetRespawn(219190, 476.58044f, 681.6464f, 217.96188f, (byte)38, 295);
        SpawnAndSetRespawn(219195, 810.7329f, 453.15427f, 228.75f, (byte)81, 295);
        SpawnAndSetRespawn(219195, 725.1208f, 547.77325f, 233.37512f, (byte)70, 295);
        SpawnAndSetRespawn(219195, 458.33813f, 530.78235f, 222.37468f, (byte)49, 295);
        SpawnAndSetRespawn(219195, 706.0179f, 490.12515f, 234.89288f, (byte)100, 295);
        SpawnAndSetRespawn(219195, 346.1327f, 655.7598f, 219.9216f, (byte)52, 295);
        SpawnAndSetRespawn(219195, 669.57355f, 774.0344f, 181.60016f, (byte)93, 295);
        SpawnAndSetRespawn(218545, 808.67224f, 525.32306f, 230.087f, (byte)67, 295);
        SpawnAndSetRespawn(218545, 825.1957f, 561.02435f, 239.06339f, (byte)82, 295);
        SpawnAndSetRespawn(218545, 799.7665f, 572.0052f, 239.54282f, (byte)20, 295);
        SpawnAndSetRespawn(218545, 809.45435f, 597.70325f, 239.5659f, (byte)88, 295);
        SpawnAndSetRespawn(218545, 820.66394f, 592.6436f, 239.41522f, (byte)79, 295);
        SpawnAndSetRespawn(218545, 796.40765f, 606.99194f, 239.88962f, (byte)103, 295);
        SpawnAndSetRespawn(218545, 662.76245f, 547.9632f, 238.60799f, (byte)93, 295);
        SpawnAndSetRespawn(218545, 372.4097f, 442.73737f, 234.19669f, (byte)65, 295);
        SpawnAndSetRespawn(218545, 348.93497f, 491.24512f, 239.26987f, (byte)107, 295);
        SpawnAndSetRespawn(218545, 732.9599f, 754.0701f, 194.79166f, (byte)100, 295);
        SpawnAndSetRespawn(218545, 403.51105f, 491.78015f, 233.7788f, (byte)12, 295);
        SpawnAndSetRespawn(218545, 745.4872f, 733.6486f, 194.79395f, (byte)40, 295);
        SpawnAndSetRespawn(218545, 447.26068f, 752.36f, 202.51537f, (byte)21, 295);
        SpawnAndSetRespawn(218545, 481.4032f, 769.2115f, 201.59654f, (byte)113, 295);
        SpawnAndSetRespawn(218545, 771.4963f, 720.42f, 194.5f, (byte)53, 295);
        SpawnAndSetRespawn(218545, 776.93744f, 742.47125f, 195.54028f, (byte)80, 295);
        SpawnAndSetRespawn(218545, 755.26324f, 776.8493f, 195.54028f, (byte)86, 295);
        SpawnAndSetRespawn(218545, 691.7244f, 638.8437f, 203.85924f, (byte)33, 295);
        SpawnAndSetRespawn(218545, 710.84985f, 672.1933f, 206.10843f, (byte)49, 295);
        SpawnAndSetRespawn(218545, 609.9847f, 498.69614f, 218.12404f, (byte)61, 295);
        SpawnAndSetRespawn(218545, 596.21906f, 522.6787f, 217.75f, (byte)81, 295);
        SpawnAndSetRespawn(218545, 621.87305f, 248.52107f, 236.5246f, (byte)3, 295);
        SpawnAndSetRespawn(218545, 643.08826f, 252.42151f, 236.9016f, (byte)64, 295);
        SpawnAndSetRespawn(218545, 737.86536f, 408.9785f, 242.11967f, (byte)93, 295);
        SpawnAndSetRespawn(218545, 789.3389f, 376.88342f, 242.2093f, (byte)23, 295);
        SpawnAndSetRespawn(218545, 788.5563f, 309.24756f, 253.43407f, (byte)53, 295);
        SpawnAndSetRespawn(218545, 713.9743f, 317.5257f, 252.13892f, (byte)16, 295);
        SpawnAndSetRespawn(218545, 721.78467f, 324.17868f, 252.13892f, (byte)73, 295);
        SpawnAndSetRespawn(218545, 724.86456f, 248.67912f, 253.43427f, (byte)34, 295);
        SpawnAndSetRespawn(218545, 566.43604f, 414.4239f, 222.67874f, (byte)35, 295);
        SpawnAndSetRespawn(218545, 788.57983f, 572.7258f, 239.375f, (byte)104, 295);
        SpawnAndSetRespawn(218545, 682.3254f, 272.88013f, 225.69778f, (byte)49, 295);
        SpawnAndSetRespawn(218545, 558.0802f, 435.9432f, 222.75f, (byte)98, 295);
        SpawnAndSetRespawn(218545, 692.28894f, 351.46298f, 228.7785f, (byte)103, 295);
        SpawnAndSetRespawn(218545, 455.10605f, 258.98337f, 246.5f, (byte)43, 295);
        SpawnAndSetRespawn(218545, 385.7541f, 270.29462f, 253.5f, (byte)7, 295);
        SpawnAndSetRespawn(218545, 815.5708f, 350.05713f, 230.98207f, (byte)73, 295);
        SpawnAndSetRespawn(218545, 446.28192f, 270.5887f, 246.46321f, (byte)32, 295);
        SpawnAndSetRespawn(218545, 313.05374f, 623.97473f, 230.95587f, (byte)47, 295);
        SpawnAndSetRespawn(218545, 283.17975f, 754.6061f, 203.93437f, (byte)79, 295);
        SpawnAndSetRespawn(218545, 533.2114f, 704.00757f, 205.625f, (byte)7, 295);
        SpawnAndSetRespawn(218545, 582.3275f, 696.94165f, 210.41594f, (byte)72, 295);
        SpawnAndSetRespawn(218545, 619.1518f, 878.12915f, 193.24794f, (byte)69, 295);
        SpawnAndSetRespawn(218545, 697.3136f, 750.4479f, 185.59483f, (byte)53, 295);
        SpawnAndSetRespawn(218545, 726.8993f, 742.47095f, 193.55424f, (byte)69, 295);
        SpawnAndSetRespawn(218545, 731.63965f, 733.70996f, 193.5039f, (byte)69, 295);
        SpawnAndSetRespawn(218549, 795.813f, 541.2819f, 229.90819f, (byte)100, 295);
        SpawnAndSetRespawn(218549, 795.4988f, 557.5386f, 240.98943f, (byte)11, 295);
        SpawnAndSetRespawn(218549, 808.0439f, 611.9442f, 239.5659f, (byte)81, 295);
        SpawnAndSetRespawn(218549, 833.02f, 599.4704f, 239.46935f, (byte)86, 295);
        SpawnAndSetRespawn(218549, 381.9712f, 458.69794f, 235.71736f, (byte)49, 295);
        SpawnAndSetRespawn(218549, 445.28845f, 785.9884f, 202.92975f, (byte)84, 295);
        SpawnAndSetRespawn(218549, 737.7692f, 793.7346f, 195.6785f, (byte)85, 295);
        SpawnAndSetRespawn(218549, 450.3595f, 767.3717f, 202.3945f, (byte)107, 295);
        SpawnAndSetRespawn(218549, 497.7351f, 762.2599f, 200.00084f, (byte)11, 295);
        SpawnAndSetRespawn(218549, 805.90955f, 737.58636f, 196.34206f, (byte)66, 295);
        SpawnAndSetRespawn(218549, 572.9683f, 490.3728f, 217.75f, (byte)113, 295);
        SpawnAndSetRespawn(218549, 584.0002f, 499.08987f, 217.75002f, (byte)107, 295);
        SpawnAndSetRespawn(218549, 630.12134f, 338.17273f, 236.74385f, (byte)111, 295);
        SpawnAndSetRespawn(218549, 710.1343f, 405.6931f, 241.98926f, (byte)92, 295);
        SpawnAndSetRespawn(218549, 809.4866f, 361.95856f, 241.92001f, (byte)19, 295);
        SpawnAndSetRespawn(218549, 777.9167f, 287.46475f, 253.43427f, (byte)65, 295);
        SpawnAndSetRespawn(218549, 576.3781f, 400.8725f, 222.4382f, (byte)116, 295);
        SpawnAndSetRespawn(218549, 741.08673f, 257.78488f, 253.4343f, (byte)27, 295);
        SpawnAndSetRespawn(218549, 685.96643f, 344.79675f, 245.26868f, (byte)14, 295);
        SpawnAndSetRespawn(218549, 701.34973f, 357.53714f, 245.2944f, (byte)73, 295);
        SpawnAndSetRespawn(218549, 692.49194f, 243.17621f, 227.16022f, (byte)61, 295);
        SpawnAndSetRespawn(218549, 643.6444f, 268.54294f, 225.69778f, (byte)5, 295);
        SpawnAndSetRespawn(218549, 560.1584f, 423.3512f, 222.625f, (byte)17, 295);
        SpawnAndSetRespawn(218549, 435.71008f, 255.26958f, 246.375f, (byte)24, 295);
        SpawnAndSetRespawn(218549, 702.438f, 373.9413f, 228.674f, (byte)115, 295);
        SpawnAndSetRespawn(218549, 805.08105f, 323.8884f, 230.98207f, (byte)32, 295);
        SpawnAndSetRespawn(218549, 556.52484f, 707.8591f, 206.58656f, (byte)41, 295);
        SpawnAndSetRespawn(218549, 566.68835f, 675.889f, 211.4778f, (byte)33, 295);
        SpawnAndSetRespawn(218549, 595.42f, 832.61285f, 188.6633f, (byte)93, 295);
        SpawnAndSetRespawn(218549, 610.9518f, 854.17535f, 192.2f, (byte)75, 295);
        SpawnAndSetRespawn(218549, 605.73267f, 832.3487f, 188.59225f, (byte)94, 295);
        SpawnAndSetRespawn(218549, 706.22314f, 756.8924f, 188.98705f, (byte)76, 295);
        SpawnAndSetRespawn(218549, 733.72406f, 719.50574f, 194.5f, (byte)25, 295);
        SpawnAndSetRespawn(218549, 717.5115f, 753.83606f, 194.45918f, (byte)108, 295);
        SpawnAndSetRespawn(218546, 807.58453f, 549.3488f, 238.99246f, (byte)24, 295);
        SpawnAndSetRespawn(218546, 831.0175f, 575.88043f, 239.375f, (byte)55, 295);
        SpawnAndSetRespawn(218546, 834.5515f, 587.1165f, 239.47925f, (byte)66, 295);
        SpawnAndSetRespawn(218546, 346.86578f, 460.7019f, 235.10414f, (byte)106, 295);
        SpawnAndSetRespawn(218546, 440.78027f, 531.0118f, 223.15675f, (byte)0, 295);
        SpawnAndSetRespawn(218546, 430.2271f, 769.12067f, 204.05684f, (byte)119, 295);
        SpawnAndSetRespawn(218546, 796.9759f, 722.1847f, 196.7664f, (byte)31, 295);
        SpawnAndSetRespawn(218546, 736.8544f, 768.3472f, 194.5534f, (byte)87, 295);
        SpawnAndSetRespawn(218546, 581.5761f, 481.5813f, 217.75f, (byte)33, 295);
        SpawnAndSetRespawn(218546, 581.79236f, 515.9131f, 217.75f, (byte)98, 295);
        SpawnAndSetRespawn(218546, 617.6701f, 275.5021f, 236.78897f, (byte)4, 295);
        SpawnAndSetRespawn(218546, 639.31323f, 280.07794f, 236.37653f, (byte)63, 295);
        SpawnAndSetRespawn(218546, 681.21075f, 391.91763f, 240.07115f, (byte)104, 295);
        SpawnAndSetRespawn(218546, 770.5945f, 406.5085f, 241.92291f, (byte)82, 295);
        SpawnAndSetRespawn(218546, 734.4841f, 343.54346f, 249.30322f, (byte)87, 295);
        SpawnAndSetRespawn(218546, 694.2494f, 308.95227f, 249.30322f, (byte)1, 295);
        SpawnAndSetRespawn(218546, 569.8869f, 435.66293f, 222.04213f, (byte)17, 295);
        SpawnAndSetRespawn(218546, 684.5185f, 260.11212f, 225.69778f, (byte)76, 295);
        SpawnAndSetRespawn(218546, 706.58984f, 334.94543f, 229.35587f, (byte)44, 295);
        SpawnAndSetRespawn(218546, 437.9635f, 277.951f, 246.375f, (byte)105, 295);
        SpawnAndSetRespawn(218546, 299.42566f, 722.23926f, 205.89642f, (byte)74, 295);
        SpawnAndSetRespawn(218546, 557.1041f, 691.66016f, 205.91748f, (byte)27, 295);
        SpawnAndSetRespawn(218546, 588.75586f, 871.6779f, 192.84047f, (byte)110, 295);
        SpawnAndSetRespawn(218546, 694.68243f, 715.9783f, 193.79282f, (byte)1, 295);
        SpawnAndSetRespawn(218546, 742.9393f, 709.2019f, 194.5f, (byte)22, 295);
        SpawnAndSetRespawn(218920, 670.0766f, 560.6434f, 229.34996f, (byte)97, 295);
        SpawnAndSetRespawn(218920, 685.3019f, 427.8387f, 229.82187f, (byte)2, 295);
        SpawnAndSetRespawn(218920, 757.7039f, 356.35028f, 232.42679f, (byte)14, 295);
        SpawnAndSetRespawn(218920, 618.3982f, 361.9023f, 224.94342f, (byte)65, 295);
        SpawnAndSetRespawn(218920, 517.9982f, 230.86314f, 231.92047f, (byte)117, 295);
        SpawnAndSetRespawn(218548, 467.81332f, 551.91583f, 216.59146f, (byte)47, 295);
        SpawnAndSetRespawn(218548, 489.4462f, 736.5246f, 209.70947f, (byte)4, 295);
        SpawnAndSetRespawn(218548, 266.49475f, 605.3711f, 223.27095f, (byte)7, 295);
        SpawnAndSetRespawn(218548, 576.5212f, 229.43967f, 232.6177f, (byte)19, 295);
        SpawnAndSetRespawn(218548, 703.4563f, 466.252f, 227.25f, (byte)69, 295);
        SpawnAndSetRespawn(218548, 520.92334f, 475.87265f, 216.5186f, (byte)101, 295);
        SpawnAndSetRespawn(218548, 309.666f, 650.7151f, 214.44623f, (byte)31, 295);
        SpawnAndSetRespawn(218548, 483.80945f, 689.0492f, 216.82463f, (byte)39, 295);
        SpawnAndSetRespawn(218548, 530.01953f, 721.6219f, 203.44101f, (byte)38, 295);
        SpawnAndSetRespawn(218548, 584.07904f, 794.46204f, 187.90817f, (byte)61, 295);
        SpawnAndSetRespawn(218548, 634.5419f, 739.6843f, 183.52533f, (byte)19, 295);
        SpawnAndSetRespawn(219167, 306.2718f, 449.41235f, 234.73024f, (byte)58, 295);
        SpawnAndSetRespawn(219167, 406.56827f, 561.6233f, 214.85794f, (byte)24, 295);
        SpawnAndSetRespawn(219167, 470.27045f, 382.566f, 241.05424f, (byte)32, 295);
        SpawnAndSetRespawn(219167, 570.3662f, 364.65964f, 226.03668f, (byte)3, 295);
        SpawnAndSetRespawn(219167, 491.91467f, 675.4431f, 220.99588f, (byte)43, 295);
        SpawnAndSetRespawn(219167, 477.62268f, 668.1604f, 220.99417f, (byte)30, 295);
        SpawnAndSetRespawn(219167, 604.9417f, 803.43274f, 186.88289f, (byte)12, 295);
        SpawnAndSetRespawn(219196, 417.46832f, 312.88907f, 233.34177f, (byte)96, 295);
        SpawnAndSetRespawn(219198, 378.59048f, 365.98883f, 226.03691f, (byte)54, 295);
        SpawnAndSetRespawn(219198, 487.1122f, 663.8798f, 221.72038f, (byte)38, 295);
        SpawnAndSetRespawn(219169, 642.6832f, 562.6061f, 229.95897f, (byte)95, 295);
        SpawnAndSetRespawn(219169, 616.85236f, 237.28638f, 229.79572f, (byte)56, 295);
        SpawnAndSetRespawn(219169, 629.52075f, 426.4336f, 226.93431f, (byte)42, 295);
        SpawnAndSetRespawn(219169, 484.21207f, 411.73413f, 233.46696f, (byte)10, 295);
        SpawnAndSetRespawn(219188, 804.1674f, 406.99316f, 232.52094f, (byte)55, 295);
        SpawnAndSetRespawn(219188, 676.3855f, 489.59888f, 226.12563f, (byte)94, 295);
        SpawnAndSetRespawn(219188, 299.4595f, 699.49084f, 207.92665f, (byte)20, 295);
        SpawnAndSetRespawn(219188, 259.73505f, 750.9975f, 201.32014f, (byte)23, 295);
        SpawnAndSetRespawn(219188, 266.61508f, 748.002f, 201.34828f, (byte)24, 295);
        SpawnAndSetRespawn(219188, 595.1314f, 781.1905f, 186.65916f, (byte)89, 295);
        SpawnAndSetRespawn(218551, 737.00867f, 431.07535f, 230.37608f, (byte)69, 295);
        SpawnAndSetRespawn(218551, 359.74063f, 661.96454f, 217.18471f, (byte)23, 295);
        SpawnAndSetRespawn(219194, 495.03677f, 439.18695f, 223.67601f, (byte)108, 295);
        SpawnAndSetRespawn(219194, 615.3467f, 265.0791f, 226.51422f, (byte)65, 295);
        SpawnAndSetRespawn(219194, 435.5179f, 681.1302f, 214.8404f, (byte)109, 295);
        SpawnAndSetRespawn(219194, 519.0116f, 780.06616f, 194.38846f, (byte)104, 295);
        SpawnAndSetRespawn(218550, 499.67398f, 422.29724f, 226.32646f, (byte)53, 295);
        SpawnAndSetRespawn(218550, 701.68555f, 410.91293f, 231.0f, (byte)43, 295);
        SpawnAndSetRespawn(218550, 647.8726f, 481.46146f, 226.34134f, (byte)103, 295);
        SpawnAndSetRespawn(218550, 451.62155f, 714.1448f, 213.3955f, (byte)5, 295);
        SpawnAndSetRespawn(219187, 581.25275f, 333.01978f, 227.84341f, (byte)7, 295);
        SpawnAndSetRespawn(219187, 438.07648f, 695.8468f, 215.41328f, (byte)109, 295);
        SpawnAndSetRespawn(219197, 591.01575f, 812.62335f, 186.81348f, (byte)37, 295);

        // guardian tower
        SpawnAndSetRespawn(233509, 704.6f, 636.3f, 212.239f, (byte)37, 295); // asmodian
        SpawnAndSetRespawn(233509, 712.0f, 639.2f, 212.271f, (byte)37, 295); // asmodian
        SpawnAndSetRespawn(233509, 589.2f, 699.7f, 220.973f, (byte)73, 295); // asmodian
        SpawnAndSetRespawn(233509, 583.7f, 706.6f, 221.170f, (byte)73, 295); // asmodian

        SpawnAndSetRespawn(233529, 288.5f, 391.0f, 238.445f, (byte)14, 295); // elyos
        SpawnAndSetRespawn(233529, 282.9f, 397.2f, 238.200f, (byte)14, 295); // elyos
        SpawnAndSetRespawn(233529, 330.5f, 626.5f, 247.564f, (byte)45, 295); // elyos
        SpawnAndSetRespawn(233529, 336.0f, 632.0f, 247.601f, (byte)45, 295); // elyos
    }

    private void AddRespawnLocations()
    {
        respawnLocations.Clear();
        respawnLocations[Race.ELYOS] = new List<WorldPosition>();
        respawnLocations[Race.ELYOS].Add(new WorldPosition(mapId, 274.143f, 384.335f, 239.973f, (byte)14));
        respawnLocations[Race.ELYOS].Add(new WorldPosition(mapId, 342.138f, 616.856f, 248.197f, (byte)35));
        respawnLocations[Race.ASMODIANS] = new List<WorldPosition>();
        respawnLocations[Race.ASMODIANS].Add(new WorldPosition(mapId, 598.229f, 712.984f, 223.306f, (byte)73));
        respawnLocations[Race.ASMODIANS].Add(new WorldPosition(mapId, 711.403f, 621.797f, 213.276f, (byte)31));
    }

    private void AddSupplyPositions()
    {
        supplyPositions.Clear();
        supplyPositions.Add(new WorldPosition(mapId, 709.6463f, 313.6129f, 254.21637f, (byte)14));
        supplyPositions.Add(new WorldPosition(mapId, 749.5364f, 330.05954f, 233.81584f, (byte)89));
        supplyPositions.Add(new WorldPosition(mapId, 703.55786f, 292.23004f, 233.81587f, (byte)119));
        supplyPositions.Add(new WorldPosition(mapId, 612.4221f, 274.82172f, 235.73499f, (byte)5));
        supplyPositions.Add(new WorldPosition(mapId, 648.80536f, 253.2089f, 235.73445f, (byte)62));
        supplyPositions.Add(new WorldPosition(mapId, 772.5526f, 411.02084f, 241.0154f, (byte)90));
        supplyPositions.Add(new WorldPosition(mapId, 709.7187f, 411.01987f, 241.01144f, (byte)92));
        supplyPositions.Add(new WorldPosition(mapId, 655.7371f, 530.3507f, 226.47437f, (byte)107));
        supplyPositions.Add(new WorldPosition(mapId, 795.3396f, 532.70593f, 229.58707f, (byte)115));
        supplyPositions.Add(new WorldPosition(mapId, 646.19354f, 212.37506f, 223.40485f, (byte)113));
        supplyPositions.Add(new WorldPosition(mapId, 330.28857f, 390.94733f, 226.0986f, (byte)31));
        supplyPositions.Add(new WorldPosition(mapId, 389.6383f, 625.3936f, 214.52452f, (byte)1));
        supplyPositions.Add(new WorldPosition(mapId, 280.92044f, 645.77905f, 217.54143f, (byte)98));
        supplyPositions.Add(new WorldPosition(mapId, 452.21927f, 515.92175f, 223.16016f, (byte)49));
        supplyPositions.Add(new WorldPosition(mapId, 705.30096f, 654.2082f, 206.6876f, (byte)48));
        supplyPositions.Add(new WorldPosition(mapId, 513.3479f, 462.09848f, 216.95465f, (byte)13));
        supplyPositions.Add(new WorldPosition(mapId, 629.89185f, 860.046f, 190.88751f, (byte)100));
        supplyPositions.Add(new WorldPosition(mapId, 677.7658f, 714.2354f, 178.125f, (byte)43));
        supplyPositions.Add(new WorldPosition(mapId, 493.3107f, 764.61127f, 200.02097f, (byte)2));
        supplyPositions.Add(new WorldPosition(mapId, 726.9365f, 328.25638f, 254.21623f, (byte)73));
        supplyPositions.Add(new WorldPosition(mapId, 640.6047f, 413.0314f, 243.93956f, (byte)103));
    }

    private void AddKeymasterPositions()
    {
        keymasterPositions.Clear();
        keymasterPositions.Add(new WorldPosition(mapId, 354.27307f, 497.8957f, 239.26987f, (byte)95));
        keymasterPositions.Add(new WorldPosition(mapId, 419.7437f, 769.88983f, 205.1365f, (byte)119));
        keymasterPositions.Add(new WorldPosition(mapId, 259.40518f, 736.55725f, 201.33997f, (byte)25));
        keymasterPositions.Add(new WorldPosition(mapId, 793.8396f, 774.8803f, 200.86058f, (byte)70));
        keymasterPositions.Add(new WorldPosition(mapId, 604.94684f, 900.01465f, 195.53622f, (byte)94));
        keymasterPositions.Add(new WorldPosition(mapId, 820.80194f, 606.7696f, 239.70268f, (byte)82));
        keymasterPositions.Add(new WorldPosition(mapId, 552.9182f, 414.02127f, 222.76308f, (byte)19));
        keymasterPositions.Add(new WorldPosition(mapId, 395.32816f, 272.79468f, 253.375f, (byte)108));
        keymasterPositions.Add(new WorldPosition(mapId, 717.7881f, 320.78925f, 233.5026f, (byte)102));
        keymasterPositions.Add(new WorldPosition(mapId, 590.50385f, 506.48993f, 217.75f, (byte)57));
        keymasterPositions.Add(new WorldPosition(mapId, 667.1276f, 278.3438f, 225.69778f, (byte)32));
        keymasterPositions.Add(new WorldPosition(mapId, 817.77606f, 371.4041f, 243.45387f, (byte)48));
        keymasterPositions.Add(new WorldPosition(mapId, 781.83466f, 357.42792f, 230.98207f, (byte)52));
        keymasterPositions.Add(new WorldPosition(mapId, 630.30853f, 263.7316f, 238.48415f, (byte)33));
        keymasterPositions.Add(new WorldPosition(mapId, 490.17584f, 667.2109f, 221.4411f, (byte)61));
        keymasterPositions.Add(new WorldPosition(mapId, 423.2521f, 629.2255f, 214.52452f, (byte)64));
    }

    private void AddTreasurePositions()
    {
        treasurePositions.Clear();
        treasurePositions.Add(new WorldPosition(mapId, 644.363f, 222.8753f, 238.07552f, (byte)20));
        treasurePositions.Add(new WorldPosition(mapId, 822.94727f, 370.6611f, 243.34569f, (byte)73));
        treasurePositions.Add(new WorldPosition(mapId, 565.6456f, 396.60397f, 228.94838f, (byte)26));
        treasurePositions.Add(new WorldPosition(mapId, 778.3336f, 787.82886f, 198.75298f, (byte)83));
        treasurePositions.Add(new WorldPosition(mapId, 599.0367f, 567.8311f, 214.96388f, (byte)25));
        treasurePositions.Add(new WorldPosition(mapId, 614.5359f, 886.3159f, 193.82806f, (byte)78));
        treasurePositions.Add(new WorldPosition(mapId, 436.56647f, 754.596f, 202.92058f, (byte)17));
        treasurePositions.Add(new WorldPosition(mapId, 412.67242f, 645.81995f, 214.52452f, (byte)92));
        treasurePositions.Add(new WorldPosition(mapId, 353.569f, 504.86948f, 239.26987f, (byte)92));
        treasurePositions.Add(new WorldPosition(mapId, 567.02625f, 595.80725f, 209.19331f, (byte)109));
        treasurePositions.Add(new WorldPosition(mapId, 647.5166f, 543.63727f, 222.8279f, (byte)67));
        treasurePositions.Add(new WorldPosition(mapId, 803.04175f, 604.1839f, 239.5659f, (byte)92));
        treasurePositions.Add(new WorldPosition(mapId, 676.91064f, 785.8807f, 181.20055f, (byte)83));
        treasurePositions.Add(new WorldPosition(mapId, 585.99786f, 662.6794f, 211.93208f, (byte)40));
        treasurePositions.Add(new WorldPosition(mapId, 575.78815f, 850.86847f, 188.95987f, (byte)105));
        treasurePositions.Add(new WorldPosition(mapId, 464.98325f, 733.43646f, 212.67583f, (byte)95));
        treasurePositions.Add(new WorldPosition(mapId, 248.65541f, 756.76154f, 201.36113f, (byte)8));
        treasurePositions.Add(new WorldPosition(mapId, 373.7291f, 327.44672f, 228.25072f, (byte)7));
        treasurePositions.Add(new WorldPosition(mapId, 447.88635f, 257.91846f, 246.49289f, (byte)26));
        treasurePositions.Add(new WorldPosition(mapId, 636.1489f, 422.67554f, 242.47498f, (byte)104));
        treasurePositions.Add(new WorldPosition(mapId, 587.17395f, 507.7597f, 217.75f, (byte)106));
        treasurePositions.Add(new WorldPosition(mapId, 521.03375f, 542.86005f, 214.33388f, (byte)70));
        treasurePositions.Add(new WorldPosition(mapId, 726.05597f, 437.2465f, 229.625f, (byte)76));
        treasurePositions.Add(new WorldPosition(mapId, 731.07806f, 272.2283f, 233.4975f, (byte)30));
        treasurePositions.Add(new WorldPosition(mapId, 775.14264f, 300.74243f, 233.49748f, (byte)63));
        treasurePositions.Add(new WorldPosition(mapId, 735.2953f, 248.73598f, 253.43423f, (byte)44));
        treasurePositions.Add(new WorldPosition(mapId, 786.8716f, 291.9434f, 253.43422f, (byte)46));
    }

    private string GetZoneNameL10n(Player player)
    {
        foreach (ZoneInstance zone in player.FindZones())
        {
            int zoneNameL10nId = GetZoneNameL10nId(zone.GetAreaTemplate().GetZoneName().ToString());
            if (zoneNameL10nId > 0)
            {
                return ChatUtil.L10n(zoneNameL10nId);
            }
        }
        return null;
    }

    private int GetZoneNameL10nId(string zoneName)
    {
        return zoneName switch
        {
            "ANCILLARY_SENTRY_POST_301220000" => 404085,
            "ARTILLERY_COMMAND_CENTER_301220000" => 404088,
            "ASSAULT_COMMAND_CENTER_301220000" => 404090,
            "AXIAL_SENTRY_POST_301220000" => 404084,
            "CENTRAL_SUPPLY_BASE_301220000" => 404086,
            "HEADQUARTERS_301220000" => 404092,
            "HEADQUARTERS_ANNEX_301220000" => 404093,
            "HOLY_GROUND_OF_RESURRECTION_301220000" => 404094,
            "MILITARY_SUPPLY_BASE_2_301220000" => 404089,
            "PASHID_ARMY_ENCAMPMENT_301220000" => 404083,
            "PERIPHERAL_SUPPLY_BASE_301220000" => 404087,
            "SIEGE_BASE_301220000" => 404091,
            "THE_ETERNAL_BASTION_301220000" => 404082,
            "UNDERGROUND_WATERWAY_1_301220000" => 404095,
            _ => 0,
        };
    }

    public override float GetApMultiplier()
    {
        return CustomConfig.PVP_MAP_PVE_AP_MULTIPLIER;
    }
}
