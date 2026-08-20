using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Spawns;
using System.Threading.Tasks;

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
/// <para>
/// <b>It is a pattern class now, and still passive.</b> It was written on <c>GeneralNpcAI</c> to keep
/// these npcs from attacking on sight, and that reasoning was right -- but it also meant an npc bound
/// here could hold nothing else, and 143 of them turned out to have real death rungs that never ran.
/// <see cref="PassivePatternAi"/> is the base that resolves it: it is a <c>PatternAi</c>, so the slots
/// exist, and it puts the three aggression overrides back the way <c>GeneralNpcAI</c> has them, so
/// nothing here starts fighting. The variable write is untouched and still happens on spawn.
/// </para>
/// </remarks>
[AIName("wake_variable")]
public class WakeVariableAI : PassivePatternAi
{
	private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, AiPattern> ByNpcId =
		new System.Collections.Concurrent.ConcurrentDictionary<int, AiPattern>();

	public WakeVariableAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern =>
		ByNpcId.GetOrAdd(GetOwner().GetNpcId(), static id => GeneratedPattern.For(id));

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
		WakeVariableWrites.Apply(GetOwner());
	}
}

/// <summary>
/// The same job, for an npc that fights.
/// </summary>
/// <remarks>
/// 197 of these patterns belong to npcs on <c>aggressive</c>. They cannot take
/// <see cref="WakeVariableAI"/>, which descends from <c>GeneralNpcAI</c> and would quietly remove their
/// aggression -- the mirror of the reason that class exists at all. Two thin classes over one shared
/// write is the whole of it; the alternative was a table that silently changed what a third of its npcs
/// do in a fight.
/// <para>
/// Now a <see cref="PatternAi"/>, which is already aggressive, so these npcs keep the aggression this
/// class exists to protect and gain the slots they were missing. Nothing about the write changes.
/// </para>
/// </remarks>
[AIName("wake_variable_aggressive")]
public class WakeVariableAggressiveAI : PatternAi
{
	private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, AiPattern> ByNpcId =
		new System.Collections.Concurrent.ConcurrentDictionary<int, AiPattern>();

	public WakeVariableAggressiveAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern =>
		ByNpcId.GetOrAdd(GetOwner().GetNpcId(), static id => GeneratedPattern.For(id));

	protected override void HandleSpawned()
	{
		base.HandleSpawned();
		WakeVariableWrites.Apply(GetOwner());
	}
}

/// <summary>The write itself, so the passive and aggressive classes cannot drift apart.</summary>
internal static class WakeVariableWrites
{
	/// <summary>
	/// Scoped to this npc's own world and instance, so two copies of an instance do not open each
	/// other's gates -- the registry is keyed on both.
	/// </summary>
	internal static void Apply(Npc owner)
	{
		SpawnVariables store = SpawnVariableRegistry.For(owner.GetWorldId(), owner.GetInstanceId());
		foreach (WakeVariables.Write write in WakeVariables.For(owner.GetNpcId()))
			store.Write(write.Name, write.Set, write.Modify);

		if (!WakeVariables.Vanishes(owner.GetNpcId()))
			return;

		// Retail's `despawn_self` on the same rung: 75 of these patterns announce a state and go, which
		// is the whole of what the npc is for. Scheduled rather than done here, for the reason
		// `AttackAfterSpawn` gives -- this runs inside the owner's own BringIntoWorld, and removing it
		// mid-spawn fights the rest of that path.
		ThreadPoolManager.GetInstance().Schedule(_ =>
		{
			if (owner.IsSpawned())
				owner.GetController().Delete();
			return ValueTask.CompletedTask;
		}, 0);
	}
}
