using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Isbariya's two servants: the holy servant (281659) and the apocalyptic energy (281660). Retail
/// pattern <c>IDCT_Boss_ArchPriest</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/instance/beshmundirTemple/IsbariyaServantsAI (Luzien). TODO: random aggro switch.
/// Retail-sourced correction below; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>The two lifetimes were swapped, and neither was right.</b> Retail gives the shield summon
/// (<c>ArchPriest2_Shield_Sum</c>, 281659) <b>seven</b> seconds and the Taros
/// (<c>ArchPriest2_Taros</c>, 281660) <b>twenty</b>. This class gave 281659 twenty and everything else
/// ten — so the short-lived one outlasted the long-lived one by a factor of three.
/// </para>
/// </remarks>
[AIName("isbariyaServants")]
public class IsbariyaServantsAI : AggressiveNpcAI
{
    /// <summary>Retail's <c>live_time</c> on each: seven seconds and twenty.</summary>
    private const int ShieldLifeMillis = 7000;
    private const int TarosLifeMillis = 20000;

    public IsbariyaServantsAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        int lifetime = GetNpcId() == 281659 ? ShieldLifeMillis : TarosLifeMillis;
        ToDespawn(lifetime);
    }

    private void ToDespawn(int delay)
    {
        ThreadPoolManager.GetInstance().Schedule(
            _ =>
            {
                AIActions.DeleteOwner(this);
                return ValueTask.CompletedTask;
            },
            (long)delay);
    }
}
