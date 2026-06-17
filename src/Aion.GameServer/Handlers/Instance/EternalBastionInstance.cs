using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Network.Aion.Instanceinfo;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Abyss;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>
/// Remaining Online Information:<br/>
/// Quick Summary:
/// Players need to defend the fortress commander while also progressing the instance and accumulating additional points by killing the surrounding
/// camp commanders. The first three phases are progressed by killing three specific commanders, whereas the fourth phase will be completed if all
/// five siege towers are killed.<br/>
/// Killing a specific barricade or dredgion signal tower or activating the siege cannon will result in additional assault pod spawns.
/// Every two minutes an assault wave will spawn. It's strength, i.e. assaulter count, increases over time. Additional waves can spawn from
/// assault pods, siege towers or broken wall/gate.<br/>
/// Players can skip specific waves by killing enough commanders and thus reducing the assault strength. They can also use the cannons or tank to
/// make defending/attacking easier.
///
/// @author Cheatkiller, Estrayl. Java parity: instance/EternalBastionInstance : GeneralInstanceHandler. @InstanceID(300540000). 1:1.
/// </summary>
[InstanceID(300540000)]
public class EternalBastionInstance : GeneralInstanceHandler
{
    private const int START_DELAY = 180 * 1000;
    private int assaultPower = 12; // Retail. AtomicInteger
    private int progressionKills; // AtomicInteger
    private int isRaceSet; // AtomicBoolean -> 0/1
    private readonly List<ScheduledTask> spawnTasks = new();
    private ScheduledTask instanceTimerTask, assaultWaveTask;
    private int waveCount;
    private long startTime;
    private NormalScore instanceReward;

    public EternalBastionInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnDie(Npc npc)
    {
        base.OnDie(npc);
        switch (npc.GetNpcId())
        {
            case 231168: // Pashid Scout Commander Azute
            case 231169: // Pashid Scout Commander Zest
            case 231170: // Pashid Scout Commander Sartas
            case 231171: // Pashid Infantry Commander Matuk
            case 231172: // Pashid Assault Commander Badute
            case 231173: // Pashid Assault Commander Katsu
            case 231174: // Pashid Artillery Commander Murat
            case 231175: // Pashid Artillery Commander Kaimdu
            case 231176: // Pashid Artillery Commander Nirta
                AddPoints(npc, 1880);
                CheckProgress(Interlocked.Increment(ref progressionKills));
                break;
            case 231143: // Pashid Siege Tower
            case 231152: // Pashid Siege Tower
            case 231153: // Pashid Siege Tower
            case 231154: // Pashid Siege Tower
            case 231155: // Pashid Siege Tower
                AddPoints(npc, 334);
                CheckProgress(Interlocked.Increment(ref progressionKills));
                break;
            case 231177: // Deathbringer Tariksha
                AddPoints(npc, 1880);
                break;
            case 231178: // Commander Hakunta
            case 231179: // Commander Rakunta
                AddPoints(npc, 1880);
                Interlocked.Add(ref assaultPower, -2); // Retail
                break;
            case 230784: // Pashid Snare Turret
            case 230785: // Pashid Assault Flamethrower
            case 231137: // Pashid Danuar Turret
            case 231138: // Pashid Danuar Turret
            case 231140: // Pashid Assault Pod
            case 231141: // Pashid Siege Drop Pod
            case 231144: // Pashid Siege Cannon
            case 231156: // Pashid Assault Pod
            case 231157: // Pashid Assault Pod
            case 231158: // Pashid Assault Pod
            case 231159: // Pashid Assault Pod
            case 231160: // Pashid Assault Pod
            case 231162: // Pashid Assault Pod
            case 231163: // Pashid Siege Drop Pod
            case 231164: // Pashid Siege Drop Pod
            case 231165: // Pashid Siege Drop Pod
            case 231167: // Pashid Siege Drop Pod
            case 231180: // Dredgion Signal Tower
                AddPoints(npc, 334);
                break;
            case 231148: // Dredgion Signal Tower
                AddPoints(npc, 334);
                PacketSendUtility.BroadcastToMap(npc, SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5b_TD_AddWave_03());
                SpawnWithDelay(231157, 778.845f, 323.282f, 253.434f, (byte)40, 30000);
                SpawnWithDelay(231159, 697.564f, 305.424f, 249.303f, (byte)100, 30000);
                break;
            case 231149: // Pashid Army Barricade
                AddPoints(npc, 266);
                PacketSendUtility.BroadcastToMap(npc, SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5b_TD_AddWave_02());
                SpawnWithDelay(231164, 667.350f, 281.046f, 225.698f, (byte)33, 30000); // Pashid Assault Pod
                SpawnWithDelay(231165, 721.498f, 358.172f, 230.940f, (byte)0, 30000);
                break;
            case 231181: // Pashid Army Barricade
                AddPoints(npc, 266);
                break;
            case 230746: // Pashid Assault Tribuni Sentry
            case 230753: // Pashid Assault Rider
            case 230754: // Pashid Assault Gunner
            case 230756: // Pashid Assault Supply Officer
            case 230757: // Pashid Assault Dragon
                AddPoints(npc, 1002);
                Interlocked.Decrement(ref assaultPower);
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5b_TD_Notice_06());
                break;
            case 230744: // Pashid Assault Tribuni Combatant
            case 230745: // Pashid Assault Tribuni Protector
            case 230749: // Pashid Assault Tribuni Marksman
            case 231131: // Pashid Siege Dragon
            case 231132: // Pashid Siege Dragon
            case 231133: // Pashid Siege Dragon
            case 231134: // Pashid Siege Dragon
                AddPoints(npc, 1002);
                break;
            case 831333: // Castle Wall
                AddPoints(npc, -150);
                DeleteAliveNpcs(831332); // Right Castle Gate
                DeleteAliveNpcs(231150); // Drill
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5b_TD_Notice_04());
                break;
            case 831335: // Inner Water Gate
                AddPoints(npc, -150);
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5b_TD_Notice_02());
                break;
            case 209516: // Commander Lysander
            case 209517: // Commander Granir
                AddPoints(npc, -100000); // Retail
                EndInstance();
                break;
            case 209555: // Lysander's Disciple
            case 209557: // Granir's Disciple
                AddPoints(npc, -50);
                break;
            case 231130: // Grand Commander Pashid
                AddPoints(npc, 24000);
                EndInstance();
                break;
            case 231117: // Pashid Elite Siege Combatant
            case 231118: // Pashid Elite Siege Protector
            case 231119: // Pashid Elite Siege Ambusher
            case 231120: // Pashid Elite Siege Troublemaker
            case 231122: // Pashid Elite Siege Marksman
            case 231123: // Pashid Elite Siege Rampager
            case 231124: // Pashid Elite Siege Magus
            case 231125: // Pashid Elite Siege Summoner
            case 231126: // Pashid Elite Siege Cavalry
            case 231127: // Pashid Elite Siege Striker
            case 231128: // Pashid Elite Siege Medic
            case 233310: // Pashid Siege Cavalry
            case 233311: // Pashid Siege Engineer
                AddPoints(npc, 42);
                break;
            case 233312: // Pashid Siege Healer
            case 233314: // Pashid Elite Siege Defender
            case 233315: // Pashid Elite Siege Gunner
                AddPoints(npc, 36);
                break;
            case 231115: // Pashid Siege Soldier
            case 231116: // Pashid Siege Mage
            case 233309: // Pashid Siege Ambusher
                AddPoints(npc, 33);
                break;
            case 233313:
                AddPoints(npc, 20);
                break;
        }
    }

    private void CheckProgress(int progressionKills)
    {
        switch (progressionKills)
        {
            case 3:
            {
                Npc outerWaterGate = GetNpc(831334);
                if (outerWaterGate != null)
                    outerWaterGate.GetController().DeleteIfAliveOrCancelRespawn();
                Spawn(233314, 575.858f, 146.753f, 221.351f, (byte)33); // Pashid Elite Siege Defender
                Spawn(233314, 587.445f, 152.020f, 218.004f, (byte)63);
                Spawn(233314, 609.691f, 187.747f, 216.455f, (byte)87);
                Spawn(233314, 630.440f, 192.271f, 219.763f, (byte)40);
                Spawn(233315, 598.051f, 160.956f, 216.754f, (byte)100); // Pashid Elite Siege Gunner
                Spawn(233315, 609.099f, 150.973f, 216.063f, (byte)57);
                Spawn(233315, 637.820f, 203.284f, 222.032f, (byte)77);
                Spawn(233315, 641.959f, 197.833f, 221.788f, (byte)77);

                spawnTasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
                {
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5b_TD_MainWave_02());
                    Spawn(231171, 655.755f, 212.606f, 223.931f, (byte)80); // Pashid Infantry Commander Matuk
                    SpawnWithWalker(231142, 604.397f, 170.492f, 216.042f, (byte)100, "NPCPathIDLDF5b_TD_Mob_Z1_S2_D1"); // Pashid Siege Volatile
                    SpawnWithWalker(231142, 605.397f, 171.492f, 216.092f, (byte)100, "NPCPathIDLDF5b_TD_Mob_Z1_S2_D1");
                    SpawnWithWalker(231142, 603.397f, 171.492f, 216.085f, (byte)100, "NPCPathIDLDF5b_TD_Mob_Z1_S2_D1");
                    SpawnWithWalker(231173, 657.052f, 465.173f, 225.052f, (byte)100, "NPCPathIDLDF5b_TD_Mob_Z1_S2_B2F2"); // Pashid Assault Commander Katsu
                    SpawnWithWalker(233313, 659.052f, 467.173f, 225.000f, (byte)100, "NPCPathIDLDF5b_TD_Mob_Z1_S2_B2F2");
                    SpawnWithWalker(233313, 655.052f, 467.173f, 225.133f, (byte)100, "NPCPathIDLDF5b_TD_Mob_Z1_S2_B2F2");
                    SpawnWithWalker(231172, 604.429f, 413.910f, 223.782f, (byte)100, "NPCPathIDLDF5b_TD_Mob_Z1_S2_B1F2"); // Pashid Assault Commander Badute
                    SpawnWithWalker(233313, 606.429f, 411.910f, 224.027f, (byte)100, "NPCPathIDLDF5b_TD_Mob_Z1_S2_B1F2");
                    SpawnWithWalker(233313, 602.429f, 411.910f, 223.756f, (byte)100, "NPCPathIDLDF5b_TD_Mob_Z1_S2_B1F2");
                }, 90 * 1000L));
                break;
            }
            case 6:
                spawnTasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
                {
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5b_TD_MainWave_03());
                    Spawn(233313, 572.428f, 368.118f, 226.464f, (byte)113); // Pashid Siege Combatant
                    Spawn(233313, 577.691f, 374.779f, 226.077f, (byte)110);
                    Spawn(233313, 583.372f, 380.359f, 225.562f, (byte)107);
                    Spawn(233313, 590.788f, 386.729f, 224.273f, (byte)100);
                    Spawn(233313, 652.680f, 456.840f, 225.698f, (byte)110);
                    Spawn(233313, 660.402f, 469.521f, 225.095f, (byte)113);
                    Spawn(233313, 670.701f, 477.320f, 225.120f, (byte)100);
                    Spawn(233313, 681.626f, 481.653f, 224.853f, (byte)100);
                    Spawn(231137, 569.389f, 374.023f, 228.221f, (byte)110); // Pashid Danuar Turret
                    Spawn(231137, 576.424f, 381.682f, 226.099f, (byte)107);
                    Spawn(231137, 584.247f, 388.219f, 225.080f, (byte)103);
                    Spawn(231138, 650.886f, 466.252f, 225.282f, (byte)110);
                    Spawn(231138, 661.941f, 478.229f, 226.286f, (byte)103);
                    Spawn(231138, 673.506f, 486.307f, 225.869f, (byte)100);
                    Spawn(231140, 635.426f, 243.117f, 238.075f, (byte)33); // Pashid Assault Pods
                    Spawn(231141, 666.361f, 294.435f, 225.698f, (byte)20);
                    Spawn(231158, 768.339f, 390.709f, 243.356f, (byte)40);
                    Spawn(231174, 669.851f, 468.267f, 225.250f, (byte)107); // Pashid Artillery Commander Murat
                    Spawn(231175, 583.830f, 373.812f, 225.280f, (byte)107); // Pashid Artillery Commander Kaimdu
                    Spawn(231176, 760.219f, 392.471f, 243.354f, (byte)50); // Pashid Infantry Commander Nirta
                }, 90 * 1000L));
                break;
            case 9:
                spawnTasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
                {
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5b_TD_MainWave_04());
                    Spawn(231143, 613.231f, 262.163f, 227.255f, (byte)3);
                    Spawn(231152, 608.371f, 303.514f, 226.295f, (byte)113);
                    Spawn(231153, 625.244f, 352.624f, 226.295f, (byte)113);
                    Spawn(231154, 668.864f, 405.970f, 228.500f, (byte)83);
                    Spawn(231155, 691.536f, 409.367f, 231.720f, (byte)98);
                }, 90 * 1000L));
                break;
            case 14:
                spawnTasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
                {
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5b_TD_MainWave_05());
                    Spawn(231130, 740.668f, 298.082f, 233.889f, (byte)100); // Commander Pashid
                    Spawn(231131, 686.574f, 358.216f, 243.386f, (byte)100); // Pashid Siege Dragons
                    Spawn(231131, 655.856f, 351.118f, 241.595f, (byte)20);
                    Spawn(231131, 732.982f, 371.320f, 230.942f, (byte)106);
                    Spawn(231132, 582.631f, 376.172f, 225.461f, (byte)100);
                    Spawn(231133, 745.820f, 322.916f, 249.287f, (byte)86);
                    Spawn(231133, 713.242f, 289.971f, 249.285f, (byte)0);
                    Spawn(231134, 668.732f, 473.705f, 225.159f, (byte)100);
                    Spawn(231156, 641.551f, 339.264f, 238.075f, (byte)20); // Pashid Assault Pods
                    Spawn(231163, 727.175f, 364.431f, 230.941f, (byte)7);
                }, 90 * 1000L));
                break;
        }
    }

    public override void OnEnterInstance(Player player)
    {
        if (!instanceReward.IsRewarded())
            SendPacket();
        if (Interlocked.CompareExchange(ref isRaceSet, 1, 0) == 0)
        {
            SpawnRaceGuards(player.GetRace());
            if (Rnd.NextBoolean())
            {
                Spawn(231177, 821.146f, 607.305f, 239.703f, (byte)73); // Deathbringer Tariksha
                Spawn(230746, 551.146f, 412.105f, 222.760f, (byte)30); // Pashid Assault Tribuni Sentry
                Spawn(231149, 702.116f, 552.614f, 232.423f, (byte)110); // Pashid Army Barricade (Assault Pod Trigger)
                Spawn(231181, 564.414f, 250.835f, 233.198f, (byte)110); // Pashid Army Barricade
            }
            else
            {
                Spawn(230746, 821.146f, 607.305f, 239.703f, (byte)73); // Pashid Assault Tribuni Sentry
                Spawn(231177, 551.146f, 412.105f, 222.760f, (byte)30); // Deathbringer Tariksha
                Spawn(231181, 702.116f, 552.614f, 232.423f, (byte)110); // Pashid Army Barricade
                Spawn(231149, 564.414f, 250.835f, 233.198f, (byte)110); // Pashid Army Barricade (Assault Pod Trigger)
            }
        }
    }

    private void SpawnRaceGuards(Race race)
    {
        int guardId = race == Race.ELYOS ? 209555 : 209557;
        Spawn(race == Race.ELYOS ? 209516 : 209517, 750.205f, 285.880f, 233.752f, (byte)40); // Commander
        Spawn(race == Race.ELYOS ? 701923 : 701924, 744.174f, 292.949f, 233.698f, (byte)40); // Flag
        Spawn(race == Race.ELYOS ? 701625 : 701922, 640.862f, 412.784f, 243.940f, (byte)40); // Siege Cannon
        Spawn(guardId, 595.476f, 284.680f, 226.375f, (byte)40);
        Spawn(guardId, 598.868f, 284.201f, 226.424f, (byte)40);
        Spawn(guardId, 602.328f, 340.964f, 225.794f, (byte)40);
        Spawn(guardId, 605.731f, 343.153f, 225.448f, (byte)40);
        Spawn(guardId, 607.450f, 387.642f, 223.353f, (byte)40);
        Spawn(guardId, 611.817f, 388.865f, 223.500f, (byte)40);
        Spawn(guardId, 681.742f, 444.580f, 226.818f, (byte)40);
        Spawn(guardId, 684.437f, 447.848f, 226.787f, (byte)40);
        Spawn(guardId, 690.046f, 351.800f, 244.744f, (byte)40);
        Spawn(guardId, 690.220f, 341.532f, 228.674f, (byte)40);
        Spawn(guardId, 692.778f, 337.952f, 228.674f, (byte)40);
        Spawn(guardId, 693.082f, 354.432f, 244.733f, (byte)40);
        Spawn(guardId, 715.405f, 427.312f, 230.025f, (byte)40);
        Spawn(guardId, 719.378f, 428.101f, 230.112f, (byte)40);
        Spawn(guardId, 748.146f, 361.345f, 230.945f, (byte)40);
        Spawn(guardId, 749.389f, 364.988f, 230.945f, (byte)40);
        if (race == Race.ELYOS)
        {
            Spawn(701596, 617.501f, 248.196f, 235.740f, (byte)60); // Cannons
            Spawn(701597, 612.806f, 275.206f, 235.740f, (byte)67);
            Spawn(701598, 616.159f, 313.939f, 235.740f, (byte)53);
            Spawn(701599, 625.603f, 339.608f, 235.734f, (byte)53);
            Spawn(701600, 650.914f, 372.932f, 238.607f, (byte)53);
            Spawn(701601, 677.853f, 396.203f, 238.632f, (byte)40);
            Spawn(701602, 710.145f, 410.661f, 241.014f, (byte)30);
            Spawn(701603, 736.803f, 414.121f, 241.017f, (byte)40);
            Spawn(701604, 772.961f, 410.834f, 241.014f, (byte)20);
            Spawn(701605, 798.383f, 401.605f, 241.015f, (byte)30);
            Spawn(701606, 709.602f, 313.531f, 254.216f, (byte)40);
            Spawn(701607, 726.757f, 327.932f, 254.216f, (byte)50);
        }
        else
        {
            Spawn(701610, 617.501f, 248.196f, 235.740f, (byte)60); // Cannons
            Spawn(701611, 612.806f, 275.206f, 235.740f, (byte)67);
            Spawn(701612, 616.159f, 313.939f, 235.740f, (byte)53);
            Spawn(701613, 625.603f, 339.608f, 235.734f, (byte)53);
            Spawn(701614, 650.914f, 372.932f, 238.607f, (byte)53);
            Spawn(701615, 677.853f, 396.203f, 238.632f, (byte)40);
            Spawn(701616, 710.145f, 410.661f, 241.014f, (byte)30);
            Spawn(701617, 736.803f, 414.121f, 241.017f, (byte)40);
            Spawn(701618, 772.961f, 410.834f, 241.014f, (byte)20);
            Spawn(701619, 798.383f, 401.605f, 241.015f, (byte)30);
            Spawn(701620, 709.602f, 313.531f, 254.216f, (byte)40);
            Spawn(701621, 726.757f, 327.932f, 254.216f, (byte)50);
        }
    }

    public override void OnInstanceCreate()
    {
        instanceReward = new NormalScore();
        instanceReward.SetInstanceProgressionType(InstanceProgressionType.PREPARING);
        instanceReward.SetPoints(20000);
        startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        instanceTimerTask = ThreadPoolManager.GetInstance().Schedule(OnStart, START_DELAY);
    }

    private void OnStart()
    {
        startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        instanceReward.SetInstanceProgressionType(InstanceProgressionType.START_PROGRESS);
        SendPacket();
        instance.ForEachDoor(door => door.SetOpen(true));
        assaultWaveTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ => { SpawnAssaultWave(); return System.Threading.Tasks.ValueTask.CompletedTask; }, System.TimeSpan.FromMilliseconds(60000), System.TimeSpan.FromMilliseconds(60000));
        instanceTimerTask = ThreadPoolManager.GetInstance().Schedule(OnTimeOut, 30 * 60 * 1000L);

        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5b_TD_MainWave_01());
        Spawn(233313, 584.013f, 371.221f, 225.374f, (byte)110); // Pashid Siege Fighter
        Spawn(233313, 588.725f, 377.543f, 225.221f, (byte)110);
        Spawn(233313, 655.190f, 454.515f, 225.936f, (byte)110);
        Spawn(233313, 659.293f, 461.406f, 225.449f, (byte)110);
        Spawn(233313, 795.178f, 462.909f, 225.853f, (byte)118);
        Spawn(233313, 804.449f, 461.860f, 227.897f, (byte)58);
        Spawn(233315, 572.093f, 377.641f, 227.147f, (byte)110); // Pashid Elite Siege Gunner
        Spawn(233315, 580.561f, 387.814f, 225.668f, (byte)110);
        Spawn(233315, 646.597f, 458.471f, 225.575f, (byte)117);
        Spawn(233315, 652.617f, 467.432f, 225.265f, (byte)113);
        Spawn(233315, 794.179f, 474.019f, 225.361f, (byte)88);
        Spawn(233315, 806.574f, 473.837f, 227.837f, (byte)98);
        Spawn(231168, 652.191f, 461.264f, 225.095f, (byte)110); // Pashid Scout Commander Azute
        Spawn(231169, 581.777f, 377.664f, 225.528f, (byte)110); // Pashid Scout Commander Zest
        Spawn(231170, 800.515f, 469.416f, 228.586f, (byte)88); // Pashid Scout Commander Sartas
        Spawn(831334, 569.772f, 162.763f, 220.048f, (byte)53, 271); // Outer Water Gate
        SpawnWithDelay(231167, 735.282f, 295.307f, 233.752f, (byte)115, 9000); // Pashid Assault Pods
        SpawnWithDelay(231162, 747.273f, 300.182f, 233.752f, (byte)97, 6000);
    }

    private void SpawnAssaultWave()
    {
        switch (++waveCount)
        {
            case 1:
            case 5:
            case 13:
            case 17:
            case 25:
                SpawnAssaultPodWave();
                break;
            case 2:
                SpawnEasternWaveOne();
                break;
            case 4:
                SpawnEasternWaveOne();
                SpawnNorthernWaveOne();
                break;
            case 6:
                SpawnEasternWaveOne();
                SpawnNorthernWaveOne();
                SpawnEasternWaveTwo();
                SpawnCanalWave();
                SpawnSiegeTowerWave();
                break;
            case 8:
                SpawnEasternWaveOne();
                SpawnNorthernWaveOne();
                SpawnEasternWaveTwo();
                SpawnWesternWave();
                break;
            case 9:
            case 21:
                SpawnAssaultPodWave();
                SpawnSiegeTowerWave();
                break;
            case 10:
                SpawnEasternWaveOne();
                SpawnNorthernWaveOne();
                SpawnEasternWaveTwo();
                SpawnWesternWave();
                SpawnNorthernWaveTwo();
                SpawnCanalWave();
                if (Volatile.Read(ref assaultPower) >= 8)
                {
                    SpawnWithWalker(231142, 795.579f, 478.629f, 225.086f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S38"); // Pashid Siege Volatile
                    SpawnWithWalker(231142, 798.579f, 479.629f, 225.221f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S38");
                    SpawnWithWalker(231142, 792.579f, 479.629f, 224.934f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S38");
                    SpawnWithWalker(231142, 801.579f, 481.629f, 225.845f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S38");
                    SpawnWithWalker(231142, 789.579f, 481.629f, 224.622f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S38");
                }
                break;
            case 12:
                SpawnEasternWaveOne();
                SpawnNorthernWaveOne();
                SpawnEasternWaveTwo();
                SpawnWesternWave();
                SpawnNorthernWaveTwo();
                SpawnEasternWaveThree();
                SpawnSiegeTowerWave();
                break;
            case 14:
                SpawnEasternWaveOne();
                SpawnNorthernWaveOne();
                SpawnEasternWaveTwo();
                SpawnWesternWave();
                SpawnNorthernWaveTwo();
                SpawnEasternWaveThree();
                SpawnCanalWave();
                break;
            case 15:
                SpawnCanalWave();
                SpawnSiegeTowerWave();
                if (Volatile.Read(ref assaultPower) >= 7 && GetNpc(831333) != null)
                    SpawnWithWalker(231150, 798.563f, 477.952f, 225.231f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S73"); // Pashid Siege Ram
                break;
            case 16:
                if (Volatile.Read(ref assaultPower) >= 11)
                    SpawnEasternWaveOne();
                SpawnNorthernWaveOne();
                SpawnEasternWaveTwo();
                if (Volatile.Read(ref assaultPower) >= 12)
                    SpawnWesternWave();
                SpawnNorthernWaveTwo();
                SpawnEasternWaveThree();
                break;
            case 18:
                SpawnEasternWaveOne();
                SpawnNorthernWaveOne();
                if (Volatile.Read(ref assaultPower) >= 10)
                    SpawnEasternWaveTwo();
                SpawnWesternWave();
                if (Volatile.Read(ref assaultPower) >= 9)
                    SpawnNorthernWaveTwo();
                SpawnEasternWaveThree();
                SpawnCanalWave();
                SpawnSouthernWave();
                SpawnSiegeTowerWave();
                break;
            case 20:
                SpawnEasternWaveOne();
                SpawnNorthernWaveOne();
                SpawnEasternWaveTwo();
                SpawnWesternWave();
                SpawnNorthernWaveTwo();
                SpawnEasternWaveThree();
                break;
            case 22:
                SpawnEasternWaveOne();
                SpawnNorthernWaveOne();
                SpawnEasternWaveTwo();
                SpawnWesternWave();
                SpawnNorthernWaveTwo();
                SpawnEasternWaveThree();
                SpawnCanalWave();
                SpawnSouthernWave();
                break;
            case 24:
                if (Volatile.Read(ref assaultPower) >= 5)
                    SpawnEasternWaveOne();
                SpawnNorthernWaveOne();
                SpawnEasternWaveTwo();
                if (Volatile.Read(ref assaultPower) >= 6)
                    SpawnWesternWave();
                SpawnNorthernWaveTwo();
                SpawnEasternWaveThree();
                SpawnSiegeTowerWave();
                break;
            case 26:
                SpawnEasternWaveOne();
                if (Volatile.Read(ref assaultPower) >= 3)
                    SpawnNorthernWaveOne();
                SpawnEasternWaveTwo();
                SpawnWesternWave();
                SpawnNorthernWaveTwo();
                if (Volatile.Read(ref assaultPower) >= 4)
                    SpawnEasternWaveThree();
                SpawnCanalWave();
                SpawnSouthernWave();
                break;
            case 28:
                SpawnEasternWaveOne();
                SpawnNorthernWaveOne();
                if (Volatile.Read(ref assaultPower) >= 1)
                    SpawnEasternWaveTwo();
                SpawnWesternWave();
                if (Volatile.Read(ref assaultPower) >= 2)
                    SpawnNorthernWaveTwo();
                SpawnEasternWaveThree();
                break;
        }
    }

    private void SpawnEasternWaveOne()
    {
        SpawnWithWalker(231113, 652.071f, 475.738f, 226.125f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S46"); // East 1
        SpawnWithWalker(231110, 655.071f, 478.738f, 226.125f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S46");
        SpawnWithWalker(231110, 649.071f, 478.738f, 226.125f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S46");
    }

    private void SpawnEasternWaveTwo()
    {
        SpawnWithWalker(231114, 671.857f, 480.417f, 225.195f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S32"); // East 2
        SpawnWithWalker(231112, 674.857f, 483.417f, 225.337f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S32");
        SpawnWithWalker(231112, 668.857f, 483.417f, 226.457f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S32");
    }

    private void SpawnEasternWaveThree()
    {
        SpawnWithWalker(231113, 632.525f, 451.311f, 223.422f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S34"); // East 3
        SpawnWithWalker(231111, 635.525f, 454.311f, 223.193f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S34");
        SpawnWithWalker(231111, 629.525f, 454.311f, 220.445f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S34");
    }

    private void SpawnNorthernWaveOne()
    {
        SpawnWithWalker(231113, 598.026f, 411.715f, 223.784f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S71"); // North 1
        SpawnWithWalker(231110, 601.026f, 414.715f, 223.519f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S71");
        SpawnWithWalker(231110, 595.026f, 414.715f, 223.552f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S71");
    }

    private void SpawnNorthernWaveTwo()
    {
        SpawnWithWalker(231113, 569.237f, 387.007f, 227.533f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S36"); // North 2
        SpawnWithWalker(231111, 572.237f, 390.007f, 227.905f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S36");
        SpawnWithWalker(231111, 566.237f, 390.007f, 228.194f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S36");
    }

    private void SpawnWesternWave()
    {
        SpawnWithWalker(231114, 587.952f, 239.621f, 229.530f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S44"); // West
        SpawnWithWalker(231112, 590.952f, 242.621f, 229.152f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S44");
        SpawnWithWalker(231112, 584.952f, 242.621f, 229.822f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S44");
    }

    private void SpawnSouthernWave()
    {
        if (GetNpc(831333) == null)
        {
            SpawnWithWalker(231113, 794.134f, 483.021f, 224.756f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S39"); // South Wall
            SpawnWithWalker(231113, 796.134f, 481.021f, 225.008f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S39");
            SpawnWithWalker(231113, 792.134f, 481.021f, 224.820f, (byte)100, "NPCPathIDLDF5b_TD_Mob_S39");
        }
    }

    private void SpawnCanalWave()
    {
        if (GetNpc(831335) == null)
        {
            SpawnWithWalker(231110, 610.571f, 189.724f, 216.509f, (byte)100, "NPCPathIDLDF5b_TD_Mob_Z1_S2_T1"); // Canal
            SpawnWithWalker(231108, 612.571f, 191.724f, 216.589f, (byte)100, "NPCPathIDLDF5b_TD_Mob_Z1_S2_T1");
            SpawnWithWalker(231108, 608.571f, 187.724f, 216.574f, (byte)100, "NPCPathIDLDF5b_TD_Mob_Z1_S2_T1");
        }
    }

    private void SpawnAssaultPodWave()
    {
        if (GetNpc(231140) != null)
        {
            SpawnWithWalker(231106, 633.457f, 245.792f, 238.075f, (byte)33, "NPCPathIDLDF5b_TD_Mob_Z1_S3_POD01");
            SpawnWithWalker(231108, 635.457f, 247.792f, 238.075f, (byte)33, "NPCPathIDLDF5b_TD_Mob_Z1_S3_POD01");
            SpawnWithWalker(231108, 631.457f, 247.792f, 238.075f, (byte)33, "NPCPathIDLDF5b_TD_Mob_Z1_S3_POD01");
        }
        if (GetNpc(231156) != null)
        {
            SpawnWithWalker(231106, 642.871f, 343.420f, 238.075f, (byte)20, "NPCPathIDLDF5b_TD_Z1_S5_POD01");
            SpawnWithWalker(231108, 644.871f, 345.420f, 238.075f, (byte)20, "NPCPathIDLDF5b_TD_Z1_S5_POD01");
            SpawnWithWalker(231108, 640.871f, 345.420f, 238.075f, (byte)20, "NPCPathIDLDF5b_TD_Z1_S5_POD01");
        }
        if (GetNpc(231157) != null)
        {
            SpawnWithWalker(231106, 776.242f, 326.041f, 253.434f, (byte)40, "NPCPathIDLDF5b_TD_Z4_POD02");
            SpawnWithWalker(231108, 778.242f, 328.041f, 253.434f, (byte)40, "NPCPathIDLDF5b_TD_Z4_POD02");
            SpawnWithWalker(231108, 774.242f, 328.041f, 253.434f, (byte)40, "NPCPathIDLDF5b_TD_Z4_POD02");
        }
        if (GetNpc(231158) != null)
        {
            SpawnWithWalker(231106, 765.481f, 393.614f, 243.354f, (byte)40, "NPCPathIDLDF5b_TD_Mob_Z1_S3_POD3");
            SpawnWithWalker(231108, 767.481f, 395.614f, 243.354f, (byte)40, "NPCPathIDLDF5b_TD_Mob_Z1_S3_POD3");
            SpawnWithWalker(231108, 763.481f, 395.614f, 243.354f, (byte)40, "NPCPathIDLDF5b_TD_Mob_Z1_S3_POD3");
        }
        if (GetNpc(231141) != null)
        {
            SpawnWithWalker(231105, 667.631f, 297.565f, 225.700f, (byte)20, "NPCPathIDLDF5b_TD_Mob_Z1_S3_POD2");
            SpawnWithWalker(231107, 669.631f, 299.565f, 225.700f, (byte)20, "NPCPathIDLDF5b_TD_Mob_Z1_S3_POD2");
            SpawnWithWalker(231107, 665.631f, 299.565f, 225.700f, (byte)20, "NPCPathIDLDF5b_TD_Mob_Z1_S3_POD2");
        }
        if (GetNpc(231163) != null)
        {
            SpawnWithWalker(231105, 731.089f, 365.461f, 230.941f, (byte)7, "NPCPathIDLDF5b_TD_Z1_S5_POD02");
            SpawnWithWalker(231107, 731.089f, 365.461f, 230.941f, (byte)7, "NPCPathIDLDF5b_TD_Z1_S5_POD02");
            SpawnWithWalker(231107, 731.089f, 365.461f, 230.941f, (byte)7, "NPCPathIDLDF5b_TD_Z1_S5_POD02");
        }
        if (GetNpc(231159) != null)
        {
            SpawnWithWalker(231106, 699.760f, 302.938f, 249.303f, (byte)100, "NPCPathIDLDF5b_TD_Z4_POD01");
            SpawnWithWalker(231108, 701.760f, 304.938f, 249.303f, (byte)100, "NPCPathIDLDF5b_TD_Z4_POD01");
            SpawnWithWalker(231108, 697.760f, 304.938f, 249.303f, (byte)100, "NPCPathIDLDF5b_TD_Z4_POD01");
        }
        if (GetNpc(231162) != null)
        { // Could be a bug on retail, but anyway
            SpawnWithWalker(231106, 724.927f, 359.346f, 230.941f, (byte)0, "NPCPathIDLDF5b_TD_Z3_POD02");
            SpawnWithWalker(231108, 726.927f, 361.346f, 230.941f, (byte)0, "NPCPathIDLDF5b_TD_Z3_POD02");
            SpawnWithWalker(231108, 722.927f, 361.346f, 230.941f, (byte)0, "NPCPathIDLDF5b_TD_Z3_POD02");
        }
        if (GetNpc(231164) != null)
        {
            SpawnWithWalker(231106, 724.927f, 359.346f, 230.941f, (byte)0, "NPCPathIDLDF5b_TD_Z3_POD02");
            SpawnWithWalker(231108, 726.927f, 361.346f, 230.941f, (byte)0, "NPCPathIDLDF5b_TD_Z3_POD02");
            SpawnWithWalker(231108, 722.927f, 361.346f, 230.941f, (byte)0, "NPCPathIDLDF5b_TD_Z3_POD02");
        }
        if (GetNpc(231165) != null)
        {
            SpawnWithWalker(231106, 724.927f, 359.346f, 230.941f, (byte)0, "NPCPathIDLDF5b_TD_Z3_POD02");
            SpawnWithWalker(231108, 726.927f, 361.346f, 230.941f, (byte)0, "NPCPathIDLDF5b_TD_Z3_POD02");
            SpawnWithWalker(231108, 722.927f, 361.346f, 230.941f, (byte)0, "NPCPathIDLDF5b_TD_Z3_POD02");
        }
    }

    private void SpawnSiegeTowerWave()
    {
        if (GetNpc(230783) != null)
        {
            SpawnWithWalker(231107, 623.235f, 263.392f, 238.484f, (byte)3, "NPCPathIDLDF5b_TD_Z1_S4_T1");
            SpawnWithWalker(231105, 625.235f, 265.392f, 238.484f, (byte)3, "NPCPathIDLDF5b_TD_Z1_S4_T1");
            SpawnWithWalker(231105, 621.235f, 265.392f, 238.484f, (byte)3, "NPCPathIDLDF5b_TD_Z1_S4_T1");
        }
        if (GetNpc(231152) != null)
        {
            SpawnWithWalker(231107, 621.920f, 298.179f, 238.075f, (byte)113, "NPCPathIDLDF5b_TD_Z1_S4_T2");
            SpawnWithWalker(231105, 623.920f, 300.179f, 238.075f, (byte)113, "NPCPathIDLDF5b_TD_Z1_S4_T2");
            SpawnWithWalker(231105, 619.920f, 300.179f, 238.075f, (byte)113, "NPCPathIDLDF5b_TD_Z1_S4_T2");
        }
        if (GetNpc(231153) != null)
        {
            SpawnWithWalker(231107, 644.089f, 351.522f, 239.764f, (byte)113, "NPCPathIDLDF5b_TD_Z1_S4_T3");
            SpawnWithWalker(231105, 646.089f, 353.522f, 241.151f, (byte)113, "NPCPathIDLDF5b_TD_Z1_S4_T3");
            SpawnWithWalker(231105, 642.089f, 353.522f, 239.809f, (byte)113, "NPCPathIDLDF5b_TD_Z1_S4_T3");
        }
        if (GetNpc(231154) != null)
        {
            SpawnWithWalker(231107, 664.091f, 394.303f, 240.223f, (byte)83, "NPCPathIDLDF5b_TD_Z1_S4_T4");
            SpawnWithWalker(231105, 666.091f, 396.303f, 240.223f, (byte)83, "NPCPathIDLDF5b_TD_Z1_S4_T4");
            SpawnWithWalker(231105, 662.091f, 396.303f, 240.223f, (byte)83, "NPCPathIDLDF5b_TD_Z1_S4_T4");
        }
        if (GetNpc(231155) != null)
        {
            SpawnWithWalker(231107, 692.867f, 396.708f, 241.594f, (byte)85, "NPCPathIDLDF5b_TD_Z1_S4_T5");
            SpawnWithWalker(231105, 694.867f, 398.708f, 242.018f, (byte)85, "NPCPathIDLDF5b_TD_Z1_S4_T5");
            SpawnWithWalker(231105, 690.867f, 398.708f, 241.594f, (byte)85, "NPCPathIDLDF5b_TD_Z1_S4_T5");
        }
    }

    private void OnTimeOut()
    {
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5b_TD_MainWave_06());
        EndInstance();
    }

    /*
     * Original points for ranks:
     * 92,000 = S-Rank
     * 84,000 = A-Rank
     * 76,000 = B-Rank
     * 50,000 = C-Rank
     * 10,000 = D-Rank
     */
    private void EndInstance()
    {
        CancelTasks();
        instanceReward.SetInstanceProgressionType(InstanceProgressionType.END_PROGRESS);

        int rank = GetFinalRank();
        switch (rank)
        {
            case 1:
                instanceReward.SetFinalAp(35000);
                instanceReward.SetRewardItem1(186000242); // Ceramium Medal
                instanceReward.SetRewardItem1Count(4);
                instanceReward.SetRewardItem2(188052596); // Highest Grade Material Support Bundle
                instanceReward.SetRewardItem2Count(1);
                instanceReward.SetRewardItem3(188052594); // Highest Grade Material Box
                instanceReward.SetRewardItem3Count(1);
                break;
            case 2:
                instanceReward.SetFinalAp(25000);
                instanceReward.SetRewardItem1(186000242); // Ceramium Medal
                instanceReward.SetRewardItem1Count(2);
                instanceReward.SetRewardItem2(188052594); // Highest Grade Material Box
                instanceReward.SetRewardItem2Count(1);
                instanceReward.SetRewardItem3(188052597); // High Grade Material Support Bundle
                instanceReward.SetRewardItem3Count(1);
                break;
            case 3: // B-Rank
                instanceReward.SetFinalAp(15000);
                instanceReward.SetRewardItem1(186000242); // Ceramium Medal
                instanceReward.SetRewardItem1Count(1);
                instanceReward.SetRewardItem2(188052595); // High Grade Material Box
                instanceReward.SetRewardItem2Count(1);
                instanceReward.SetRewardItem3(188052598); // Low Grade Material Support Bundle
                instanceReward.SetRewardItem3Count(1);
                break;
            case 4: // C-Rank
                instanceReward.SetFinalAp(11000);
                instanceReward.SetRewardItem1(188052598); // Low Grade Material Support Bundle
                instanceReward.SetRewardItem1Count(1);
                break;
            case 5: // D-Rank
                instanceReward.SetFinalAp(7000);
                break;
        }
        instanceReward.SetInstanceProgressionType(InstanceProgressionType.END_PROGRESS);
        instanceReward.SetRank(rank);
        instance.ForEachNpc(npc => npc.GetController().Delete());
        SendPacket();
        instance.ForEachPlayer(DistributeRewards);
        SpawnFinalChest(rank);
        Spawn(730871, 766.458f, 263.157f, 233.498f, (byte)100); // Exit
        log.LogInformation("[{MapName}] Instance completed with {Points} points resulting in {Rank}-Rank. Player(s) in instance: {Players}",
            DataManager.WORLD_MAPS_DATA.GetTemplate(mapId).GetName(), instanceReward.GetPoints(), GetRankNameById(rank),
            string.Join(", ", instance.GetPlayersInside().Select(p => string.Format("{0} (ID:{1})", p.GetName(), p.GetObjectId()))));
    }

    private int GetFinalRank()
    {
        if (instanceReward.GetPoints() >= 90000) // S-Rank
        {
            return 1;
        }
        else if (instanceReward.GetPoints() >= 82000) // A-Rank
        {
            return 2;
        }
        else if (instanceReward.GetPoints() >= 60000) // B-Rank
        {
            return 3;
        }
        else if (instanceReward.GetPoints() >= 30000) // C-Rank
        {
            return 4;
        }
        else if (instanceReward.GetPoints() >= 5000) // D-Rank
        {
            return 5;
        }
        else
        {
            return 8;
        }
    }

    private void SpawnFinalChest(int rank)
    {
        switch (rank)
        {
            case 1:
                Spawn(701913, 744.167f, 292.860f, 233.702f, (byte)100); // Biggest in model size
                break;
            case 2:
                Spawn(701914, 744.167f, 292.860f, 233.702f, (byte)100);
                break;
            case 3:
                Spawn(701915, 744.167f, 292.860f, 233.702f, (byte)100);
                break;
            case 4:
                Spawn(701916, 744.167f, 292.860f, 233.702f, (byte)100);
                break;
            case 5:
                Spawn(701917, 744.167f, 292.860f, 233.702f, (byte)100); // Smallest in model size
                break;
        }
    }

    private void DistributeRewards(Player player)
    {
        AbyssPointsService.AddAp(player, instanceReward.GetFinalAp());
        if (instanceReward.GetRewardItem1() > 0)
            ItemService.AddItem(player, instanceReward.GetRewardItem1(), instanceReward.GetRewardItem1Count(), true);
        if (instanceReward.GetRewardItem2() > 0)
            ItemService.AddItem(player, instanceReward.GetRewardItem2(), instanceReward.GetRewardItem2Count(), true);
        if (instanceReward.GetRewardItem3() > 0)
            ItemService.AddItem(player, instanceReward.GetRewardItem3(), instanceReward.GetRewardItem3Count(), true);
        if (instanceReward.GetRewardItem4() > 0)
            ItemService.AddItem(player, instanceReward.GetRewardItem4(), instanceReward.GetRewardItem4Count(), true);
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        if (npc.GetNpcId() == 701625 || npc.GetNpcId() == 701922)
        {
            Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc, 21069, 1, npc).UseSkill();
            ThreadPoolManager.GetInstance().Schedule(() => npc.GetController().Delete(), 3000);
            SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDLDF5b_TD_AddWave_01(), 3000);
            SpawnWithDelay(231160, 707.703f, 259.173f, 253.038f, (byte)40, 33000); // Assault Pod
        }
    }

    public override void OnEndEffect(Effect effect)
    {
        if (effect.GetEffected() is Player player && !player.IsDead() && !player.GetLifeStats().IsAboutToDie())
        {
            switch (effect.GetSkillId())
            {
                case 21138: // Cannons respawn if not killed
                case 21139:
                {
                    Point3D pos = new Point3D(player.GetX(), player.GetY(), player.GetZ());
                    Race race = player.GetRace();
                    spawnTasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
                    {
                        if (instanceReward.GetInstanceProgressionType() == InstanceProgressionType.START_PROGRESS)
                            Spawn(race == Race.ELYOS ? 701596 : 701610, pos.GetX(), pos.GetY(), pos.GetZ(), (byte)50);
                    }, 10 * 1000L));
                    break;
                }
            }
        }
    }

    public override void OnInstanceDestroy()
    {
        CancelTasks();
    }

    public override bool OnReviveEvent(Player player)
    {
        PlayerReviveService.Revive(player, 100, 100, false, 0);
        player.GetGameStats().UpdateStatsAndSpeedVisually();
        TeleportService.TeleportTo(player, instance, 449.581f, 448.846f, 270.747f, (byte)70);
        return true;
    }

    private void CancelTasks()
    {
        spawnTasks.ForEach(task =>
        {
            if (task != null && !task.IsDone())
                task.Cancel(false);
        });
        if (instanceTimerTask != null && !instanceTimerTask.IsCancelled)
            instanceTimerTask.Cancel(false);
        if (assaultWaveTask != null && !assaultWaveTask.IsCancelled)
            assaultWaveTask.Cancel(false);
    }

    private void SpawnWithDelay(int npcId, float x, float y, float z, byte h, int delay)
    {
        spawnTasks.Add(ThreadPoolManager.GetInstance().Schedule(() => Spawn(npcId, x, y, z, h), delay));
    }

    private void SpawnWithWalker(int npcId, float x, float y, float z, byte h, string walker)
    {
        Spawn(npcId, x, y, z, h).GetSpawn().SetWalkerId(walker);
    }

    private void AddPoints(Npc npc, int points)
    {
        if (instanceReward.GetInstanceProgressionType() == InstanceProgressionType.START_PROGRESS)
        {
            instanceReward.AddPoints(points);
            PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_GET_SCORE(npc.GetObjectTemplate().GetL10n(), points));
            SendPacket();
        }
    }

    private int GetTime()
    {
        int current = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime);
        return instanceReward.GetInstanceProgressionType() == InstanceProgressionType.PREPARING ? 180000 - current : Math.Max(1800000 - current, 0);
    }

    private void SendPacket()
    {
        PacketSendUtility.BroadcastToMap(instance, new SM_INSTANCE_SCORE(instance.GetMapId(), new EternalBastionScoreWriter(instanceReward), GetTime()));
    }

    public override void LeaveInstance(Player player)
    {
        if (instanceReward.GetInstanceProgressionType() == InstanceProgressionType.END_PROGRESS)
            TeleportService.MoveToInstanceExit(player, mapId, player.GetRace());
    }

    private string GetRankNameById(int rank)
    {
        return rank switch
        {
            1 => "S",
            2 => "A",
            3 => "B",
            4 => "C",
            5 => "D",
            _ => "F",
        };
    }

    public override bool IsBoss(Npc npc)
    {
        return npc.GetNpcId() switch
        {
            209516 or 209517 or 231168 or 231169 or 231170 or 231171 or 231172 or 231173 or 231174 or 231175 or 231176 or 231130 => true,
            _ => false,
        };
    }
}
