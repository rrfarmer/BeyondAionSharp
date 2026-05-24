using Aion.GameServer.Services;

namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerSummonKnownObject(
	int ObjectId,
	PlayerSummonKnownObjectKind Kind,
	int CreatorObjectId = 0,
	int NpcTemplateId = 0,
	PlayerSummonKnownNpcTemplateType NpcTemplateType = PlayerSummonKnownNpcTemplateType.None,
	IReadOnlySet<int>? DisabledSkillCooldownIds = null,
	long? LastSkillTimeMilliseconds = null,
	int? NextSkillDelayMilliseconds = null,
	PlayerSummonKnownObjectNpcSkillCandidateListProjection? LastNpcSkillListProjection = null,
	PlayerSummonKnownObjectNpcSkillSelectionPreview? LastNpcSkillSelectionPreview = null,
	PlayerSummonKnownObjectNpcSkillActionPreview? LastNpcSkillActionPreview = null,
	PlayerSummonKnownObjectNpcSkillPostSpawnPreview? LastNpcSkillPostSpawnPreview = null,
	PlayerSummonKnownObjectNpcSkillActionWorkflowPreview? LastNpcSkillActionWorkflowPreview = null,
	PlayerSummonKnownObjectNpcSkillPerformAttackPreview? LastNpcSkillPerformAttackPreview = null,
	PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview? LastNpcSkillPerformAttackExecutionPreview = null,
	PlayerAbnormalState AbnormalState = PlayerAbnormalState.None,
	bool IsTransformed = false,
	bool TransformBansSkillUse = false,
	bool IsVisible = true,
	bool IsDead = false,
	bool IsAboutToDie = false,
	int HpPercentage = 100,
	bool IsFlying = false,
	bool? IsPhysicalClass = null,
	IReadOnlyList<PlayerSummonKnownObjectNpcSkillCarvedSignetState>? CarvedSignets = null)
{
	private static readonly IReadOnlySet<int> EmptyDisabledCooldowns = new HashSet<int>();
	private static readonly IReadOnlyList<PlayerSummonKnownObjectNpcSkillCarvedSignetState> EmptyCarvedSignets = [];

	public IReadOnlySet<int> DisabledCooldowns => DisabledSkillCooldownIds ?? EmptyDisabledCooldowns;

	public IReadOnlyList<PlayerSummonKnownObjectNpcSkillCarvedSignetState> ActiveCarvedSignets => CarvedSignets ?? EmptyCarvedSignets;

	public bool IsCreature => Kind == PlayerSummonKnownObjectKind.Creature;

	public bool IsSkillCooldownDisabled(int cooldownId)
	{
		// Java parity: Creature.isSkillDisabled checks the skill template cooldown id against creature cooldowns.
		return DisabledCooldowns.Contains(cooldownId);
	}

	public bool IsAbnormalSet(PlayerAbnormalState state)
	{
		// Java parity: controllers/effect/EffectController.isAbnormalSet for represented NPC known objects.
		return state == PlayerAbnormalState.None ? AbnormalState == PlayerAbnormalState.None : (AbnormalState & state) == state;
	}

	public bool IsInAnyAbnormalState(PlayerAbnormalState state)
	{
		// Java parity: controllers/effect/EffectController.isInAnyAbnormalState for represented NPC known objects.
		return state == PlayerAbnormalState.None ? AbnormalState == PlayerAbnormalState.None : (AbnormalState & state) != 0;
	}
}
