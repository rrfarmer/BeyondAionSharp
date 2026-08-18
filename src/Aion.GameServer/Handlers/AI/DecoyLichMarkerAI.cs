using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The markers that clear Beshmundir's decoy liches (281696, 281759, 281760). Retail patterns
/// <c>IDCT_DebuffLich</c>, <c>_2</c> and <c>_3</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>An invisible marker that appears, says "this is
/// the one", and is gone.</b> It broadcasts at fifty metres the instant it wakes, and every lich in
/// range removes itself — which is how the room is left holding a single Macunbello instead of a row
/// of identical ones.
/// <para>
/// <b>The marker deletes itself in the same branch as the call</b>, so it exists for exactly one
/// broadcast. Retail writes the same body twice, on <c>on_wake_up</c> and on <c>on_see_npc</c>: the
/// second is there for a marker placed before the liches are, and it fires on the first lich that
/// comes into view rather than on a player.
/// </para>
/// <para>
/// <b>Not translated:</b> the <c>display_system_message</c> beside each call
/// (<c>STR_MSG_IDCatacombs_NmdLich_weakness1</c>), which is blocked on the same string-id work as every
/// shout.
/// </para>
/// </remarks>
[AIName("decoy_lich_marker")]
public class DecoyLichMarkerAI : PatternAi
{
	/// <summary>Retail's <c>6981</c>: the real one is here, so the rest of you go.</summary>
	public const int TheRealOneIsHere = 6981;

	/// <summary>Retail's <c>range_as_meter</c> on both branches.</summary>
	private const float Reach = 50f;

	private static PatternBranch Clear(int priority) =>
		Branch(priority, "the real one is here", [],
			Do.Broadcast(TheRealOneIsHere, Reach), Do.DespawnSelf());

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnWakeUp = Of(Clear(7)),
		OnSeeNpc = Of(Clear(7)),
	};

	public DecoyLichMarkerAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
