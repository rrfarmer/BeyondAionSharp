using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SkillDpConditionServiceTests
{
	[Fact]
	public void Validate_SucceedsWhenCurrentDpMeetsRequiredValue()
	{
		var player = CreatePlayer(objectId: 1200, dp: 900);

		var result = SkillDpConditionService.Validate(player, requiredDp: 900);

		Assert.Equal(SkillDpConditionStatus.Satisfied, result.Status);
		Assert.True(result.Succeeded);
		Assert.Equal(player.ObjectId, result.ObjectId);
		Assert.Equal(900, result.RequiredDp);
		Assert.Equal(900, result.CurrentDp);
	}

	[Fact]
	public void Validate_FailsWhenCurrentDpIsBelowRequiredValue()
	{
		var player = CreatePlayer(objectId: 1201, dp: 899);

		var result = SkillDpConditionService.Validate(player, requiredDp: 900);

		Assert.Equal(SkillDpConditionStatus.NotEnoughDp, result.Status);
		Assert.False(result.Succeeded);
		Assert.Equal(player.ObjectId, result.ObjectId);
		Assert.Equal(900, result.RequiredDp);
		Assert.Equal(899, result.CurrentDp);
	}

	[Fact]
	public void Validate_RequiresPlayerEffector()
	{
		var result = SkillDpConditionService.Validate(effector: null, requiredDp: 900);

		Assert.Equal(SkillDpConditionStatus.MissingEffector, result.Status);
		Assert.False(result.Succeeded);
		Assert.Equal(0, result.ObjectId);
		Assert.Equal(900, result.RequiredDp);
		Assert.Equal(0, result.CurrentDp);
	}

	private static Player CreatePlayer(int objectId, int dp)
	{
		return new Player
		{
			ObjectId = objectId,
			PlayerClass = "RANGER",
			Race = "ELYOS",
			Dp = dp,
		};
	}
}
