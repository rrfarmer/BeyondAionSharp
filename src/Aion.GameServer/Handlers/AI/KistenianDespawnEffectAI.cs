using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The Kistenian despawn effect (295181). Retail pattern <c>DGuard_KistenianDespawn</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The whole pattern is one branch: the moment it
/// appears it shouts twice and removes itself.
/// <list type="bullet">
/// <item><b>10017</b> — every fire spirit within fifty metres disperses</item>
/// <item><b>10018</b> — Kistenian lights another flame</item>
/// </list>
/// <para>
/// It is left where a spirit dies, and by Kistenian when he does. Its display name is "dredgion elite
/// fighter", which is misleading: it fights nothing and lasts six seconds. That name is why the audit
/// listed it as a missing combatant.
/// </para>
/// <para>
/// Killing one spirit therefore clears the rest and hands Kistenian a fresh flame — the cost of
/// thinning them, and the reason the fight does not simply accumulate adds.
/// </para>
/// <para>
/// <b>Untested, and worth a look.</b> This shouts from <c>on_wake_up</c> and removes itself in the
/// same branch, so it broadcasts at the instant it enters the world. <c>NpcMessageBus</c> walks the
/// sender's known list, and a just-spawned NPC's known list is not populated yet in the test harness —
/// the cry reaches nobody there. Whether the live server fills it before the AI's spawn hook runs was
/// not established. If it does not, this pattern is inert on the server too and the fix belongs in the
/// bus or in the spawn order, not here.
/// </para>
/// </remarks>
[AIName("kistenian_despawn_effect")]
public class KistenianDespawnEffectAI : PatternAi
{
    private const float CryRange = 50f;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnWakeUp = Of(
            Branch(7, "", When.Always,
                Do.Broadcast(KistenianPetAI.Disperse, CryRange),
                Do.Broadcast(KistenianAI.LightAnotherFlame, CryRange),
                Do.DespawnSelf())),
    };

    public KistenianDespawnEffectAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
