using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The fortress and village "killers": the npcs that clear a fortress's guards when it changes hands.
/// Retail patterns <c>AB1_DrGuard_Artifact_Killer</c>,
/// <c>LDF5_Fortress_DrGuard_Artifact_Killer</c>, <c>LDF4_Advance_Killer_43</c> and
/// <c>LDF5_Fortress_Ctrl_01</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md, and
/// <c>tools/client-extract/audit_npc_call_family.py</c> for the family this belongs to.
/// <para>
/// <b>These npcs did nothing at all.</b> Ten of them had no <c>ai</c> attribute in
/// <c>npc_templates</c> and the rest were on plain <c>aggressive</c> or on
/// <see cref="AbyssGuardCallAI"/>, which only knows message 23000. So the mechanic that takes a
/// fortress's guards down when it flips has never run: the killer spawned, stood there, and was
/// eventually removed by whatever placed it.
/// </para>
/// <para>
/// <b>It is a three-message loop, and none of it is about players.</b>
/// <list type="number">
/// <item><b>30001</b>, broadcast at fifty metres as the killer wakes: every artifact and fortress
/// protector that hears it drops what it is doing and comes for the killer.</item>
/// <item><b>30002</b>, broadcast by a protector: the killer takes the caller with
/// <c>points_to_add=1000000</c> and goes for it. That is not 23000's single point — a million is a
/// command, and it is what makes this npc-versus-npc rather than a hint about a player.</item>
/// <item><b>30003</b>, broadcast by a protector as it dies: the killer that was hunting it stands
/// down and despawns.</item>
/// </list>
/// </para>
/// <para>
/// <b>Retail guards both answers with <c>is_enemy</c> on the sender, and that guard does real work.</b>
/// Every artifact protector in our data is <c>race="DRAKAN"</c>, <c>tribe="GUARD_DRAGON"</c>; without
/// the check, one protector calling would set every protector in a fortress on its own side.
/// <see cref="PatternAi.HateMessageSender"/> reaches the aggro list, which applies the same test, so
/// the condition is carried rather than re-implemented.
/// </para>
/// <para>
/// <b>Extends <see cref="AbyssGuardCallAI"/> deliberately.</b> Retail's killer patterns answer
/// <c>23000</c> as well — they are guards too — and several of these npcs were already bound to that
/// class. Subclassing keeps the guard call for the ones that had it rather than trading one mechanic
/// for another.
/// </para>
/// <para>
/// <b>Not translated:</b> each killer's own cast ladder and its <c>goto_waypoint</c> walk, which is how
/// retail actually moves it to the guards it is going to kill. Here it stands where it spawned and
/// waits to be called. And <c>LDF5_Fortress_Ctrl_01</c> sends 30001 without answering anything, so it
/// is a pure trigger; it gets the same class because the send is all it needs.
/// </para>
/// </remarks>
[AIName("fortress_killer")]
public class FortressKillerAI : AbyssGuardCallAI
{
    /// <summary>Retail's three message numbers for this family.</summary>
    public const int KillerAwake = 30001;
    public const int ProtectorCalls = 30002;
    public const int ProtectorDown = 30003;

    /// <summary>Retail's <c>range_as_meter</c> on the wake-up call.</summary>
    public const float WakeCallRange = 50f;

    /// <summary>
    /// Retail's <c>points_to_add</c> on this family, against <see cref="AbyssGuardCallAI"/>'s 1.
    /// </summary>
    public const int DropEverything = 1_000_000;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, AiPattern> Messages =
        new System.Collections.Concurrent.ConcurrentDictionary<int, AiPattern>();

    /// <summary>
    /// The two npc-versus-npc rungs, for the killers retail actually gives them to.
    /// </summary>
    /// <remarks>
    /// Four npcs sit on this class and retail names four listeners for each message, so this changes
    /// nothing today — and that was the problem. It was exact by coincidence, not by construction: a
    /// fifth killer bound to the class would have answered both messages with nothing to notice. The
    /// same shape, unchecked one message over, is what left 147 protectors charging a waking killer.
    /// </remarks>
    private static AiPattern MessagesFor(int npcId)
    {
        List<PatternBranch> rungs = new List<PatternBranch>(2);

        // Retail files the despawn at priority 100 — it outranks the fight.
        if (GuardAnswers.Answers(npcId, ProtectorDown))
        {
            rungs.Add(Branch(100, "a protector died; stand down", [When.Message(ProtectorDown)],
                Do.DespawnSelf()));
        }

        if (GuardAnswers.Answers(npcId, ProtectorCalls))
        {
            rungs.Add(Branch(5, "a protector called; go for it", [When.Message(ProtectorCalls)],
                Do.HateMessageSender(DropEverything)));
        }

        return new AiPattern { OnMessage = Of([.. rungs]) };
    }

    public FortressKillerAI(Npc owner)
        : base(owner)
    {
    }

    /// <summary>
    /// This class's own rungs, on top of whatever guard call the base built for this npc id.
    /// </summary>
    /// <remarks>
    /// The base keys its pattern on the npc, so a killer that is also a listed guard keeps both; one
    /// that is not gets these three messages and nothing else.
    /// </remarks>
    protected override AiPattern Pattern =>
        Merge(base.Pattern, Messages.GetOrAdd(GetOwner().GetNpcId(), static id => MessagesFor(id)),
            FortressKillers.PatternFor(GetOwner().GetNpcId()));

    /// <summary>
    /// Three sources, because a killer is three things at once.
    /// </summary>
    /// <remarks>
    /// The base keys its pattern on the npc, so a killer that is also a listed guard keeps its 23000.
    /// <see cref="Pattern_"/> is the message loop every killer shares. And
    /// <see cref="FortressKillers"/> carries what differs per killer — the wake call's range, whether it
    /// walks its route, and whether it hunts a garrison chief on sight — which three constants here
    /// would have got wrong for two of the three patterns.
    /// </remarks>
    /// <summary>
    /// Folds two unconditional branch lists into one branch, so neither is lost to first-match-wins.
    /// </summary>
    /// <remarks>
    /// Only safe because both sides here are <see cref="AiPattern.When.Always"/>: a guarded branch must
    /// keep its own place in the ladder, and this refuses to touch one.
    /// </remarks>
    private static PatternBranch[] MergeUnconditional(PatternBranch[] first, PatternBranch[] second)
    {
        if (first.Length == 0)
            return second;
        if (second.Length == 0)
            return first;
        if (first.Any(b => b.Conditions.Length > 0) || second.Any(b => b.Conditions.Length > 0))
            return [.. first, .. second];

        PatternAction[] actions = [.. first.SelectMany(b => b.Actions),
                                   .. second.SelectMany(b => b.Actions)];
        return [new PatternBranch(first[0].Priority, "the guard call and the focus clock, as one rung",
                                  [], actions)];
    }

    private static AiPattern Merge(AiPattern guard, AiPattern killer, AiPattern own) => new AiPattern
    {
        OnWakeUp = [.. guard.OnWakeUp, .. killer.OnWakeUp, .. own.OnWakeUp],
        // Both halves of entering combat, in ONE branch.
        //
        // **Concatenating them does not work and looked like it did.** Branch lists are first-match-wins,
        // so a killer that is also a listed guard ran the base's unconditional 23000 branch and never
        // reached the table's unconditional timer branch behind it -- `armed=0` for every artifact
        // killer, while the Advance killers worked because they are absent from GuardCalls and had no
        // first branch to lose to.
        //
        // Retail writes one rung that does both: `add_battle_timer`, `broadcast_message 23000`,
        // `use_skill`. So the actions are merged, which is what the data says anyway.
        OnEnterAttack = MergeUnconditional(guard.OnEnterAttack, own.OnEnterAttack),
        OnBattleTimer = own.OnBattleTimer,
        OnLeaveAttack = own.OnLeaveAttack,
        OnSeeNpc = own.OnSeeNpc,
        OnMessage = [.. guard.OnMessage, .. killer.OnMessage],
    };
}
