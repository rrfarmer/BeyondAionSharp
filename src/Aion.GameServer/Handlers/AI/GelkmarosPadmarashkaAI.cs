using System;
using System.Threading;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/worlds/gelkmaros/PadmarashkaAI (Estrayl), with her rockfall replaced by retail
/// pattern <c>DF4_Dramata</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Everything Java does - the protective slumber, the
/// four shield NPCs that break it, the stat overrides, the berserk at 5% - is untouched. What changed
/// is the rocks, which were an invention: <b>forty of them in a ring around a fixed point</b>, once,
/// at 10% health, with no lifetime.
/// <para>
/// Retail drops rocks <b>on the players</b>, capped, for twelve seconds, and each one engages whoever
/// it landed on. There are five sources, and they are not one mechanic at one threshold:
/// </para>
/// <list type="table">
/// <item><term>opening step</term><description>three <b>B</b> rocks, once, on the third heartbeat
/// tick</description></item>
/// <item><term>every 90s from then</term><description>three B rocks (timer 17)</description></item>
/// <item><term>every 90s from the first tick</term><description>four B rocks (timers 6 and 7, a
/// ping-pong pair)</description></item>
/// <item><term>below 10%</term><description><b>fifteen</b> rocks at once - three draws of five - and
/// four more every 90s afterwards (timers 2 and 3)</description></item>
/// <item><term>below 5%</term><description>fifteen more, once</description></item>
/// </list>
/// <para>
/// <b>The heartbeat is a ladder, not a loop.</b> Timer 0 re-arms every five seconds and its branches
/// are one-shot steps guarded by flag vars, so the fight walks down them: the first tick opens the
/// long-cycle chains, the second a chain that is all casts, the third the opening rockfall. That
/// ordering is why the first rocks land fifteen seconds in rather than immediately.
/// </para>
/// <para>
/// <b>Not translated.</b> Every branch here also casts a <c>SKILLI_INDEX</c> - fourteen distinct
/// indices across the pattern against a much shorter skill list - so the casts and the timers that
/// carry nothing else (1, 5, 9, 10, 11, 12, 15, 16, 20, 25, 26, 27, 28, 29) are left out, as are the
/// waypoint egg-laying branches, the abnormal-state handlers and the messages. Each rock step also has
/// a fifty-percent twin that differs only in which system message it prints; the mechanic is
/// identical, so one branch translates both.
/// </para>
/// </remarks>
[AIName("padmarashka_world_boss")]
public class GelkmarosPadmarashkaAI : PatternAi, HpPhases.PhaseHandler
{
    /// <summary>
    /// The berserk step at 5%, which is ours rather than retail's and stays. The 10% rockfall that used
    /// to hang here belongs to the pattern now.
    /// </summary>
    private readonly HpPhases hpPhases = new HpPhases(5);
    private readonly AtomicInteger deadProtectors = new AtomicInteger();

    /// <summary><c>BDF4_DramataRock_57_An</c>, the heavy rock of the two low-health bursts.</summary>
    private const int Rock = 281936;

    /// <summary><c>BDF4_DramataRock_B_57_An</c>, the one every earlier chain drops.</summary>
    private const int RockB = 282140;

    /// <summary>Retail's <c>SPAWN_ID_2</c>: leaving the fight clears exactly this group.</summary>
    private const int Rocks = 2;

    private const int RockLife = 12;

    /// <summary>Retail's <c>valid_distance</c> - the whole arena, so it is a cap and not a radius.</summary>
    private const float Reach = 150f;

    /// <summary>Retail's <c>hatepoints_to_add</c>, with <c>attack_target_after_spawn</c>.</summary>
    private const int OnArrival = 1;

    // Retail's flag vars, in the order it names them. Only the five that gate a step we translate are
    // used; the numbering is kept so the table reads against the digest.
    private const int Alpha1 = 0;
    private const int Beta1 = 5;
    private const int Gamma1 = 10;
    private const int Gamma4 = 13;
    private const int Epsilon1 = 20;

    private const int HeartbeatMillis = 5000;

    private static PatternAction Fall(int npcId, int cap) =>
        Do.SpawnOnEachTarget(npcId, Rocks, Reach, maxTargets: cap, MultiTargetOrder.Random,
            liveSeconds: RockLife, attackHate: OnArrival);

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = AiPattern.Of(
            AiPattern.Branch(11, "SetTimer", When.Always,
                Do.ArmTimer(0, HeartbeatMillis))),

        OnBattleTimer = AiPattern.Of(
            // Below five: fifteen rocks in one breath, once.
            AiPattern.Branch(90, "kcast13", [When.Timer(0), When.HpBelow(5), When.FirstTime(Alpha1)],
                Do.ArmTimer(0, HeartbeatMillis),
                Fall(Rock, 5), Fall(Rock, 5), Fall(Rock, 5)),

            // Below ten: the same burst, and it opens the timer-2 chain that keeps going afterwards.
            AiPattern.Branch(88, "kcast13", [When.Timer(0), When.HpBelow(10), When.FirstTime(Beta1)],
                Do.ArmTimer(0, HeartbeatMillis),
                Do.ArmTimer(2, 30000),
                Fall(Rock, 5), Fall(Rock, 5), Fall(Rock, 5)),

            AiPattern.Branch(80, "", [When.Timer(2)],
                Do.ArmTimer(3, 45000),
                Fall(Rock, 4)),
            AiPattern.Branch(78, "", [When.Timer(3)], Do.ArmTimer(2, 45000)),

            // First heartbeat tick: opens the timer-6 chain.
            AiPattern.Branch(60, "", [When.Timer(0), When.FirstTime(Gamma1)],
                Do.ArmTimer(0, HeartbeatMillis),
                Do.ArmTimer(6, 45000)),

            AiPattern.Branch(55, "", [When.Timer(6)],
                Do.ArmTimer(7, 45000),
                Fall(RockB, 4)),
            AiPattern.Branch(53, "", [When.Timer(7)], Do.ArmTimer(6, 45000)),

            // Second tick: a step that is all casts. Kept because it consumes a heartbeat, which is why
            // the opening rockfall lands on the third tick rather than the second.
            AiPattern.Branch(40, "", [When.Timer(0), When.FirstTime(Gamma4)],
                Do.ArmTimer(0, HeartbeatMillis)),

            // Third tick: the opening rockfall, and the timer-17 chain that repeats it.
            AiPattern.Branch(20, "kcast13", [When.Timer(0), When.FirstTime(Epsilon1)],
                Do.ArmTimer(0, HeartbeatMillis),
                Do.ArmTimer(17, 90000),
                Fall(RockB, 3)),

            AiPattern.Branch(13, "kcast13", [When.Timer(17)],
                Do.ArmTimer(17, 90000),
                Fall(RockB, 3)),

            // The heartbeat itself, once every step above has been consumed.
            AiPattern.Branch(1, "", [When.Timer(0)], Do.ArmTimer(0, HeartbeatMillis))),

        // Retail's on_leave_attack_state clears SPAWN_ID_1, _2 and _3; _2 is the rocks.
        OnLeaveAttack = AiPattern.Of(
            AiPattern.Branch(7, "", When.Always, Do.Despawn(Rocks))),

        OnDie = AiPattern.Of(
            AiPattern.Branch(5, "", When.Always, Do.Despawn(Rocks))),
    };

    protected override AiPattern Pattern => Pattern_;

    public GelkmarosPadmarashkaAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(19186, GetOwner(), GetOwner());
            return ValueTask.CompletedTask;
        }, 3000L);
        SpawnShieldNpcs();
    }

    public override void ModifyOwnerStat(Stat2 stat)
    {
        switch (stat.GetStat())
        { // Tweak for 12p (600 s | 3000 dps)
            case StatEnum.MAXHP:
                stat.SetBase(20_880_000);
                break;
            case StatEnum.PHYSICAL_ATTACK:
                stat.SetBase(2200);
                break;
        }
    }

    private void SpawnShieldNpcs()
    {
        SpawnAndObserveNpc(281938, 2906.05f, 865.15f, 35.289f, (byte)107);
        SpawnAndObserveNpc(281939, 2920.70f, 878.94f, 35.289f, (byte)94);
        SpawnAndObserveNpc(281940, 2952.03f, 878.61f, 35.266f, (byte)81);
        SpawnAndObserveNpc(281941, 2963.97f, 859.07f, 35.289f, (byte)69);
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 5:
                GetOwner().QueueSkill(18730, 1, 3000); // Berserk State
                break;
        }
    }

    public override void OnEffectEnd(Effect effect)
    {
        if (effect.GetSkillId() == 19186)
            PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_DF4_DRAMATA_AWAKENING());
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        hpPhases.Reset();
        DespawnNpcs(Rock, RockB);
    }

    protected override void HandleDespawned()
    {
        DespawnNpcs(281938, 281939, 281940, 281941, Rock, RockB);
        base.HandleDespawned();
    }

    private void DespawnNpcs(params int[] npcIds)
    {
        GetOwner().GetWorldMapInstance().GetNpcs(npcIds).ForEach(npc => npc.GetController().Delete());
    }

    private void HandleObservedNpcDied(Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 281938:
            case 281939:
            case 281940:
            case 281941:
                if (deadProtectors.IncrementAndGet() >= 4)
                    GetOwner().GetEffectController().RemoveEffect(19186); // Protective Slumber
                break;
        }
    }

    private void SpawnAndObserveNpc(int npcId, float x, float y, float z, byte h)
    {
        Npc npc = (Npc)Spawn(npcId, x, y, z, (sbyte)h);
        npc.GetObserveController().Attach(new DeathObserver(_ => HandleObservedNpcDied(npc)));
    }
}
