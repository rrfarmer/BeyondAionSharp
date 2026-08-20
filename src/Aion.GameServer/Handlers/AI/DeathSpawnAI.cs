using System.Collections.Concurrent;
using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// NPCs that leave something behind when they fall, and did not.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Found by <c>audit_death_spawns.py</c>, which lists
/// retail death spawns whose owner runs a shared AI — so no class and no data could have been doing it,
/// because <c>&lt;summons&gt;</c> is keyed on health percentage and has no death trigger at all.
/// <para>
/// <b>One class rather than nine.</b> These are unrelated encounters across five instances, but the
/// mechanic is identical in every one: on death, put <c>N</c> of something at my own point, optionally
/// scattered, optionally for a while. What differs is the facts, so the facts are a table and the
/// structure is here — the same split as <see cref="GuardReinforcementPatterns"/>.
/// </para>
/// <para>
/// <b>The one worth naming is 214700.</b> "Suspicious boy" is not a boy: killing him puts down
/// <c>Adma_UndeadLightRaNamedReal_50_Ae</c> — 214701, <b>betrayer villaire</b> — for an hour. It is a
/// disguise, and this port let players kill the disguise and walk away.
/// </para>
/// <para>
/// <b><c>is_user</c> is carried, not flattened.</b> Four of the nine fire only on a player kill; 214700
/// fires on <c>on_killed_by_user</c> <i>and</i> <c>on_killed_by_npc</c>, which is not the same as being
/// unguarded and is why the flag is per row. See <c>When.KilledByPlayer</c>.
/// </para>
/// <para>
/// <b>Deliberately not carried:</b> the two Abyssal Reliquary cannons also spawn
/// <c>IDAbRe_Core_Sum_OnDie_silika</c> for three seconds, which is an effect this port collapses — and
/// <c>IDF4Re_Drana_Named_C</c>'s absolute-placed <c>NoShowNPC2</c>, for the same reason. The audit's
/// remaining rows are two <c>Test_</c> patterns, which are not content.
/// </para>
/// </remarks>
[AIName("death_spawn")]
public class DeathSpawnAI : PatternAi
{
	/// <summary>What one NPC leaves behind. Read out of the patterns, not transcribed by eye.</summary>
	/// <param name="NpcId">What is left.</param>
	/// <param name="Count">Retail's <c>num_to_spawn</c>.</param>
	/// <param name="Range">Retail's <c>spawn_range</c>; 0 means exactly at the fallen NPC.</param>
	/// <param name="LiveSeconds">Retail's <c>live_time</c>; 0 means it stays.</param>
	/// <param name="PlayerKillOnly">Retail hung it on <c>on_killed_by_user</c> alone.</param>
	internal readonly record struct Bequest(int NpcId, int Count, float Range, int LiveSeconds, bool PlayerKillOnly);

	/// <summary>Retail's <c>SPAWN_ID_1</c> where it names one; <c>SPAWN_ID_NONE</c> is 0.</summary>
	private const int Left = 1;

	internal static readonly IReadOnlyDictionary<int, Bequest> Bequests = new Dictionary<int, Bequest>
	{
		// NLehpar_AeB1 — a lehpar that leaves its assistant for six minutes.
		[212151] = new Bequest(280793, 1, 0f, 360, true),
		// D2_AnH — mudthorn, ten minutes.
		[212205] = new Bequest(212206, 1, 0f, 600, true),
		// ND2_ReA_1 — the suspicious boy, and the betrayer underneath him, for an hour.
		[214700] = new Bequest(214701, 1, 0f, 3600, false),
		// IDAbRe_Core_Cannon — five orkanimum fragments, scattered three metres.
		[216953] = new Bequest(700863, 5, 3f, 0, false),
		// DF4_CondorEgg — the huge gryphu egg hatches an angry conchi for three minutes.
		[217121] = new Bequest(217089, 1, 0f, 180, false),
		// IDArena_pvp02_S3_meatBarrel — the barrel spills its meat five metres.
		[218757] = new Bequest(218758, 1, 5f, 0, false),
		// IDAbRe_Core_Cannon_02 — five fragments again, scattered five.
		[219545] = new Bequest(701587, 5, 5f, 0, false),
		// Neuth2_Abyss_65_An — a five-second trap where it fell.
		[231196] = new Bequest(282437, 1, 0f, 5, false),
		// ND2_RnG — likewise, on a player kill only.
		[280946] = new Bequest(280947, 1, 0f, 5, true),
	};

	private static readonly ConcurrentDictionary<int, AiPattern> ByNpcId = new ConcurrentDictionary<int, AiPattern>();

	/// <summary>An NPC not in the table behaves exactly as <c>aggressive</c> did.</summary>
	private static readonly AiPattern Nothing = new AiPattern();

	private static AiPattern Build(int npcId)
	{
		if (!Bequests.TryGetValue(npcId, out Bequest left))
		{
			// Everything not hand-read comes from the generated tables, composed: this npc may also
			// have a rotation, a wake rung or an idle cycle, and before GeneratedPattern existed
			// being bound here meant none of those were ever read.
			return GeneratedPattern.For(npcId);
		}

		PatternCondition[] guards = left.PlayerKillOnly ? [When.KilledByPlayer] : [];

		return new AiPattern
		{
			OnDie = Of(
				Branch(7, "leaves what retail leaves", guards,
					Do.SpawnNear(left.NpcId, Left, left.Count, left.Range, left.LiveSeconds))),
		};
	}

	public DeathSpawnAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => ByNpcId.GetOrAdd(GetOwner().GetNpcId(), static id => Build(id));
}
