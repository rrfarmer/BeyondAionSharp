using System;
using System.Threading;
using System.Threading.Tasks;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/RaksangRuinsInstance (Estrayl, AION 4.8) : GeneralInstanceHandler. @InstanceID(300610000). Rnd way; AtomicBoolean/Int→int+Interlocked; volatile bool; Future→ScheduledTask. getExpMultiplier 1.8f; onEnterInstance race entry; onDie wave webs; handleUseItemFinish forge/vault; delaySpawn overloads (wave A/B/C); rndSpawnInRange (GeoService collision); onCreatureDetected/onEnterZone wave triggers; onInstanceDestroy. 1:1.</summary>
[InstanceID(300610000)]
public class RaksangRuinsInstance : GeneralInstanceHandler
{
    private readonly int way = Rnd.Get(0, 2);
    private int spawns;
    private int isEventStarted;
    private int waveKillz;
    private volatile bool isInstanceDestroyed;
    private ScheduledTask spawnTask;
    private bool isDoorAccessible = false;

    public RaksangRuinsInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override float GetExpMultiplier()
    {
        return 1.8f;
    }

    public override void OnEnterInstance(Player player)
    {
        Spawn(206378 + way + (player.GetRace() == Race.ASMODIANS ? 17 : 0), 818.103f, 931.0215f, 1207.4312f, (byte)13);
    }

    public override void OnDie(Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 236010: // Trained Porgus
            case 236011: // Trained Worg
            case 236012: // Crumbling Skelesword
            case 236013: // Withering Husk
            case 236014: // Ragelich Adept
                if (Interlocked.Increment(ref waveKillz) >= 31)
                {
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_TAMES_SOLO_A_END());
                    isDoorAccessible = true;
                }
                break;
            case 236074:
            case 236075:
            case 236076:
                switch (Interlocked.Increment(ref waveKillz))
                {
                    case 15:
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_TAMES_SOLO_C_END());
                        instance.SetDoorState(457, true);
                        break;
                    case 30:
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_TAMES_SOLO_C_END());
                        instance.SetDoorState(64, true);
                        break;
                }
                break;
            case 236020:
            case 236021:
            case 236096:
            case 236097:
                switch (Interlocked.Increment(ref waveKillz))
                {
                    case 28:
                        spawnTask.Cancel(false);
                        spawnTask = null;
                        Interlocked.Exchange(ref spawns, 0);
                        instance.SetDoorState(107, true);
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_TAMES_SOLO_B_END());
                        break;
                    case 56:
                        Spawn(236305, 331.107f, 787.326f, 147.798f, (byte)47);
                        break;
                }
                break;
            case 236084:
                instance.SetDoorState(307, true);
                break;
            case 217469:
                instance.SetDoorState(107, true);
                break;
            case 236303:
                instance.SetDoorState(294, true);
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_TAMES_SOLO_A_END());
                break;
            case 236304:
                instance.SetDoorState(118, true);
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_TAMES_SOLO_C_END());
                break;
            case 236305:
                instance.SetDoorState(324, true);
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_TAMES_SOLO_B_END());
                break;
            case 236306:
                Spawn(730445, 620.65f, 663.44f, 522.049f, (byte)27); // Exit
                break;
            case 0:
                Spawn(730438, 628.132f, 187.505f, 924.86f, (byte)0);
                break;
        }
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 702673: // Torment's Forge
            case 702674:
            case 702675:
            case 702690:
            case 702691:
            case 702692:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_TAMES_SOLO_A_START());
                DelaySpawn(player, 236074 + Rnd.NextInt(3), 2000);
                DelaySpawn(player, 236074 + Rnd.NextInt(3), 2000);
                DelaySpawn(player, 236074 + Rnd.NextInt(3), 8000);
                DelaySpawn(player, 236074 + Rnd.NextInt(3), 8000);
                DelaySpawn(player, 236074 + Rnd.NextInt(3), 12000);
                npc.GetController().Delete();
                break;
            case 730438: // Terror' Vault
                if (isDoorAccessible)
                    TeleportService.TeleportTo(player, instance, 711.10895f, 312.82013f, 910.6781f);
                else
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_TAMES_SOLO_A_DOOR_CONDITION());
                break;
        }
    }

    /// <summary>
    /// Used for way A (Terror's Vault &amp; Cuttling Grounds).
    /// </summary>
    private void DelaySpawn(int npcId, int delay)
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (!isInstanceDestroyed)
                Spawn(npcId, 621.50f, 198.54f, 924.838f, (byte)42);
        }, delay);
    }

    private void DelaySpawn(byte step, int delay)
    {
        spawnTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (Interlocked.Increment(ref spawns) > 8 || isInstanceDestroyed)
            {
                spawnTask.Cancel(false);
                return ValueTask.CompletedTask;
            }
            switch (step)
            {
                case 1:
                    switch (Volatile.Read(ref spawns))
                    {
                        case 1:
                        case 3:
                        case 5:
                        case 7:
                            Spawn(236020, 344.016f, 630.834f, 146.514f, (byte)0);
                            Spawn(236020, 325.803f, 645.552f, 146.514f, (byte)0);
                            Spawn(236020, 307.303f, 614.676f, 146.514f, (byte)0);
                            Spawn(236020, 329.768f, 608.466f, 146.514f, (byte)0);
                            break;
                        case 2:
                        case 4:
                        case 6:
                        case 8:
                            Spawn(236021, 312.577f, 605.469f, 146.514f, (byte)0);
                            Spawn(236021, 307.422f, 630.051f, 146.514f, (byte)0);
                            Spawn(236021, 337.200f, 639.658f, 146.514f, (byte)0);
                            break;
                    }
                    break;
                case 2:
                    switch (Volatile.Read(ref spawns))
                    {
                        case 1:
                        case 3:
                        case 5:
                        case 7:
                            Spawn(236096, 303.755f, 772.090f, 148.988f, (byte)0);
                            Spawn(236096, 350.070f, 801.111f, 145.082f, (byte)0);
                            Spawn(236096, 332.003f, 767.004f, 147.394f, (byte)0);
                            Spawn(236096, 325.803f, 810.720f, 144.885f, (byte)0);
                            break;
                        case 2:
                        case 4:
                        case 6:
                        case 8:
                            Spawn(236097, 344.371f, 781.986f, 147.159f, (byte)0);
                            Spawn(236097, 339.273f, 813.446f, 145.390f, (byte)0);
                            Spawn(236097, 317.126f, 766.019f, 148.844f, (byte)0);
                            break;
                    }
                    break;
                case 3:
                    switch (Volatile.Read(ref spawns))
                    {
                        case 1:
                            DelaySpawn(236010, 1000);
                            DelaySpawn(236010, 4000);
                            DelaySpawn(236010, 7000);
                            DelaySpawn(236010, 10000);
                            DelaySpawn(236010, 13000);
                            break;
                        case 2:
                            DelaySpawn(236010, 1000);
                            DelaySpawn(236010, 3000);
                            DelaySpawn(236010, 6000);
                            DelaySpawn(236011, 9000);
                            DelaySpawn(236011, 9500);
                            break;
                        case 3:
                            DelaySpawn(236011, 1000);
                            DelaySpawn(236011, 1500);
                            DelaySpawn(236011, 6000);
                            DelaySpawn(236011, 6500);
                            DelaySpawn(236011, 7000);
                            break;
                        case 4:
                            DelaySpawn(236011, 1000);
                            DelaySpawn(236011, 1500);
                            DelaySpawn(236013, 2000);
                            break;
                        case 5:
                            DelaySpawn(236010, 2000);
                            DelaySpawn(236010, 5000);
                            DelaySpawn(236010, 7000);
                            DelaySpawn(236011, 11000);
                            DelaySpawn(236011, 11500);
                            break;
                        case 6:
                            DelaySpawn(236011, 1000);
                            DelaySpawn(236011, 1500);
                            DelaySpawn(236013, 2000);
                            DelaySpawn(236011, 6000);
                            DelaySpawn(236011, 6500);
                            break;
                        case 7:
                            DelaySpawn(236012, 7000);
                            DelaySpawn(236012, 7500);
                            DelaySpawn(236014, 8000);
                            break;
                    }
                    break;
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.Zero, TimeSpan.FromMilliseconds(delay));
    }

    /// <summary>
    /// Used for way C (Torment's Forge).
    /// </summary>
    private void DelaySpawn(Player p, int npcId, int delay)
    {
        ThreadPoolManager.GetInstance().Schedule(() => RndSpawnInRange(p, npcId, 5), delay);
    }

    private void RndSpawnInRange(Player player, int npcId, int distance)
    {
        double angleRadians = (Math.PI / 180.0) * Rnd.NextFloat(360f);
        float x = player.GetX() + (float)(Math.Cos(angleRadians) * distance);
        float y = player.GetY() + (float)(Math.Sin(angleRadians) * distance);
        Vector3f pos = GeoService.GetInstance().GetClosestCollision(player, x, y, player.GetZ());
        byte headingTowardsPlayer = PositionUtil.GetHeadingTowards(pos.GetX(), pos.GetY(), player.GetX(), player.GetY());
        Spawn(npcId, pos.GetX(), pos.GetY(), pos.GetZ(), headingTowardsPlayer);
    }

    public override void OnCreatureDetected(Npc detector, Creature detected)
    {
        if (detected is Player)
        {
            switch (detector.GetNpcId())
            {
                case 206197:
                    detector.GetController().Delete();
                    instance.SetDoorState(219, true); // collapse pathway
                    break;
                case 206198:
                    if (spawnTask == null && Interlocked.CompareExchange(ref isEventStarted, 1, 0) == 0)
                    {
                        detector.GetController().Delete();
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_TAMES_SOLO_B_START());
                        DelaySpawn((byte)1, 10000);
                    }
                    break;
                case 206199:
                    if (spawnTask == null && Interlocked.CompareExchange(ref isEventStarted, 0, 1) == 1)
                    {
                        detector.GetController().Delete();
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_TAMES_SOLO_B_START());
                        DelaySpawn((byte)2, 10000);
                    }
                    break;
            }
        }
    }

    public override void OnEnterZone(Player player, ZoneInstance zone)
    {
        if (zone.GetZoneTemplate().GetName() == ZoneName.Get("IDRAKSHA_SOLO_WAVE_01_206399_1_300610000"))
        {
            if (spawnTask == null && Interlocked.CompareExchange(ref isEventStarted, 1, 0) == 0)
            {
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_TAMES_SOLO_A_START());
                DelaySpawn((byte)3, 20000);
            }
        }
    }

    public override void OnInstanceDestroy()
    {
        isInstanceDestroyed = true;
        if (spawnTask != null)
            spawnTask.Cancel(true);
    }
}
