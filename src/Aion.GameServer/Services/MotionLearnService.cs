using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class MotionLearnService
{
	public static MotionLearnPlan CreatePlan(
		Player player,
		ItemAnimationActionInfo? action,
		DateTimeOffset now)
	{
		// Java parity: model/templates/item/actions/AnimationAddAction.addMotion via MotionList.add.
		if (action == null || action.MotionIds.Count == 0)
			return MotionLearnPlan.Failed(MotionLearnFailure.MissingAction);

		var expireTime = action.Minutes == 0
			? 0
			: (int)Math.Min(int.MaxValue, now.ToUnixTimeSeconds() + action.Minutes * 60L);
		var finalMotions = player.Motions.ToList();
		var deactivatedMotionIds = new List<int>();
		var addedMotions = new List<PlayerMotion>();
		foreach (var motionId in action.MotionIds)
		{
			if (expireTime == 0)
				finalMotions.RemoveAll(motion => motion.Id == motionId);

			var motionType = PlayerMotion.GetMotionType(motionId);
			for (var index = 0; index < finalMotions.Count; index++)
			{
				var existing = finalMotions[index];
				if (existing.IsActive
					&& existing.Id != motionId
					&& PlayerMotion.GetMotionType(existing.Id) == motionType)
				{
					finalMotions[index] = existing with { IsActive = false };
					deactivatedMotionIds.Add(existing.Id);
				}
			}

			var addedMotion = new PlayerMotion(motionId, expireTime, IsActive: true);
			var existingIndex = finalMotions.FindIndex(motion => motion.Id == motionId);
			if (existingIndex >= 0)
				finalMotions[existingIndex] = addedMotion;
			else
				finalMotions.Add(addedMotion);
			addedMotions.Add(addedMotion);
		}

		return new MotionLearnPlan(
			MotionLearnFailure.None,
			finalMotions,
			addedMotions,
			deactivatedMotionIds.Distinct().ToArray());
	}
}

public sealed record MotionLearnPlan(
	MotionLearnFailure Failure,
	IReadOnlyList<PlayerMotion> Motions,
	IReadOnlyList<PlayerMotion> AddedMotions,
	IReadOnlyList<int> DeactivatedMotionIds)
{
	public bool Succeeded => Failure == MotionLearnFailure.None;

	public static MotionLearnPlan Failed(MotionLearnFailure failure)
	{
		return new MotionLearnPlan(failure, Array.Empty<PlayerMotion>(), Array.Empty<PlayerMotion>(), Array.Empty<int>());
	}
}

public enum MotionLearnFailure
{
	None,
	MissingAction,
}
