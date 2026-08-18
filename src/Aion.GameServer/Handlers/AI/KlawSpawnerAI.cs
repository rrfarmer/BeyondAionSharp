using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The klaw spawner (700169) of Heiron. Retail pattern <c>BroadAtt_MR</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <c>BroadAtt_MR</c> is one of a family of three —
/// <c>_LR</c>, <c>_MR</c> and <c>_SR</c> at fifty, twenty-five and fifteen metres — whose whole
/// content is a single call: <b>somebody is hitting me</b>, naming the attacker, on every blow and
/// every spell. It is retail's standard way of making an object that cannot fight cry out for the
/// things that can.
/// <para>
/// <b>What answers here is a nest.</b> Klaw workers, gatherers, a seeker and a spriggan fighter take a
/// hundred hate on whoever struck the spawner and go for them; two kerubs a field away take one point
/// and no more. Same message, same protocol, two very different degrees of interest.
/// </para>
/// <para>
/// <b>The class it had is kept.</b> <c>onedmg_passive</c> is shared by a hundred and twelve npcs, so
/// this extends <see cref="OneDmgNoActionAI"/> and adds only the call, exactly as
/// <see cref="DefencePostFlagAI"/> does.
/// </para>
/// <para>
/// <b>Not built:</b> the <c>BroadTalk_*</c> half of the family — the same call raised by being talked
/// to rather than struck — whose live members are a wine barrel and a kerubian bucket on
/// <c>quest_use_item</c>, a class shared by six hundred and ten npcs. And <c>BroadAtt_SR</c>'s arachna
/// egg, for the same reason.
/// </para>
/// </remarks>
[AIName("klaw_spawner")]
public class KlawSpawnerAI : OneDmgNoActionAI
{
	/// <summary>Retail's <c>1103</c>: somebody is hitting me.</summary>
	public const int BeingAttacked = 1103;

	/// <summary>Retail's <c>range_as_meter</c> on the middle member of the family.</summary>
	public const float MiddleReach = 25f;

	public KlawSpawnerAI(Npc owner)
		: base(owner)
	{
	}

	protected override void HandleAttack(Creature creature)
	{
		base.HandleAttack(creature);
		NpcMessageBus.Broadcast(GetOwner(), BeingAttacked, creature, MiddleReach);
	}
}

/// <summary>
/// Whatever is nesting around a <c>BroadAtt</c> object and answers when it is struck. Retail patterns
/// <c>ND2_CnD_RE1_egg</c>, <c>ND2_CnD_BR1_egg</c> and <c>D2_FnA_B1</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. One branch, and the whole of the mechanic is the
/// number in it: <b>a hundred hate points for the klaws, one for the kerubs.</b>
/// <para>
/// <b>A hundred is a claim and one is a glance.</b> The klaw nest commits to whoever struck the
/// spawner and will hold that player against ordinary threat; the kerubs join and are moved by the
/// next thing that happens. Retail says so with nothing but <c>point_to_add</c>, and a class that used
/// one value for both would make a field of kerubs behave like a nest.
/// </para>
/// <para>
/// <b>Not translated:</b> everything else these three patterns do.
/// </para>
/// </remarks>
[AIName("broad_att_answer")]
public class BroadAttAnswerAI : PatternAi
{
	/// <summary>Retail's <c>point_to_add</c>, which is the only thing separating the two nests.</summary>
	private static readonly Dictionary<int, int> Weight = new()
	{
		[210874] = 100,   // klaw worker
		[210908] = 100,   // klaw worker
		[210917] = 100,   // klaw gatherer
		[210928] = 100,   // klaw seeker
		[211499] = 100,   // spriggan fighter
		[210670] = 1,     // smallhorn kerub
		[210671] = 1,     // bigfoot kerubar
	};

	private static readonly Dictionary<int, AiPattern> Patterns =
		Weight.ToDictionary(e => e.Key, e => Build(e.Value));

	private static AiPattern Build(int hate) => new AiPattern
	{
		OnMessage = Of(
			Branch(1, "", [When.Message(KlawSpawnerAI.BeingAttacked)],
				Do.HateMessageParam(hate))),
	};

	private readonly AiPattern pattern;

	public BroadAttAnswerAI(Npc owner)
		: base(owner)
	{
		pattern = Patterns[owner.GetNpcId()];
	}

	protected override AiPattern Pattern => pattern;
}
