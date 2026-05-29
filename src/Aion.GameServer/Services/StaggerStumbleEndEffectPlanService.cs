namespace Aion.GameServer.Services;

public enum StaggerStumbleEndEffectPlanStatus
{
	Planned,
	BlockedInvalidEffected,
}

public sealed record StaggerStumbleEndEffectPlanInput(
	ForcedMoveEffectKind EffectKind,
	int EffectedObjectId);

public sealed record StaggerStumbleEndEffectPlan(
	StaggerStumbleEndEffectPlanStatus Status,
	StaggerStumbleEndEffectPlanInput Input,
	string AbnormalStateName,
	bool ShouldUnsetAbnormal,
	string JavaSource)
{
	public bool IsLive => false;
}

public static class StaggerStumbleEndEffectPlanService
{
	public static StaggerStumbleEndEffectPlan CreatePlan(StaggerStumbleEndEffectPlanInput input)
	{
		// Java parity breadcrumb: StaggerEffect.endEffect and StumbleEffect.endEffect only
		// call effected.getEffectController().unsetAbnormal(...) for the matching state.
		var abnormalStateName = ResolveAbnormalStateName(input.EffectKind);
		if (input.EffectedObjectId <= 0)
		{
			return new StaggerStumbleEndEffectPlan(
				StaggerStumbleEndEffectPlanStatus.BlockedInvalidEffected,
				input,
				abnormalStateName,
				ShouldUnsetAbnormal: false,
				"Forced-move endEffect requires a live effected Creature with a positive object id");
		}

		return new StaggerStumbleEndEffectPlan(
			StaggerStumbleEndEffectPlanStatus.Planned,
			input,
			abnormalStateName,
			ShouldUnsetAbnormal: true,
			input.EffectKind == ForcedMoveEffectKind.Stagger ? "StaggerEffect.endEffect" : "StumbleEffect.endEffect");
	}

	private static string ResolveAbnormalStateName(ForcedMoveEffectKind effectKind)
	{
		return effectKind switch
		{
			ForcedMoveEffectKind.Stagger => "STAGGER",
			ForcedMoveEffectKind.Stumble => "STUMBLE",
			_ => throw new ArgumentOutOfRangeException(nameof(effectKind), effectKind, "Only stagger/stumble end-effect planning is supported."),
		};
	}
}
