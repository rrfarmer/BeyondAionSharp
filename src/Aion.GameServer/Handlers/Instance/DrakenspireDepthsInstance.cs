using System.Threading;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>
/// Quick Difficulty Guide:
/// Difficulty 5: Hard Mode
/// - Players have to kill the twin bosses in under 5 minutes within a 15s gap
/// - Players have to kill Orissan in his second vulnerable phase
/// - Players have to defeat 5 waves to defend their siege weapon
/// - Players have to dispel 3 buffs of Beritra and he'll transform into a dragon
/// Difficulty 3: Normal Mode
/// - Players have failed to kill the twin bosses in under 5 minutes or failed to kill Orissan in his second vulnerable phase
/// - Players have to kill a weakened Orissan in his second vulnerable phase (if they already failed the twin bosses)
/// - Players have to defeat 3 waves to defend their siege weapon
/// - Beritra remains human and NPCs will help players removing "Everlasting Life"
/// Difficulty 1: Easy Mode
/// - Players have failed to kill the twin bosses in under 5 minutes and failed to kill the weakened Orissan in his second vulnerable phase
/// - Players have to defeat 1 wave to defend their siege weapon
/// - Beritra remains human and NPCs will immediately help players removing all buffs
/// Difficulty 0: Fail Run
/// - Players have failed to defend their siege weapon, so no Beritra for them
///
/// @author Estrayl. Java parity: instance/DrakenspireDepthsInstance : GeneralInstanceHandler. @InstanceID(301390000). 1:1.
/// </summary>
[InstanceID(301390000)]
public class DrakenspireDepthsInstance : GeneralInstanceHandler
{
    private const int HARD_MODE = 5;
    private const int NORMAL_MODE = 3;
    private const int EASY_MODE = 1;
    private const int FAILED = 0;
    private int difficulty = HARD_MODE;
    private int isStageActive; // AtomicBoolean -> 0/1
    private int waveSurvivors = 8; // increases the final boss time by 15s for each survivor
    private int eventCounter;
    private Race? race; // AtomicReference<Race>
    private readonly object raceLock = new();
    private ScheduledTask currentEventTask, additionalEventTask; // re-usable for all stages

    public DrakenspireDepthsInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceDestroy()
    {
        CancelTasks(currentEventTask, additionalEventTask);
    }

    public override void OnEnterInstance(Player player)
    {
        bool wasSet = false;
        lock (raceLock)
        {
            if (race == null)
            {
                race = player.GetRace();
                wasSet = true;
            }
        }
        if (wasSet)
        {
            Spawn(race == Race.ELYOS ? 209678 : 209743, 353.443f, 185.629f, 1686.1825f, (byte)60);
            Spawn(race == Race.ELYOS ? 209679 : 209744, 353.443f, 179.259f, 1686.1825f, (byte)60);
            Spawn(race == Race.ELYOS ? 209680 : 209745, 347.099f, 192.390f, 1686.1825f, (byte)90);
            Spawn(race == Race.ELYOS ? 209680 : 209745, 351.295f, 192.437f, 1686.1825f, (byte)90);
            Spawn(race == Race.ELYOS ? 209681 : 209746, 347.099f, 173.774f, 1686.1825f, (byte)30);
            Spawn(race == Race.ELYOS ? 209681 : 209746, 351.295f, 173.821f, 1686.1825f, (byte)30);
        }
    }

    public override void OnSpawn(VisibleObject obj)
    {
        if (obj is Npc npc)
        {
            switch (npc.GetNpcId())
            {
                case 236230: // Immortal Orissan
                case 236233:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_IMMORTAL_03(), 2500);
                    break;
                case 236231: // Exhausted Orissan
                case 236234:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_IMMORTAL_05(), 2500);
                    ScheduleOrissansNextImmortality();
                    break;
                case 702719: // Empyrean Lord's Siege Weapon
                case 702720:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_WAVE_01(), 3000);
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_WAVE_02(), 6000);
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_WAVE_03(), 9000);
                    break;
                case 236204: // Flamesquelch Destroyer
                case 236205: // Flamesquelch Burnsmark
                case 236206: // Flamesquelch Rangerblaze
                case 236216:
                case 236217:
                case 236218:
                case 236219:
                case 236220:
                    StartWalking(npc, CreatureState.WALK_MODE);
                    break;
                case 236243: // Commander Virtsha
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_WAVE_08());
                    break;
                case 236247: // Dragon Beritra
                    // TODO: scheduleBeritrasDespawn();
                    Spawn(702699, 153.250f, 516.473f, 1751.0494f, (byte)0, 383); // Pillar remains
                    break;
                case 855460:
                case 855461:
                case 855462:
                case 855463:
                case 855464: // Drakenspire Protector
                case 855465:
                case 855466:
                case 855467:
                case 855468:
                case 855469:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_GUARDIAN_01());
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_GUARDIAN_02(), 3000);
                    break;
            }
        }
    }

    public override void OnDie(Npc npc)
    {
        base.OnDie(npc);
        int npcId = npc.GetNpcId();
        switch (npcId)
        {
            case 236225:
            case 236226:
                Npc oppositeProtector = GetNpc(npcId == 236225 ? 236226 : 236225);
                if (oppositeProtector == null || oppositeProtector.IsDead())
                    OnTwinsComplete();
                break;
            case 236227: // Lava Protector
            case 236228: // Heatvent Protector
                if (GetNpc(npcId == 236227 ? 855709 : 855708) != null)
                {
                    OnTwinsComplete();
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_TWIN_07());
                }
                else
                {
                    Spawn(npcId == 236227 ? 855708 : 855709, npc.GetX(), npc.GetY(), npc.GetZ(), (byte)0); // Sphere
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_TWIN_RESSURECT_02());
                    SetAdditionalEventTask(ThreadPoolManager.GetInstance().Schedule(OnTwinRespawn, 15000L));
                }
                break;
            case 236223: // Fetid Phantomscorch Chimera
            case 833012:
            case 833013:
            case 833015:
                return;
            case 236224: // Rapacious Kadena
                instance.SetDoorState(375, true);
                break;
            case 236661: // Rapacious Kadena
                instance.SetDoorState(376, true);
                break;
            case 236662: // Rapacious Kadena
                instance.SetDoorState(378, true);
                break;
            case 236231: // Exhausted Orissan
            case 236234:
                OnOrissanComplete();
                DeleteAliveNpcs(855607, 855608, 855699, 855700);
                break;
            case 236239: // Wave Commanders
            case 236240:
            case 236241:
            case 236242:
            case 236243:
                if (Interlocked.Increment(ref eventCounter) >= Volatile.Read(ref difficulty)) // difficulty is equivalent to the necessary kills
                    OnWaveEventComplete(true);
                break;
            case 236244: // Beritra Easy Mode
            case 236245: // Beritra Normal Mode
                WorldPosition pos = npc.GetPosition();
                Spawn(npcId == 236244 ? 833012 : 833013, pos.GetX(), pos.GetY(), pos.GetZ(), pos.GetHeading());
                SpawnExit();
                break;
            case 236246: // Beritra Hard Mode
            case 236247: // Beritra Dragon
                // TODO: spawn exit and NPCs
                SpawnExit();
                break;
            case 236248:
            case 236249:
                if (Interlocked.Decrement(ref waveSurvivors) == 0)
                    OnWaveEventComplete(false);
                break;
            // exploding flame 856459 skill use
            case 731580:
                RespawnService.ScheduleDecayTask(npc, 4000L);
                switch (npc.GetSpawn().GetStaticId())
                {
                    case 332:
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_WAVE_09());
                        break;
                    case 522:
                    case 603:
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_TWIN_08());
                        break;
                }
                break;
        }
    }

    public override void OnAggro(Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 236227: // Lava Protector
            case 236228: // Heatvent Protector
                if (Interlocked.CompareExchange(ref isStageActive, 1, 0) == 0)
                {
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_TWIN_01());
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_TWIN_02(), 2500);
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_TWIN_03(), 5000);
                    OnTwinsFightStart();
                }
                break;
        }
    }

    private void OnTwinsFightStart()
    {
        int twinProgressCount = 0;
        SetCurrentEventTask(ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            switch (++twinProgressCount)
            {
                case 2:
                    instance.SetDoorState(380, true); // one of the best doors which closes if you set it open
                    instance.SetDoorState(374, true);
                    instance.SetDoorState(377, true);
                    instance.SetDoorState(379, true);
                    instance.SetDoorState(391, true);
                    break;
                case 8:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_TWIN_04());
                    break;
                case 9:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_TWIN_05(), 5000);
                    break;
                case 10:
                    CancelTasks(currentEventTask, additionalEventTask);
                    OnTwinsFail();
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_TWIN_06());
                    break;
            }
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(30000L), System.TimeSpan.FromMilliseconds(10 * 1000L)));
    }

    /// <summary>
    /// Fifteen seconds after a twin fell, with its font still standing: the raid missed the window.
    /// </summary>
    /// <remarks>
    /// Retail marks that moment with a <b>failure display</b> — 855510 on the physical side and 855511
    /// on the magical — whose only job is to announce the time-over so the font can call its guards
    /// down. Nothing placed one, which is why <see cref="Aion.GameServer.Handlers.AI.TwinFontAI"/>'s
    /// handler had no sender. See docs/retail-ai-fidelity.md.
    /// </remarks>
    private void AnnounceTimeOver(int fontId)
    {
        Npc font = GetNpc(fontId);
        if (font == null)
            return;

        Spawn(fontId == 855708 ? 855510 : 855511, font.GetX(), font.GetY(), font.GetZ(), (byte)0);
    }

    private void OnTwinRespawn()
    {
        AnnounceTimeOver(855708);
        AnnounceTimeOver(855709);

        if (GetNpc(855708) != null)
            Spawn(236227, 531.0885f, 212.43806f, 1683.4116f, (byte)60);
        if (GetNpc(855709) != null)
            Spawn(236228, 530.8584f, 151.8681f, 1683.4116f, (byte)60);
        DeleteAliveNpcs(855708, 855709);
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_TWIN_RESSURECT_03());
    }

    private void OnTwinsComplete()
    {
        CancelTasks(currentEventTask, additionalEventTask);
        DeleteAliveNpcs(855708, 855709);

        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            Sp(race == Race.ELYOS ? 209690 : 209755, 543.35f, 149.81f, 1681.82f, (byte)0, "301390000_NPCPathFunction_Npc_Path03", 4000);
            Sp(race == Race.ELYOS ? 209690 : 209755, 543.45f, 208.97f, 1681.82f, (byte)0, "301390000_NPCPathFunction_Npc_Path01", 4000);
            Sp(race == Race.ELYOS ? 209693 : 209758, 543.43f, 155.08f, 1681.82f, (byte)0, "301390000_NPCPathFunction_Npc_Path04", 4000);
            Sp(race == Race.ELYOS ? 209693 : 209758, 543.43f, 214.24f, 1681.82f, (byte)0, "301390000_NPCPathFunction_Npc_Path02", 4000);
            ThreadPoolManager.GetInstance().Schedule(() =>
            {
                Spawn(race == Race.ELYOS ? 209695 : 209760, 582.61f, 183.52f, 1683.73f, (byte)0);
                Spawn(race == Race.ELYOS ? 209694 : 209759, 582.55f, 178.02f, 1683.73f, (byte)0);
            }, 16000L);
        }, 2000L);

        if (Volatile.Read(ref difficulty) == HARD_MODE)
            Spawn(236232, 812.0238f, 568.3602f, 1701.045f, (byte)92); // HM Orissan
        else
            Spawn(236229, 812.0238f, 568.3602f, 1701.045f, (byte)92); // NM Orissan

        Volatile.Write(ref isStageActive, 0);
    }

    private void OnTwinsFail()
    {
        if (Interlocked.CompareExchange(ref difficulty, NORMAL_MODE, HARD_MODE) == HARD_MODE)
        {
            DeleteAliveNpcs(236227, 236228, 855708, 855709);
            Spawn(236225, 531.0885f, 212.4381f, 1683.412f, (byte)60); // Fountless Lava Protector
            Spawn(236226, 530.8584f, 151.8681f, 1683.412f, (byte)60); // Fountless Heatvent Protector
        }
    }

    /// <summary>
    /// Immortality phases are always 90s + 10s buff duration, vice versa; his vulnerability phase differs.
    /// So let's handle only this phase.
    /// 1st = 120s; 2nd = 228s, 3rd = 228s, 4th endless
    /// </summary>
    private void ScheduleOrissansNextImmortality()
    {
        int delay;
        switch (Interlocked.Increment(ref eventCounter))
        {
            case 1:
                delay = 120000;
                break;
            case 2:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_IMMORTAL_06(), 5000);
                delay = 228000;
                break;
            case 3:
                int prevDiff = Volatile.Read(ref difficulty);
                switch (Interlocked.Exchange(ref difficulty, prevDiff - 2))
                {
                    case NORMAL_MODE:
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_IMMORTAL_07(), 5000);
                        break;
                    case EASY_MODE:
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_IMMORTAL_08(), 5000);
                        break;
                }
                delay = 240000; // Let's give them a few seconds more
                break;
            default:
                // Maybe retail has lowered the difficulty to easy mode at this point (if normal mode was present)
                switch (Volatile.Read(ref difficulty))
                {
                    case NORMAL_MODE:
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_IMMORTAL_09(), 5000);
                        break;
                    case EASY_MODE:
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_IMMORTAL_10(), 5000);
                        break;
                }
                return;
        }

        SetCurrentEventTask(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            Npc orissan = GetNpc(Volatile.Read(ref difficulty) == HARD_MODE ? 236234 : 236231);
            if (orissan != null) // should not happen
                Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(orissan, 21635, 1, orissan).UseSkill(); // Summon Crystal
        }, (long)delay));
    }

    private void OnOrissanComplete()
    {
        CancelTasks(currentEventTask);
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            Spawn(race == Race.ELYOS ? 209705 : 209770, 816.18f, 517.78f, 1707.41f, (byte)33);
            Spawn(race == Race.ELYOS ? 209705 : 209770, 820.82f, 517.82f, 1707.41f, (byte)33);
            Spawn(race == Race.ELYOS ? 209704 : 209769, 816.00f, 521.35f, 1706.88f, (byte)33);
            Spawn(race == Race.ELYOS ? 209704 : 209769, 820.48f, 521.49f, 1706.88f, (byte)33);

            Spawn(race == Race.ELYOS ? 209711 : 209776, 811.00f, 587.83f, 1701.045f, (byte)32);
            Spawn(race == Race.ELYOS ? 209709 : 209774, 807.50f, 583.54f, 1701.045f, (byte)32);
            Spawn(race == Race.ELYOS ? 209710 : 209775, 814.85f, 583.56f, 1701.045f, (byte)32);
            Spawn(race == Race.ELYOS ? 209708 : 209773, 811.11f, 582.62f, 1701.045f, (byte)32);
        }, 4000L);
        Volatile.Write(ref eventCounter, 0);
        Spawn(206405, 639.50f, 866.50f, 1600.88f, (byte)0);
    }

    private void OnWaveEventStart()
    {
        Sp(race == Race.ELYOS ? 209720 : 209785, 635.53f, 886.83f, 1600.72f, (byte)30, "301390000_NPCPathFunction_Npc_Path06",
            CreatureState.WALK_MODE, 7000);
        Spawn(race == Race.ELYOS ? 209731 : 209796, 639.05f, 895.86f, 1600.41f, (byte)30);
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            Sp(race == Race.ELYOS ? 209722 : 209787, 637.75f, 847.98f, 1599.94f, (byte)30, "301390000_NPCPathFunction_Npc_Path07", 2000);
            Sp(race == Race.ELYOS ? 209722 : 209787, 637.75f, 847.98f, 1599.94f, (byte)30, "301390000_NPCPathFunction_Npc_Path08", 2000);
            Sp(race == Race.ELYOS ? 209722 : 209787, 637.75f, 847.98f, 1599.94f, (byte)30, "301390000_NPCPathFunction_Npc_Path09", 2000);
            Sp(race == Race.ELYOS ? 209722 : 209787, 637.75f, 847.98f, 1599.94f, (byte)30, "301390000_NPCPathFunction_Npc_Path10", 2000);
            Sp(race == Race.ELYOS ? 209722 : 209787, 633.95f, 847.58f, 1599.87f, (byte)30, "301390000_NPCPathFunction_Npc_Path13", 2000);
            Sp(race == Race.ELYOS ? 209722 : 209787, 633.95f, 847.58f, 1599.87f, (byte)30, "301390000_NPCPathFunction_Npc_Path14", 2000);
            Sp(race == Race.ELYOS ? 209722 : 209787, 633.95f, 847.58f, 1599.87f, (byte)30, "301390000_NPCPathFunction_Npc_Path15", 2000);
            Sp(race == Race.ELYOS ? 209722 : 209787, 633.95f, 847.58f, 1599.87f, (byte)30, "301390000_NPCPathFunction_Npc_Path16", 2000);
            ThreadPoolManager.GetInstance().Schedule(() =>
            {
                DeleteAliveNpcs(race == Race.ELYOS ? 209720 : 209785);
                Spawn(race == Race.ELYOS ? 209723 : 209788, 636.09f, 879.98f, 1600.82f, (byte)33); // Main Npc
                Spawn(race == Race.ELYOS ? 702719 : 702720, 635.68f, 883.03f, 1603.91f, (byte)29); // Siege Weapon
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_WAVE_01(), 3000);
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_WAVE_02(), 6000);
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_WAVE_03(), 9000);
                ScheduleWaveStarts();
            }, 12000L);
        }, 18000L);
    }

    private void ScheduleWaveStarts()
    {
        int waveCount = 0;
        SetCurrentEventTask(ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            switch (++waveCount)
            {
                case 1:
                    instance.SetDoorState(267, true);
                    instance.SetDoorState(271, true);
                    Spawn(731581, 578.55f, 819.27f, 1609.42f, (byte)0, 84);
                    Spawn(731581, 690.49f, 822.41f, 1609.57f, (byte)0, 405);
                    Spawn(731581, 635.39f, 784.05f, 1596.72f, (byte)0, 548);
                    break;
                case 2:
                    if (Volatile.Read(ref difficulty) == HARD_MODE)
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_WAVE_05());
                    break;
                case 3:
                    instance.SetDoorState(7, true);
                    instance.SetDoorState(310, true);
                    Spawn(731581, 707.34f, 876.80f, 1603.69f, (byte)0, 398);
                    Spawn(731581, 570.70f, 877.51f, 1599.80f, (byte)0, 401);
                    if (Volatile.Read(ref difficulty) == HARD_MODE)
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_WAVE_06());
                    break;
                case 4:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_WAVE_07());
                    break;
                case 5:
                    instance.SetDoorState(210, true);
                    instance.SetDoorState(312, true);
                    Spawn(731581, 694.01f, 935.97f, 1618.09f, (byte)0, 399);
                    Spawn(731581, 572.94f, 940.04f, 1620.04f, (byte)0, 407);
                    break;
            }
            if (waveCount <= Volatile.Read(ref difficulty))
                ScheduleCommanderWave(waveCount);
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(6000L), System.TimeSpan.FromMilliseconds(60000L)));
    }

    private void ScheduleCommanderWave(int waveCount)
    {
        SetAdditionalEventTask(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            Sp(Rnd.Get(236216, 236220), 634.80f, 790.95f, 1596.80f, (byte)30, "301390000_Wave_Commander_Left", 0);
            Sp(Rnd.Get(236216, 236220), 634.80f, 790.95f, 1596.80f, (byte)30, "301390000_Wave_Commander_Right", 0);
            if (waveCount >= 3)
                Sp(Rnd.Get(236216, 236220), 634.80f, 790.95f, 1596.80f, (byte)30, "301390000_Wave_Commander_Middle", 0);
            if (waveCount >= 4)
            {
                Sp(Rnd.Get(236216, 236220), 634.80f, 790.95f, 1596.80f, (byte)30, "301390000_Wave_Commander_Left", 4000);
                Sp(Rnd.Get(236216, 236220), 634.80f, 790.95f, 1596.80f, (byte)30, "301390000_Wave_Commander_Right", 4000);
            }
            Sp(236243 - Volatile.Read(ref difficulty) + waveCount, 634.80f, 790.95f, 1596.80f, (byte)30, "301390000_Wave_Commander_Middle", 3000); // Boss
        }, 28000L));
    }

    private void OnWaveEventComplete(bool isSuccessful)
    {
        CancelTasks(currentEventTask, additionalEventTask);
        DeleteAliveNpcs(236204, 236205, 236206, 236216, 236217, 236218, 236219, 236220, 236239, 236240, 236241, 236242, 236243, 731581);
        if (isSuccessful)
        {
            Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(GetNpc(race == Race.ELYOS ? 702719 : 702720), 20838, 1, GetNpc(731580)).UseSkill();
            switch (Volatile.Read(ref waveSurvivors))
            {
                case 8:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_WAVE_BONUS_04());
                    break;
                case 7:
                case 6:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_WAVE_BONUS_03());
                    break;
                case 5:
                case 4:
                case 3:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_WAVE_BONUS_02());
                    break;
                case 2:
                case 1:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_WAVE_BONUS_01());
                    break;
            }
            switch (Volatile.Read(ref difficulty))
            {
                // case 5 -> spawn(236246, 152f, 518f, 1749.6f, (byte) 55); TODO: Implementation
                case HARD_MODE:
                case NORMAL_MODE:
                    Spawn(236245, 152.38f, 518.68f, 1749.6f, (byte)55);
                    break;
                case EASY_MODE:
                    Spawn(236244, 152.38f, 518.68f, 1749.6f, (byte)55);
                    break;
            }
        }
        else
        {
            DeleteAliveNpcs(702719, 702720);
            // TODO: find sys msg for fail
        }
    }

    private void SpawnExit()
    {
        Race? r = race;
        switch (Volatile.Read(ref difficulty))
        {
            case FAILED:
                Spawn(r == Race.ELYOS ? 209741 : 209806, 155.390f, 516.622f, 1750f, (byte)0); // Masionel | Parsia
                Spawn(r == Race.ELYOS ? 209742 : 209807, 155.361f, 520.999f, 1750f, (byte)0); // Detachment Entry Soldier
                break;
            case EASY_MODE:
                Spawn(r == Race.ELYOS ? 209740 : 209805, 155.390f, 516.622f, 1750f, (byte)0); // Masionel | Parsia
                Spawn(r == Race.ELYOS ? 209742 : 209807, 155.361f, 520.999f, 1750f, (byte)0); // Detachment Entry Soldier
                break;
            case NORMAL_MODE:
                Spawn(r == Race.ELYOS ? 209739 : 209804, 155.390f, 516.622f, 1750f, (byte)0); // Masionel | Parsia
                Spawn(r == Race.ELYOS ? 209742 : 209807, 155.361f, 520.999f, 1750f, (byte)0); // Detachment Entry Soldier
                break;
            case HARD_MODE:
                Spawn(r == Race.ELYOS ? 209738 : 209803, 140.656f, 518.922f, 1750f, (byte)0); // Masionel | Parsia
                Spawn(r == Race.ELYOS ? 209742 : 209807, 144.452f, 516.218f, 1749.4784f, (byte)0); // Detachment Entry Soldier
                Spawn(r == Race.ELYOS ? 209742 : 209807, 144.433f, 521.472f, 1749.4768f, (byte)0); // Detachment Entry Soldier
                break;
        }
        // Exits
        Spawn(731548, 320.005f, 183.281f, 1687.2552f, (byte)60); // Start Exit
        Spawn(731548, 545.194f, 153.283f, 1681.8224f, (byte)60); // Protector Exit
        Spawn(731548, 545.325f, 212.691f, 1681.8224f, (byte)60); // Protector Exit
        Spawn(731548, 810.652f, 591.986f, 1701.0449f, (byte)90); // Orissan Exit
        Spawn(731548, 137.524f, 518.576f, 1749.4153f, (byte)0); // Beritra Exit
    }

    public override void OnCreatureDetected(Npc detector, Creature detected)
    {
        if (!(detected is Player))
            return;
        if (detector.GetNpcId() == 206405)
        {
            if (Interlocked.CompareExchange(ref isStageActive, 1, 0) == 0)
            {
                OnWaveEventStart();
                detector.GetController().Delete();
            }
        }
    }

    public override void OnStartEffect(Effect effect)
    {
        switch (effect.GetSkillId())
        {
            case 21635:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_IMMORTAL_01()); // Summon Crystal
                break;
            case 21885:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_IMMORTAL_04()); // Weaken Ascension Domination
                break;
            case 21610: // Dark Affinity
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_VRITRA_HUMAN_01());
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_VRITRA_HUMAN_02(), 3000);
                break;
        }
    }

    public override void OnEndEffect(Effect effect)
    {
        switch (effect.GetSkillId())
        {
            case 21635: // Weaken Ascension Domination
            case 21885:
                int nextId;
                if (effect.GetSkillId() == 21635)
                    nextId = Volatile.Read(ref difficulty) == HARD_MODE ? 236233 : 236230;
                else
                    nextId = Volatile.Read(ref difficulty) == HARD_MODE ? 236234 : 236231;
                WorldPosition pos = effect.GetEffected().GetPosition();
                effect.GetEffected().GetController().Delete();
                Spawn(nextId, pos.GetX(), pos.GetY(), pos.GetZ(), pos.GetHeading());
                break;
            case 21618: // Dragon Lord's Authority
                // TODO: sendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_VRITRA_HUMAN_03()); => only if it ends naturally
                break;
        }
    }

    public override void LeaveInstance(Player player)
    {
        TeleportService.MoveToInstanceExit(player, mapId, player.GetRace());
    }

    public override bool OnReviveEvent(Player player)
    {
        PlayerReviveService.Revive(player, 25, 25, false, 0);
        player.GetGameStats().UpdateStatsAndSpeedVisually();
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME());
        TeleportService.TeleportTo(player, instance, 361.706f, 182.503f, 1684.290f, (byte)0);
        return true;
    }

    private readonly object taskLock = new();

    private void SetCurrentEventTask(ScheduledTask task)
    {
        lock (taskLock)
        {
            CancelTasks(currentEventTask);
            currentEventTask = task;
        }
    }

    private void SetAdditionalEventTask(ScheduledTask task)
    {
        lock (taskLock)
        {
            CancelTasks(additionalEventTask);
            additionalEventTask = task;
        }
    }

    private void CancelTasks(params ScheduledTask[] tasks)
    {
        lock (taskLock)
        {
            foreach (ScheduledTask task in tasks)
                if (task != null && !task.IsDone())
                    task.Cancel(true);
        }
    }

    private void Sp(int id, float x, float y, float z, byte h, string walkerId, int delay)
    {
        Sp(id, x, y, z, h, walkerId, CreatureState.ACTIVE, delay);
    }

    private void Sp(int id, float x, float y, float z, byte h, string walkerId, CreatureState state, int delay)
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            Npc npc = (Npc)Spawn(id, x, y, z, h);
            npc.GetSpawn().SetWalkerId(walkerId);
            StartWalking(npc, state);
        }, (long)delay);
    }

    private void StartWalking(Npc npc, CreatureState state)
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (npc == null)
                return;
            WalkManager.StartWalking((NpcAI)npc.GetAi());
            if (state == CreatureState.ACTIVE)
            {
                npc.UnsetState(CreatureState.WALK_MODE);
                npc.SetState(state);
                PacketSendUtility.BroadcastPacket(npc, new SM_EMOTION(npc, EmotionType.CHANGE_SPEED));
            }
        }, 2000L);
    }

    public override bool IsBoss(Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 236227:
            case 236228:
            case 236231:
            case 236234:
            case 236244:
            case 236245:
            case 236247:
                return true;
            default:
                return false;
        }
    }
}
