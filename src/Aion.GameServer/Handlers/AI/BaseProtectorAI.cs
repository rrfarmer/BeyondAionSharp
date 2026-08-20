using Aion.GameServer.Ai;
using Aion.GameServer.Utils;
using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.Base;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Templates.Spawns.Basespawns;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The village chiefs and Advance guards. Retail patterns <c>LDF5_Village_chief01</c>..<c>19</c> and
/// <c>LDF4_Advance_village_01</c>..<c>37</c>.
/// </summary>
/// <remarks>
/// @author Estrayl. Retail-sourced addition below; see docs/retail-ai-fidelity.md and
/// <c>tools/client-extract/audit_npc_call_family.py</c>.
/// <para>
/// <b>These answer the fortress killer, and here they did not.</b> Both families carry the same
/// <c>on_message</c> rung — <c>is_message 30001</c>, then
/// <c>add_hate_point target=OBJI_MESSAGE_SENDER point_to_add=1000000</c> — so a killer broadcasting as
/// it wakes pulls every village guard within fifty metres onto itself. 113 npcs run one of these two
/// patterns and none of them heard it.
/// </para>
/// <para>
/// <b>This is the pairing the mechanic was designed around.</b> The abyss artifact killers share
/// <c>GUARD_DRAGON</c> with a third of the protectors they call, and those correctly ignore them. The
/// Advance killers are <c>LDF4_ADVANCE_DRGUARD</c>, whose tribe lists <c>LDF4_ADVANCE_LGUARD</c> and
/// <c>LDF4_ADVANCE_DGUARD</c> as <c>aggro</c> — the guards this class runs. Hostile by design, in the
/// client's own <c>npc_tribe_relation.xml</c> and in ours.
/// </para>
/// <para>
/// <b>Retail adds no <c>is_enemy</c> guard on this rung</b>, unlike the artifact protectors' version of
/// it, and none is added here. The aggro list applies its own tribe test either way; writing one in
/// would be inventing a condition the pattern does not have.
/// </para>
/// <para>
/// <b>And a guard under attack calls a killer to itself.</b> Both families arm a battle timer at
/// <b>5000</b> as they enter combat, broadcast <c>30002</c> naming themselves, and repeat that
/// broadcast every five seconds for as long as the fight lasts. The killer answers 30002 by coming for
/// whoever sent it, so this is the return leg: the guards answer a killer that wakes, and a guard being
/// fought calls one over. The <b>range differs between the two families</b> — twenty metres for a
/// village chief, fifty for an Advance guard — and that is retail's own split, not a rounding.
/// </para>
/// <para>
/// <b>Not translated:</b> the Advance guards' <c>30004</c>, a separate broadcast they make once on
/// entering combat and which nothing in the dump answers, so its audience is unknown. And the village
/// chiefs' <c>on_die</c> rung: <c>30003</c> at fifty metres plus two
/// <c>set_condition_spawn_variable</c> calls recording which of <c>pc_light</c>, <c>pc_dark</c> or
/// <c>drakan</c> killed it, each setting a different value — the condition-variable mechanism this port
/// has no equivalent for.
/// </para>
/// </remarks>
[AIName("base_protector")]
public class BaseProtectorAI : AggressiveNpcAI, INpcMessageListener
{
    /// <summary>Retail's killer-wakes call, and the value both families answer it with.</summary>
    public const int KillerAwake = 30001;
    public const int DropEverything = 1_000_000;

    /// <summary>Retail's <c>30002</c>: "come and deal with me".</summary>
    public const int CallTheKiller = 30002;

    /// <summary>
    /// Retail's <c>range_as_meter</c> on that broadcast, which differs by family: a village chief calls
    /// twenty metres, an Advance guard fifty.
    /// </summary>
    public const float ChiefCallRange = 20f;
    public const float GuardCallRange = 50f;

    /// <summary>Retail arms the timer at 5000 and every rung re-arms it at 5000.</summary>
    public const long CallRepeatMillis = 5000L;

    private readonly AtomicBoolean calling = new AtomicBoolean();
    private ScheduledTask? callTask;

    /// <summary>Which of the two families this npc belongs to, by its tribe.</summary>
    public static float CallRangeFor(TribeClass tribe) =>
        tribe is TribeClass.LDF5_V_CHIEF_L or TribeClass.LDF5_V_CHIEF_D or TribeClass.LDF5_V_CHIEF_DR
            ? ChiefCallRange
            : GuardCallRange;

    /// <summary>
    /// Retail's <c>on_message</c> rung: hate on the <b>sender</b>, not on anything it names.
    /// </summary>
    public void OnNpcMessage(Npc sender, int messageType, VisibleObject? param)
    {
        if (messageType != KillerAwake || IsDead() || sender == GetOwner())
            return;

        SummonOrder.Take(GetOwner(), sender, DropEverything);
    }

    /// <summary>
    /// Retail's <c>on_enter_attack_state</c>: broadcast at once, then every five seconds.
    /// </summary>
    protected override void HandleCreatureAggro(Creature creature)
    {
        base.HandleCreatureAggro(creature);
        if (!calling.CompareAndSet(false, true))
            return;

        float range = CallRangeFor(GetOwner().GetObjectTemplate().GetTribe());

        // Retail's rung broadcasts and *then* arms the timer, so the first call is part of entering
        // combat rather than the timer's first firing. Scheduling it at zero instead would leave the
        // call waiting on a clock tick, which is a killer standing next to a fight it has not been
        // told about.
        NpcMessageBus.Broadcast(GetOwner(), CallTheKiller, GetOwner(), range);

        callTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(
            _ =>
            {
                if (!IsDead())
                    NpcMessageBus.Broadcast(GetOwner(), CallTheKiller, GetOwner(), range);
                return ValueTask.CompletedTask;
            },
            System.TimeSpan.FromMilliseconds(CallRepeatMillis),
            System.TimeSpan.FromMilliseconds(CallRepeatMillis));
    }

    private void StopCalling()
    {
        if (callTask != null && !callTask.IsDone())
            callTask.Cancel(true);
        callTask = null;
        calling.Set(false);
    }

    protected override void HandleBackHome()
    {
        StopCalling();
        base.HandleBackHome();
    }

    protected override void HandleDespawned()
    {
        StopCalling();
        base.HandleDespawned();
    }

    public BaseProtectorAI(Npc owner) : base(owner)
    {
    }

    protected new BaseSpawnTemplate GetSpawnTemplate()
    {
        return (BaseSpawnTemplate)base.GetSpawnTemplate();
    }

    /// <summary>
    /// Retail's <c>on_killed_by_user</c> / <c>on_killed_by_npc</c> pair, which for these npcs is a
    /// <c>30003</c> at fifty metres.
    /// </summary>
    /// <remarks>
    /// <b>The remark above used to list this as untranslated</b>, and the reason was that the broadcast
    /// belongs to some of these npcs and not others: 69 of the base protectors carry it and the rest do
    /// not, which is not something a class can decide for itself. <see cref="SiegeDeathCalls"/> is that
    /// list, read out of the patterns.
    /// <para>
    /// Announced before <c>StopCalling</c> and the base capture, because both of those can return early
    /// -- the capture bails when there is no active base -- and a chief dying outside a live base still
    /// has to tell the killer hunting it to stand down.
    /// </para>
    /// <para>
    /// Still untranslated on that rung: the two <c>set_condition_spawn_variable</c> calls recording
    /// which of <c>pc_light</c>, <c>pc_dark</c> or <c>drakan</c> killed it.
    /// </para>
    /// </remarks>
    protected override void HandleDied()
    {
        SiegeDeathCalls.Announce(GetOwner());
        StopCalling();
        base.HandleDied();
        Base @base = BaseService.GetInstance().GetActiveBase(GetSpawnTemplate().GetId());
        if (@base == null)
            return;
        DamageInfo<AionObject> mostDamage = GetAggroList().GetFinalDamageList().ToTeamDamages().GetMostDamage();
        Creature bossKiller = mostDamage.GetAttacker() is TemporaryPlayerTeam team ? team.GetLeaderObject() : (Creature)mostDamage.GetAttacker();
        BaseOccupier newOccupier = @base.GetOccupier(bossKiller);
        BaseService.GetInstance().Capture(@base.GetId(), newOccupier);
    }

    public override void ModifyOwnerStat(Stat2 stat)
    {
        if (stat.GetStat() == StatEnum.MAXHP && GetOwner().GetLevel() >= 65) // Avoid adjusting low-level zones
            stat.SetBaseRate(SiegeConfig.BASE_PROTECTOR_HEALTH_MULTIPLIER);
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_DECAY or AIQuestion.ALLOW_RESPAWN or AIQuestion.REWARD_LOOT or AIQuestion.REMOVE_EFFECTS_ON_MAP_REGION_DEACTIVATE => false,
            _ => base.Ask(question),
        };
    }
}
