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

    /// <summary>The skeleton wave, which nothing currently fills.</summary>
    private const int Wave = 1;

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
                Do.Despawn(Wave))),
    };

    public SuspiciousCoffinAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
