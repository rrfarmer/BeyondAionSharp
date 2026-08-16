using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The six suspicious coffins in Adma Stronghold. Retail patterns <c>NoAction_CoffinA</c> through
/// <c>NoAction_CoffinF</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Disturbing a coffin is meant to be a mistake: the
/// first hit makes it shout to the room, and Lord Lannok comes for whoever landed it. None of that
/// happened here — the coffins were plain aggressive NPCs and the shout had no sender.
/// <para>
/// The six patterns are identical apart from the coordinates their skeleton waves spawn at, and those
/// waves are not translated: they are triggered by three invisible controllers that broadcast on
/// waking, and nothing yet spawns those controllers. So one class covers all six, and the despawn
/// branches below are correct but currently clear an empty group.
/// </para>
/// </remarks>
[AIName("suspicious_coffin")]
public class SuspiciousCoffinAI : PatternAi
{
    /// <summary>"Someone is at my coffin", carrying whoever it was.</summary>
    private const int AlarmMessage = 6609;
    private const float AlarmRange = 50f;

    /// <summary>Lord Lannok's all-clear, which also re-arms the alarm.</summary>
    private const int AllClearMessage = 6601;

    /// <summary>Retail's <c>FLAGVARI_BETA_1</c>: shout once, until the all-clear resets it.</summary>
    private const int Alerted = 1;

    /// <summary>The skeleton wave.</summary>
    private const int Wave = 1;

    private const int FaithfulPage = 280933;
    private const int DiligentPage = 280949;

    /// <summary>Three minutes, at the coffin's own point rather than scattered.</summary>
    private const int PageLife = 180;

    /// <summary>
    /// Where each coffin puts its pages, and which three calls it answers.
    /// </summary>
    /// <remarks>
    /// Retail writes six separate patterns — <c>NoAction_CoffinA</c> through <c>F</c> — for exactly
    /// this reason: the branches are identical and only the spawn point differs. One class over six
    /// npc ids has to carry the points itself.
    /// <para>
    /// The two triplets matter as much as the points. A, B and C answer 6602-6604, which come from the
    /// invisible controllers; D, E and F answer 6605-6607, which Lord Lannok calls himself. Guarding on
    /// the coffin's own triplet is what stops an A coffin answering a call meant for D.
    /// </para>
    /// </remarks>
    private readonly record struct Coffin(float X, float Y, float Z, int First, int Second, int Third);

    private static readonly Dictionary<int, Coffin> ByCoffin = new Dictionary<int, Coffin>
    {
        [280942] = new Coffin(601f, 765f, 198.6f, 6602, 6603, 6604),
        [280950] = new Coffin(619f, 725f, 198.6f, 6602, 6603, 6604),
        [281055] = new Coffin(575f, 724f, 198.6f, 6602, 6603, 6604),
        [281056] = new Coffin(596f, 723f, 198.6f, 6605, 6606, 6607),
        [281057] = new Coffin(585f, 749f, 198.6f, 6605, 6606, 6607),
        [281058] = new Coffin(620f, 759f, 198.6f, 6605, 6606, 6607),
    };

    /// <summary>True when the call just heard is this coffin's <paramref name="slot"/>-th.</summary>
    private static PatternCondition Hears(int slot) => ai =>
        ByCoffin.TryGetValue(ai.GetOwner().GetNpcId(), out Coffin c)
        && ai.CurrentMessage == (slot == 1 ? c.First : slot == 2 ? c.Second : c.Third);

    private static PatternAction Place(int npcId) => ai =>
    {
        if (ByCoffin.TryGetValue(ai.GetOwner().GetNpcId(), out Coffin c))
            ai.SpawnAt(npcId, Wave, PageLife, new SpawnSpot(c.X, c.Y, c.Z, 0));
    };

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(7, "", [When.FirstTime(Alerted)],
                Do.Broadcast(AlarmMessage, AlarmRange, aboutTarget: true))),

        OnMessage = Of(
            // Two branches for one message, as retail writes it: the higher-priority one clears the
            // alarm flag on its way past so the coffin can shout again, and the lower one is what runs
            // once the flag is already clear. Both despawn the wave.
            Branch(7, "", [When.Message(AllClearMessage), When.Consuming(Alerted)],
                Do.Despawn(Wave)),

            Branch(6, "", [When.Message(AllClearMessage)],
                Do.Despawn(Wave)),

            // The waves. The first call is always a page; the second and third are a mage on a small
            // roll and a page otherwise — never both, which is how retail writes the pair.
            Branch(5, "first call", [Hears(1)],
                Place(FaithfulPage)),

            Branch(4, "second call", [When.Chance(15), Hears(2)],
                Place(DiligentPage)),
            Branch(3, "second call", [Hears(2)],
                Place(FaithfulPage)),

            Branch(2, "third call", [When.Chance(30), Hears(3)],
                Place(DiligentPage)),
            Branch(1, "third call", [Hears(3)],
                Place(FaithfulPage))),
    };

    public SuspiciousCoffinAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
