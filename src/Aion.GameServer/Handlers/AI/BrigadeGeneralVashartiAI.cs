using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/rentusBase/BrigadeGeneralVashartiAI (@author xTz, Yeats, Estrayl).</summary>
[AIName("brigade_general_vasharti")]
public class BrigadeGeneralVashartiAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    /// <summary>
    /// Retail thresholds, from pattern IDYun_Nmd6: three steps at 86/56/26, where we had four at an
    /// invented 75/50/25/10. Every step does the same thing here, so there is no per-percent branch to
    /// follow the renumbering.
    /// </summary>
    /// <remarks>
    /// Retail also spawns a Glove Controller at each step. Those NPCs (283002/283004/283006) exist but
    /// are plain aggressive clones of Vasharti himself with no controller AI, so spawning them would put
    /// three extra full-strength bosses in the room rather than retail's controllers — harder than
    /// retail, not closer to it. They wait for their own AI. See docs/retail-ai-fidelity.md.
    /// </remarks>
    private readonly HpPhases hpPhases = new HpPhases(86, 56, 26);

    /// <summary>
    /// The two flames he lights when the fight starts, and where retail puts them.
    /// </summary>
    /// <remarks>
    /// Retail-sourced; see docs/retail-ai-fidelity.md. His reflect alternates red and blue on a 40s
    /// timer and players have to stand in the matching flame, so without these the mechanic has no
    /// board to play on. <c>DancingFlameAI</c> was already written for them; nothing had ever spawned
    /// one. Only 217313 is live on this pattern, so the fixed coordinates are unambiguous.
    /// </remarks>
    private const int DancingRedFlame = 282996;
    private const int DancingBlueFlame = 282997;

    /// <summary>Hard mode only (236300): the two illusions of himself he conjures beside the flames.</summary>
    /// <remarks>
    /// Retail-sourced from <c>IDYun_Nmd6_Hard</c>. Timer 6 is armed at 23s and re-arms every 75s,
    /// placing a kiss of fire and a kiss of ice at fixed points — each next to the flame of its own
    /// colour, which is the hard-mode twist on a fight that is already about picking the right one.
    /// Neither was spawned by anything. Normal mode has no such branch.
    /// <para>
    /// Their headings come from the pattern's <c>dir</c> in degrees, through the engine's own
    /// <c>ConvertAngleToHeading</c> rather than by hand.
    /// </para>
    /// </remarks>
    private const int HardModeVasharti = 236300;
    private const int KissOfFire = 856338;
    private const int KissOfIce = 856339;
    private static readonly TimeSpan FirstIllusion = TimeSpan.FromSeconds(23);
    private static readonly TimeSpan IllusionInterval = TimeSpan.FromSeconds(75);
    private readonly AtomicBoolean isHome = new AtomicBoolean(true);
    private readonly AtomicBoolean isInFlameShowerEvent = new AtomicBoolean();
    private ScheduledTask? enrageSchedule, flameShieldBuffSchedule, seaOfFireSpawnTask, illusionTask;

    public BrigadeGeneralVashartiAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isHome.CompareAndSet(true, false))
        {
            GetPosition().GetWorldMapInstance().SetDoorState(70, false);
            LightTheFlames();
            if (GetNpcId() == HardModeVasharti)
                StartIllusions();
            enrageSchedule = ThreadPoolManager.GetInstance().Schedule(_ => { HandleEnrageEvent(); return ValueTask.CompletedTask; }, (long)System.TimeSpan.FromMinutes(10).TotalMilliseconds);
            ScheduleFlameShieldBuffEvent(5000);
        }
        if (!isInFlameShowerEvent.Get())
            hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        CancelTasks(flameShieldBuffSchedule);
        GetOwner().ClearQueuedSkills();
        GetOwner().QueueSkill(20532, 1, 10000); // off (skill name)
    }

    private void ScheduleFlameShieldBuffEvent(int delay)
    {
        flameShieldBuffSchedule = ThreadPoolManager.GetInstance().Schedule(_ => { GetOwner().QueueSkill(20530 + Rnd.Get(0, 1), 60); return ValueTask.CompletedTask; }, (long)delay);
    }

    private void HandleEnrageEvent()
    {
        GetOwner().ClearQueuedSkills();
        GetOwner().QueueSkill(19962, 1, 15000); // Purple Flame Weapon
        GetOwner().QueueSkill(19907, 1, 0); // Chastise
    }

    /// <summary>The glove point: where retail’s controller stands and drops its wall.</summary>
    private const float GloveX = 188.33f;
    private const float GloveY = 414.61f;
    private const float GloveZ = 260.61f;

    /// <summary><c>Env_RedWall</c>, <c>Env_BlueWall</c>, <c>Env_BurningGround</c>.</summary>
    private const int RedWall = 283010;
    private const int BlueWall = 283011;
    private const int BurningGround = 283012;

    /// <summary>Retail’s <c>live_time</c> on each: forty on the two walls, forty-five on the ground.</summary>
    private const int WallLife = 40;
    private const int BurningGroundLife = 45;

    /// <summary><c>IDYun_Vasharti_Glove_Buffer</c>, and retail’s forty seconds for it.</summary>
    private const int GloveBuffer = 283007;
    private const int GloveBufferLife = 40;

    /// <summary><c>Glove_AreaAtk_Red</c> and <c>_Blue</c>, with retail’s six-second life.</summary>
    private const int SmashRed = 283008;
    private const int SmashBlue = 283009;
    private const int SmashLife = 6;

    /// <summary>Retail’s <c>spawn_range</c> on the two kinds of drop, and its <c>valid_distance</c>.</summary>
    private const float TargetedSpread = 5f;
    private const float AreaSpread = 35f;
    private const float TargetReach = 100f;

    /// <summary>
    /// One rung of retail’s glove ladder: how many players are picked, how many land at the glove
    /// point, and how long until the next rung.
    /// </summary>
    private readonly record struct GloveRung(
        int TargetedRed, int TargetedBlue, int AreaRed, int AreaBlue, long NextMillis);

    /// <summary>
    /// Retail’s sixteen rungs, read straight off <c>IDYun_Vasharti_Glove_ControllerA</c>.
    /// </summary>
    /// <remarks>
    /// Every rung carries its own test-and-set flag var, so the ladder runs once through and in order —
    /// five triples of "pick players, then rain red, then rain blue", with the number of players picked
    /// climbing from two to three across the five, and a bare rung at the end that dispels and leaves.
    /// The delays sum to thirty-eight seconds, which is why the controller lives forty.
    /// <para>
    /// <b>What stood here was a fixed-rate task</b> dropping fourteen, nineteen or twenty-four smashes
    /// every 7.1 seconds around the boss, all of them area drops — so the half of the mechanic that
    /// puts a smash under a named player did not exist, and the escalation across the five triples did
    /// not either.
    /// </para>
    /// </remarks>
    private static readonly GloveRung[] GloveLadder =
    [
        new GloveRung(2, 2, 0, 0, 3000), new GloveRung(0, 0, 3, 0, 1000), new GloveRung(0, 0, 0, 3, 2000),
        new GloveRung(2, 2, 0, 0, 3000), new GloveRung(0, 0, 3, 0, 1000), new GloveRung(0, 0, 0, 3, 2000),
        new GloveRung(2, 3, 0, 0, 3000), new GloveRung(0, 0, 3, 0, 1000), new GloveRung(0, 0, 0, 3, 2000),
        new GloveRung(3, 3, 0, 0, 3000), new GloveRung(0, 0, 3, 0, 1000), new GloveRung(0, 0, 0, 3, 2000),
        new GloveRung(3, 3, 0, 0, 3000), new GloveRung(0, 0, 3, 0, 1000), new GloveRung(0, 0, 0, 3, 4000),
    ];

    /// <summary>Retail’s first <c>add_battle_timer</c>, before rung one.</summary>
    private const long GloveOpeningMillis = 4000L;

    private void HandleSeaOfFireEvent()
    {
        int percent = GetLifeStats().GetHpPercentage();

        // Retail runs this from three controllers, A at 86, C at 56 and E at 26, and the only thing that
        // differs between them is which wall they drop. Reading the boss health picks the same one.
        int wall = percent <= 70 ? percent <= 40 ? BurningGround : BlueWall : RedWall;
        int wallLife = wall == BurningGround ? BurningGroundLife : WallLife;

        SpawnFor(wall, GloveX, GloveY, GloveZ, unchecked((sbyte)244), wallLife);
        SpawnFor(GloveBuffer, GloveX, GloveY, GloveZ, (sbyte)0, GloveBufferLife);

        ClimbGloveLadder(0);
    }

    /// <summary>Runs one rung of retail’s ladder and arms the next.</summary>
    private void ClimbGloveLadder(int rung)
    {
        seaOfFireSpawnTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (IsDead() || rung >= GloveLadder.Length)
                return ValueTask.CompletedTask;

            GloveRung step = GloveLadder[rung];
            DropOnPlayers(SmashRed, step.TargetedRed);
            DropOnPlayers(SmashBlue, step.TargetedBlue);
            DropAtGlove(SmashRed, step.AreaRed);
            DropAtGlove(SmashBlue, step.AreaBlue);

            ClimbGloveLadder(rung + 1);
            return ValueTask.CompletedTask;
        }, rung == 0 ? GloveOpeningMillis : GloveLadder[rung - 1].NextMillis);
    }

    /// <summary>
    /// Retail <c>spawn_on_multi_target</c>: pick that many attackers at random inside a hundred metres
    /// and drop one smash within five metres of each.
    /// </summary>
    private void DropOnPlayers(int npcId, int targets)
    {
        if (targets <= 0)
            return;

        List<Creature> picked = GetOwner().GetAggroList().StreamValidTargets(TargetReach).ToList();
        for (int i = picked.Count - 1; i > 0; i--)
        {
            int j = Rnd.Get(0, i);
            (picked[i], picked[j]) = (picked[j], picked[i]);
        }

        foreach (Creature target in picked.Take(targets))
        {
            SpawnFor(npcId, target.GetX(), target.GetY(), target.GetZ(), (sbyte)0, SmashLife);
        }
    }

    /// <summary>Retail plain <c>spawn</c> at the controller own point, spread over thirty-five metres.</summary>
    private void DropAtGlove(int npcId, int count)
    {
        for (int i = 0; i < count; i++)
            RndSpawnInRange(npcId, 0, AreaSpread);
    }

    public override void OnStartUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 20534:
                HandleSeaOfFireEvent();
                break;
        }
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 19907: // repeat until reset
                GetOwner().QueueSkill(19907, 1, 0); // Chastise
                break;
            case 20530:
            case 20531:
                WorldMapInstance instance = GetPosition().GetWorldMapInstance();
                if (instance != null)
                {
                    if (instance.GetNpc(283000) == null)
                        Spawn(283000, 171.330f, 417.57f, 261f, (sbyte)116);
                    if (instance.GetNpc(283001) == null)
                        Spawn(283001, 205.280f, 410.53f, 261f, (sbyte)56);
                }
                ScheduleFlameShieldBuffEvent(33000);
                break;
            case 20532:
                EmoteManager.EmoteStopAttacking(GetOwner());
                GetOwner().ClearQueuedSkills();
                ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    WalkManager.StartForcedWalking(this, 188.17f, 414.06f, 260.75488f);
                    GetOwner().SetState(CreatureState.ACTIVE, true);
                    PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetObjectId()));
                    return ValueTask.CompletedTask;
                }, 800L);
                break;
            case 20533:
                SetStateIfNot(AIState.FIGHT);
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20534, 1, GetOwner()).UseSkill(); // Sea of Fire
                break;
        }
    }

    public override void OnEffectEnd(Effect effect)
    {
        if (effect != null && effect.GetSkillId() == 20534 && isInFlameShowerEvent.CompareAndSet(true, false))
        {
            CancelTasks(seaOfFireSpawnTask);
            // Retail on_despawn is a despawn_all, so the wall goes with the controller. Java reads
            // getNpcId() here -- the BOSS id, which is 217313 or 236300 and matches none of these --
            // so no wall was ever deleted and each one stood for the rest of the instance. They now
            // carry retail own live_time as well, so an event that ends abnormally still clears them.
            GetKnownList().ForEachNpc(n =>
            {
                switch (n.GetNpcId())
                {
                    case RedWall:
                    case BlueWall:
                    case BurningGround:
                        n.GetController().Delete();
                        break;
                }
            });
            ScheduleFlameShieldBuffEvent(10000);
            GetOwner().GetAggroList().AddHate((Creature)GetTarget(), 1000);
        }
    }

    public override bool IsDestinationReached()
    {
        if (GetState() == AIState.FORCED_WALKING && PositionUtil.GetDistance(GetOwner().GetX(), GetOwner().GetY(), 188.17f, 414.06f) <= 1f
            && isInFlameShowerEvent.CompareAndSet(false, true))
        {
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20533, 1, GetOwner()).UseSkill(); // off (skill name)
        }
        return base.IsDestinationReached();
    }

    /// <summary>Places the red and blue flames at the two points retail lights them.</summary>
    private void LightTheFlames()
    {
        Spawn(DancingRedFlame, 167.6f, 418.22f, 262.54f, (sbyte)0);
        Spawn(DancingBlueFlame, 208.58f, 410.71f, 262.54f, (sbyte)0);
    }

    /// <summary>Conjures the two illusions, and keeps conjuring them every 75 seconds.</summary>
    private void StartIllusions()
    {
        illusionTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
                CancelTasks(illusionTask);
            else
                ConjureIllusions();
            return ValueTask.CompletedTask;
        }, FirstIllusion, IllusionInterval);
    }

    private void ConjureIllusions()
    {
        Spawn(KissOfIce, 205.28f, 410.53f, 261f, (sbyte)PositionUtil.ConvertAngleToHeading(56f));
        Spawn(KissOfFire, 171.33f, 417.57f, 261f, (sbyte)PositionUtil.ConvertAngleToHeading(116f));
    }

    private void ClearSpawns()
    {
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        if (instance != null)
        {
            DeleteNpcs(instance.GetNpcs(283002));
            DeleteNpcs(instance.GetNpcs(283003));
            DeleteNpcs(instance.GetNpcs(283004));
            DeleteNpcs(instance.GetNpcs(283005));
            DeleteNpcs(instance.GetNpcs(283006));
            DeleteNpcs(instance.GetNpcs(283007));
            DeleteNpcs(instance.GetNpcs(283010));
            DeleteNpcs(instance.GetNpcs(283011));
            DeleteNpcs(instance.GetNpcs(283012));
            DeleteNpcs(instance.GetNpcs(283000));
            DeleteNpcs(instance.GetNpcs(283001));
            DeleteNpcs(instance.GetNpcs(DancingRedFlame));
            DeleteNpcs(instance.GetNpcs(DancingBlueFlame));
            DeleteNpcs(instance.GetNpcs(KissOfFire));
            DeleteNpcs(instance.GetNpcs(KissOfIce));
        }
    }

    private void DeleteNpcs(List<Npc> npcs)
    {
        npcs.Where(npc => npc != null).ToList().ForEach(npc => npc.GetController().Delete());
    }

    private void CancelTasks(params ScheduledTask?[] tasks)
    {
        foreach (ScheduledTask? task in tasks)
            if (task != null && !task.IsCancelled)
                task.Cancel(true);
    }

    protected override void HandleDespawned()
    {
        CancelTasks(enrageSchedule, flameShieldBuffSchedule, seaOfFireSpawnTask, illusionTask);
        ClearSpawns();
        base.HandleDespawned();
    }

    protected override void HandleBackHome()
    {
        isHome.Set(true);
        GetPosition().GetWorldMapInstance().SetDoorState(70, true);
        CancelTasks(enrageSchedule, flameShieldBuffSchedule, seaOfFireSpawnTask, illusionTask);
        ClearSpawns();
        base.HandleBackHome();
        hpPhases.Reset();
    }

    protected override void HandleDied()
    {
        GetPosition().GetWorldMapInstance().SetDoorState(70, true);
        CancelTasks(enrageSchedule, flameShieldBuffSchedule, seaOfFireSpawnTask, illusionTask);
        PacketSendUtility.BroadcastMessage(GetOwner(), 1500410);
        ClearSpawns();
        base.HandleDied();
    }
}
