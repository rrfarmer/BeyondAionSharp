using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The Silikor of Memory (214668) at the bottom of Theobomos Lab. Retail pattern <c>ND2_WhG</c>.
/// </summary>
/// <remarks>
/// Retail-sourced, and it <b>replaces</b> a Java class (<c>ai/instance/theobomosLab/SilikorofMemoryAI</c>,
/// @author Ritsu) that had the add mechanic wrong in four separate ways. See
/// docs/retail-ai-fidelity.md; this is the sanctioned exception to Java-is-spec.
/// <para>
/// <b>Java gave him three health phases; retail gives him a clock.</b> aionemu spawned <em>both</em> a
/// silikor fragment and a silikor essence at fifty, twenty-five and ten percent, within two metres,
/// forever. Retail arms timer 2 fifteen seconds into the fight and then calls <b>one</b> servant every
/// <b>thirty seconds</b> for as long as the fight lasts — a coin flip between the fragment (281053) and
/// the essence (281054) — five metres out, each living <b>three minutes</b>. Six adds in the first
/// three minutes rather than two at half health, and they expire rather than piling up.
/// </para>
/// <para>
/// <b>And every fifteen seconds he points.</b> <c>6622</c> at fifty metres carries whoever he is
/// fighting, and his two guards answer it by dropping what they are doing and going for that player —
/// see <see cref="SilikorGuardAI"/>. It is the reason the guards are part of his fight rather than a
/// pull before it.
/// </para>
/// <para>
/// <b>Not translated.</b> Timers 0, 1 and 14: between them they carry two casts and a flag var whose
/// only reader is the <c>on_spelled</c> branch below, so the whole three-timer chain has no
/// translatable effect. The <c>on_spelled</c> branch itself — retail broadcasts <c>6621</c> at thirty
/// metres when a <em>spell</em> lands on him while a world flag is unset, which clears the patrolling
/// akaimum and both its guards; our pattern runtime has no <c>on_spelled</c> event, and putting it on
/// <c>on_attacked</c> would make a melee pull clear them too, which retail deliberately does not do.
/// The roamer he places on waking and on returning to spawn (280973, at 414.1/767.7): our spawn file
/// already stands one there with a walker route, so porting the spawn would double it. His four
/// <c>say_to_all</c> lines, which have no <c>npc_shouts.xml</c> row.
/// </para>
/// <para>
/// <b>The two despawn handlers are ours</b>, translating <c>despawn_at_attack_state=TRUE</c> on the
/// servant spawn. Retail declares no despawn branch — where nothing else established cleanup we have
/// left that flag to <c>live_time</c> before (the Abyssal Reliquary flying worm) — but here the Java
/// class this replaces already cleared its adds on dying, and dropping that would be a regression
/// dressed up as fidelity.
/// </para>
/// </remarks>
[AIName("silikor")]
public class SilikorofMemoryAI : PatternAi
{
    /// <summary><c>BIDDF2A_HolyservantSum_ServantA_50_An</c> — silikor fragment.</summary>
    private const int Fragment = 281053;

    /// <summary><c>BIDDF2A_HolyservantSum_ServantB_50_An</c> — silikor essence.</summary>
    private const int Essence = 281054;

    /// <summary><c>IDDF2A_CoreFX_50_n</c> — what he leaves where the core was.</summary>
    private const int CoreFx = 281032;

    /// <summary>Retail's <c>SPAWN_ID_5</c> for the servants and <c>SPAWN_ID_1</c> for the core.</summary>
    private const int Servants = 5;
    private const int Core = 1;

    /// <summary>Retail's <c>spawn_range</c> and <c>live_time</c> on a servant.</summary>
    private const float ServantRing = 5f;
    private const int ServantLife = 180;

    /// <summary>Retail's <c>range_as_meter</c> on the order he gives his guards.</summary>
    private const float OrderReach = 50f;

    // Retail's battle timer indices.
    private const int Call = 2;
    private const int Order = 9;

    private const int CallMillis = 30000;
    private const int OrderMillis = 15000;

    /// <summary>Where retail puts the core, absolutely.</summary>
    private static readonly SpawnSpot CoreMark = new SpawnSpot(392.28f, 754.11f, 190f);

    /// <summary>
    /// Retail's <c>use_skill(OBJI_SELF, SKILLI_INDEX_0)</c> on waking. The Java port had already
    /// resolved that index, so it is one of the few we can carry.
    /// </summary>
    private const int WakingWard = 18481;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnWakeUp = Of(
            Branch(7, "", When.Always,
                Do.SkillOnSelf(WakingWard))),

        OnEnterAttack = Of(
            Branch(7, "", When.Always,
                Do.ArmTimer(Call, 15000),
                Do.ArmTimer(Order, 15000))),

        OnBattleTimer = Of(
            Branch(7, "point the guards", [When.Timer(Order)],
                Do.ArmTimer(Order, OrderMillis),
                Do.Broadcast(SilikorGuardAI.TakeThisOne, OrderReach, aboutTarget: true)),

            // The coin flip: half the time a fragment, otherwise an essence. Retail writes it as a
            // probability branch above a bare one, so the fallback is the other half.
            Branch(5, "a fragment", [When.Chance(50), When.Timer(Call)],
                Do.ArmTimer(Call, CallMillis),
                Do.SpawnNear(Fragment, Servants, count: 1, range: ServantRing, liveSeconds: ServantLife)),

            Branch(4, "an essence", [When.Timer(Call)],
                Do.ArmTimer(Call, CallMillis),
                Do.SpawnNear(Essence, Servants, count: 1, range: ServantRing, liveSeconds: ServantLife))),

        OnLeaveAttack = Of(
            Branch(101, "", When.Always,
                Do.Despawn(Servants))),

        OnDie = Of(
            Branch(100, "", When.Always,
                Do.Despawn(Servants),
                Do.SpawnAt(CoreFx, Core, 0, CoreMark))),
    };

    public SilikorofMemoryAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
