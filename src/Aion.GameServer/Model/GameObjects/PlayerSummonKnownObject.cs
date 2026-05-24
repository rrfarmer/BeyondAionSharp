namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerSummonKnownObject(
	int ObjectId,
	PlayerSummonKnownObjectKind Kind,
	int CreatorObjectId = 0,
	int NpcTemplateId = 0,
	PlayerSummonKnownNpcTemplateType NpcTemplateType = PlayerSummonKnownNpcTemplateType.None,
	IReadOnlySet<int>? DisabledSkillCooldownIds = null,
	long? LastSkillTimeMilliseconds = null)
{
	private static readonly IReadOnlySet<int> EmptyDisabledCooldowns = new HashSet<int>();

	public IReadOnlySet<int> DisabledCooldowns => DisabledSkillCooldownIds ?? EmptyDisabledCooldowns;

	public bool IsSkillCooldownDisabled(int cooldownId)
	{
		// Java parity: Creature.isSkillDisabled checks the skill template cooldown id against creature cooldowns.
		return DisabledCooldowns.Contains(cooldownId);
	}
}
