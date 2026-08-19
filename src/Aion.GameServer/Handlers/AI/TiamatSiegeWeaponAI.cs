using System.Collections.Concurrent;
using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Tiamat Stronghold's Vritra siege weapons, which leave a usable one behind when they fall.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Eleven patterns —
/// <c>IDF5_TD_War_Vri_Cannon</c>, <c>_02</c>..<c>_06</c> and <c>IDF5_TD_War_Vri_DirectGun</c>,
/// <c>_02</c>..<c>_05</c> — carry the same <c>on_killed_by_user</c> rung:
/// <code>
/// ? is_user
/// &gt; spawn npc_nameid=BIDF5_TD_War_PC_Cannon_03 num_to_spawn=1
///          spawn_location_type=SPAWN_LOCATION_MY_POINT dir=90 despawn_at_attack_state=TRUE
/// </code>
/// <para>
/// <b>The thing it leaves is not wreckage.</b> The <c>PC</c> in the devname is the point: 284869 is a
/// <c>type="GENERAL"</c>, <c>DISCIPLINED</c>-rank "pashid reserve aetheric cannon" — a siege weapon the
/// raid can use. Destroying the defenders' artillery is how you get your own, and in this port destroying
/// it did nothing at all. Every one of these eleven ran plain <c>aggressive</c>.
/// </para>
/// <para>
/// <b>Each has its own heading</b>, and they are not decorative: retail gives dirs of 165, 50, 90, 35,
/// 50, 0, 153, 40, 150, 105 and 0, and a siege weapon pointing the wrong way is furniture. That is why
/// this needed <c>Do.SpawnFacing</c> rather than <c>Do.SpawnNear</c>, which hands over the spawner's
/// heading.
/// </para>
/// <para>
/// <b><c>is_user</c> is translated, not dropped.</b> Retail leaves the replacement only when a player
/// lands the kill, so a weapon that expires or is cleaned up leaves nothing — which is what stops a
/// reset from littering the field with usable artillery.
/// </para>
/// <para>
/// <b>Not translated:</b> the firing rotation. All eleven address <c>SKILLI_INDEX_0</c> on a seven-second
/// battery of timers, and this port cannot resolve a pattern's skill index to a skill id. The weapons
/// keep their <c>aggressive</c> attacking behaviour, which <see cref="PatternAi"/> derives from.
/// </para>
/// </remarks>
[AIName("tiamat_siege_weapon")]
public class TiamatSiegeWeaponAI : PatternAi
{
	/// <summary>Retail uses <c>SPAWN_ID_NONE</c>: the replacement belongs to nobody and is never cleared.</summary>
	private const int Unowned = 0;

	/// <summary>
	/// Destroyed weapon npc id -> (the usable one it leaves, retail's <c>dir</c> in degrees).
	/// </summary>
	/// <remarks>
	/// Written out rather than generated: eleven rows read from eleven patterns is not worth an extractor,
	/// and a table this small is easier to check against the patterns by eye than a generator would be.
	/// </remarks>
	internal static readonly IReadOnlyDictionary<int, (int Replacement, int Degrees)> Replacements =
		new Dictionary<int, (int, int)>
		{
			[233545] = (284802, 165),   // IDF5_TD_War_Vri_Cannon
			[233741] = (284868, 50),    // IDF5_TD_War_Vri_Cannon_02
			[233742] = (284869, 90),    // IDF5_TD_War_Vri_Cannon_03
			[233743] = (284870, 35),    // IDF5_TD_War_Vri_Cannon_04
			[233744] = (284871, 50),    // IDF5_TD_War_Vri_Cannon_05
			[233745] = (284872, 0),     // IDF5_TD_War_Vri_Cannon_06
			[233546] = (284803, 153),   // IDF5_TD_War_Vri_DirectGun
			[233746] = (284873, 40),    // IDF5_TD_War_Vri_DirectGun_02
			[233747] = (284874, 150),   // IDF5_TD_War_Vri_DirectGun_03
			[233748] = (284875, 105),   // IDF5_TD_War_Vri_DirectGun_04
			[233749] = (284876, 0),     // IDF5_TD_War_Vri_DirectGun_05
		};

	private static readonly ConcurrentDictionary<int, AiPattern> ByNpcId = new ConcurrentDictionary<int, AiPattern>();

	/// <summary>A weapon not in the table behaves exactly as <c>aggressive</c> did.</summary>
	private static readonly AiPattern Nothing = new AiPattern();

	private static AiPattern Build(int npcId)
	{
		if (!Replacements.TryGetValue(npcId, out (int Replacement, int Degrees) left))
			return Nothing;

		return new AiPattern
		{
			OnDie = Of(
				Branch(5, "leaves a usable one where it stood", [When.KilledByPlayer],
					Do.SpawnFacing(left.Replacement, Unowned, left.Degrees))),
		};
	}

	public TiamatSiegeWeaponAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => ByNpcId.GetOrAdd(GetOwner().GetNpcId(), static id => Build(id));
}
