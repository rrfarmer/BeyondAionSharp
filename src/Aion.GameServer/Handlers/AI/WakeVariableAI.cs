using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World.Spawns;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// An npc whose only job is to tell the world it is there: it writes a spawn variable and nothing else.
/// </summary>
/// <remarks>
/// The conditional spawn engine gates 21,096 retail placements on 1,201 variables. The death handlers
/// write 101 of them; classifying the rest by who writes them put <c>on_wake_up</c> at the top, and
/// these 209 patterns are the part of it that needs no machinery at all -- an unguarded list of writes,
/// no spawn, no timer, no message.
/// <para>
/// <b>This extends <see cref="GeneralNpcAI"/> on purpose.</b> Every other table here feeds a
/// <c>PatternAi</c> subclass, and <c>PatternAi</c> extends <c>AggressiveNpcAI</c>; most of these npcs
/// are on <c>general</c> and are not aggressive, so routing them through a pattern class would make
/// passive npcs attack players on sight. That is the behaviour change this project has refused
/// repeatedly, and it would have been invisible here -- the variable would still be written.
/// </para>
/// <para>
/// The 197 npcs on <c>aggressive</c> whose patterns do the same thing are <b>not</b> bound to this
/// class, for the mirror-image reason: it would take their aggression away. They need an aggressive
/// variant or a home in one of the pattern tables. See docs/retail-ai-fidelity.md.
/// </para>
/// </remarks>
[AIName("wake_variable")]
public class WakeVariableAI : GeneralNpcAI
{
	public WakeVariableAI(Npc owner)
		: base(owner)
	{
	}

	/// <summary>
	/// Writes on spawn, which is where <c>PatternAi</c> evaluates <c>on_wake_up</c> too.
	/// </summary>
	/// <remarks>
	/// The write is scoped to this npc's own world and instance, so two copies of an instance do not
	/// open each other's gates — the registry is keyed on both.
	/// </remarks>
	protected override void HandleSpawned()
	{
		base.HandleSpawned();

		SpawnVariables store = SpawnVariableRegistry.For(GetOwner().GetWorldId(),
			GetOwner().GetInstanceId());
		foreach (WakeVariables.Write write in WakeVariables.For(GetOwner().GetNpcId()))
			store.Write(write.Name, write.Set, write.Modify);
	}
}
