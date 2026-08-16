using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The first-wave ghosts of the two <c>ND2_*_PhA</c> priests. Retail patterns <c>ND2_Sum_PhA1</c> and
/// <c>ND2_Sum_Naga_PhA1</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Found by
/// <c>tools/client-extract/audit_retail_messages.py</c>, which reports what a translated class's
/// pattern does with messages the class never touches — <see cref="ExedilAI"/> was sending none.
/// <para>
/// <b>A first-wave ghost does not die when its master reaches the end: it changes.</b> On
/// <c>broadcast_message 3319</c> it spawns the second-wave ghost where it stands, with retail's
/// twenty-minute lifetime, and removes itself. One in, one out, on the spot.
/// </para>
/// <list type="table">
/// <item><term>power of exedil (280774)</term><description>becomes <b>280775</b>, which is what
/// <see cref="ExedilAI"/>'s own deeper rungs call up</description></item>
/// <item><term>power of yatri (280769)</term><description>becomes <b>280819</b>, "true power of
/// yatri"</description></item>
/// </list>
/// <para>
/// <b>Both halves of the naga side are not here.</b> The listener is, because it is the same branch;
/// the sender is not. <c>Naga_PhA</c> belongs to <b>high priest yatri</b> (212308 and 280768), which is
/// on plain <c>aggressive</c> with no class at all — a banded ladder of the same shape as Exedil's
/// with its own numbers, and a <c>spawn_on_target</c> first wave rather than a scatter. Translating it
/// is the obvious next step and is a boss's worth of work, not a branch's.
/// </para>
/// <para>
/// It extends <see cref="ServantNpcAI"/> rather than replacing it, so the cast loop every summon in
/// this family runs is untouched; this only adds the listener.
/// </para>
/// <para>
/// <b>Not translated:</b> messages 3316, 3317 and 3318, which are three separate cast branches, and
/// <c>on_see_friend_killed_by_user</c>, which is a cast and a despawn on an event our runtime does not
/// raise.
/// </para>
/// </remarks>
[AIName("exedil_ghost")]
public class ExedilGhostAI : ServantNpcAI, INpcMessageListener
{
    /// <summary>What each first-wave ghost sheds into.</summary>
    private static readonly Dictionary<int, int> TrueForms = new Dictionary<int, int>
    {
        [280774] = 280775,   // ND2_Sum_PhA1      — power of exedil
        [280769] = 280819,   // ND2_Sum_Naga_PhA1 — power of yatri
    };

    /// <summary>Retail's <c>live_time</c> on the spawn, matching the pairs the boss calls directly.</summary>
    private const int TrueFormSeconds = 1200;

    public ExedilGhostAI(Npc owner)
        : base(owner)
    {
    }

    /// <summary>What this ghost becomes, or 0 for one that is not in the table.</summary>
    internal static int TrueFormOf(int npcId) => TrueForms.TryGetValue(npcId, out int form) ? form : 0;

    public void OnNpcMessage(Npc sender, int messageType, VisibleObject? param)
    {
        if (messageType != ExedilAI.TrueForm || IsDead())
            return;

        int becomes = TrueFormOf(GetOwner().GetNpcId());
        if (becomes == 0)
            return;

        WorldPosition here = GetPosition();
        if (Spawn(becomes, here.GetX(), here.GetY(), here.GetZ(), (sbyte)here.GetHeading()) is Npc grown)
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                if (grown.IsSpawned())
                    grown.GetController().DeleteIfAliveOrCancelRespawn();
                return ValueTask.CompletedTask;
            }, TrueFormSeconds * 1000L);

        AIActions.DeleteOwner(this);
    }
}
