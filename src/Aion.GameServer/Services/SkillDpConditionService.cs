using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class SkillDpConditionService
{
	public static SkillDpConditionResult Validate(Player? effector, int requiredDp)
	{
		// Java parity: skillengine/condition/DpCondition.validate.
		if (effector == null)
			return SkillDpConditionResult.MissingEffector(requiredDp);

		return effector.Dp >= requiredDp
			? SkillDpConditionResult.Satisfied(effector.ObjectId, requiredDp, effector.Dp)
			: SkillDpConditionResult.NotEnoughDp(effector.ObjectId, requiredDp, effector.Dp);
	}
}

public sealed record SkillDpConditionResult(
	SkillDpConditionStatus Status,
	int ObjectId,
	int RequiredDp,
	int CurrentDp)
{
	public bool Succeeded => Status == SkillDpConditionStatus.Satisfied;

	public static SkillDpConditionResult MissingEffector(int requiredDp)
	{
		return new SkillDpConditionResult(
			SkillDpConditionStatus.MissingEffector,
			0,
			requiredDp,
			0);
	}

	public static SkillDpConditionResult Satisfied(int objectId, int requiredDp, int currentDp)
	{
		return new SkillDpConditionResult(
			SkillDpConditionStatus.Satisfied,
			objectId,
			requiredDp,
			currentDp);
	}

	public static SkillDpConditionResult NotEnoughDp(int objectId, int requiredDp, int currentDp)
	{
		return new SkillDpConditionResult(
			SkillDpConditionStatus.NotEnoughDp,
			objectId,
			requiredDp,
			currentDp);
	}
}

public enum SkillDpConditionStatus
{
	Satisfied,
	MissingEffector,
	NotEnoughDp,
}
