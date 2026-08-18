using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The anuhart guardian (214847), retail pattern <c>Lizardman_PeB_IDLF1</c> — the eighth Anuhart
/// listener, and the one that could not be an <see cref="AnuhartGuardAI"/>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Its pattern is byte-for-byte the other seven's, but
/// the npc is on <c>drakanmedic</c> — a Java-parity healer that seventy-nine npcs share, so the answer
/// could go neither into the shared class nor into a <see cref="Ai.Pattern.PatternAi"/> without
/// throwing the healing away. It is a subclass instead, and it implements the one call by hand.
/// <para>
/// <b>This is the guard you least want to leave standing</b>, which is why it is worth the extra class:
/// the priest is the reason the room is hard, and a call that pulls the other seven and leaves the
/// healer behind would be a quieter fight than retail's.
/// </para>
/// <para>
/// Retail's idle/attack split reads <c>NPC_STATE_ATTACK</c> directly here rather than through
/// <c>PatternAi</c>'s latch, which is the same question asked of the AI state machine instead of a
/// pattern's own bookkeeping.
/// </para>
/// </remarks>
[AIName("anuhart_medic")]
public class AnuhartMedicAI : DrakanMedicAI, INpcMessageListener
{
	private const int Rouse = 300;
	private const int Outbid = 500;

	public AnuhartMedicAI(Npc owner)
		: base(owner)
	{
	}

	public void OnNpcMessage(Npc sender, int messageType, VisibleObject? param)
	{
		if (messageType != MarabataControllerAI.BoosterUnderAttack)
			return;

		if (param is not Creature target || target.IsDead())
			return;

		GetAggroList().AddHate(target, IsInState(AIState.FIGHT) ? Outbid : Rouse);
		GetOwner().SetTarget(target);
	}
}
