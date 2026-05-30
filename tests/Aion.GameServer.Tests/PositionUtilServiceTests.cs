using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PositionUtilServiceTests
{
	[Theory]
	[InlineData(0f, 0f, 1f, 0f, 0f, 0)]
	[InlineData(0f, 0f, 0f, 1f, 90f, 30)]
	[InlineData(0f, 0f, -1f, 0f, 180f, 60)]
	[InlineData(0f, 0f, 0f, -1f, 270f, 90)]
	[InlineData(10f, 10f, 11f, 11f, 45f, 15)]
	[InlineData(10f, 10f, 11f, 9f, 315f, 105)]
	public void GetHeadingTowards_UsesJavaAtan2AngleAndTruncatedHeading(
		float x,
		float y,
		float targetX,
		float targetY,
		float expectedAngle,
		int expectedHeading)
	{
		Assert.Equal(expectedAngle, PositionUtilService.CalculateAngleFrom(x, y, targetX, targetY), precision: 5);
		Assert.Equal(expectedHeading, PositionUtilService.GetHeadingTowards(x, y, targetX, targetY));
	}

	[Theory]
	[InlineData(360f, 0f)]
	[InlineData(721.5f, 1.5f)]
	[InlineData(-1f, 359f)]
	[InlineData(-720f, -0f)]
	[InlineData(-721.5f, 358.5f)]
	public void NormalizeAngle_MatchesJavaModuloBranches(float angle, float expected)
	{
		Assert.Equal(expected, PositionUtilService.NormalizeAngle(angle), precision: 5);
	}

	[Theory]
	[InlineData(0, 0f)]
	[InlineData(30, 90f)]
	[InlineData(90, 270f)]
	[InlineData(120, 0f)]
	[InlineData(255, 357f)]
	public void ConvertHeadingToAngle_NormalizesJavaByteHeading(int heading, float expectedAngle)
	{
		Assert.Equal(expectedAngle, PositionUtilService.ConvertHeadingToAngle((byte)heading), precision: 5);
	}

	[Theory]
	[InlineData(0f, 0)]
	[InlineData(1.5f, 0)]
	[InlineData(45f, 15)]
	[InlineData(359.9f, 119)]
	public void ConvertAngleToHeading_TruncatesLikeJavaByteCast(float angle, int expectedHeading)
	{
		Assert.Equal(expectedHeading, PositionUtilService.ConvertAngleToHeading(angle));
	}

	[Theory]
	[InlineData(3.49f, true)]
	[InlineData(3.5f, false)]
	public void IsInNpcTalkRange_UsesJavaStrictSquaredRange(float xOffset, bool expected)
	{
		var player = new WorldPosition(210010000, 0, 0, 0, 0, InstanceId: 1);
		var npc = new WorldPosition(210010000, xOffset, 0, 0, 0, InstanceId: 1);

		var result = PositionUtilService.IsInNpcTalkRange(
			player,
			npc,
			npcTalkDistance: 2,
			npcBoundRadius: 0.5f);

		Assert.Equal(expected, result);
	}

	[Fact]
	public void IsInNpcTalkRange_RejectsDifferentWorldOrInstance()
	{
		var player = new WorldPosition(210010000, 0, 0, 0, 0, InstanceId: 1);
		var sameDistanceDifferentWorld = new WorldPosition(220010000, 1, 0, 0, 0, InstanceId: 1);
		var sameDistanceDifferentInstance = new WorldPosition(210010000, 1, 0, 0, 0, InstanceId: 2);

		Assert.False(PositionUtilService.IsInNpcTalkRange(player, sameDistanceDifferentWorld, 2, 0.5f));
		Assert.False(PositionUtilService.IsInNpcTalkRange(player, sameDistanceDifferentInstance, 2, 0.5f));
	}

	[Fact]
	public void IsInNpcTalkRange_AddsCreatureAndNpcBoundRadiiLikeJavaNonCenterRange()
	{
		var player = new WorldPosition(210010000, 0, 0, 0, 0, InstanceId: 1);
		var npc = new WorldPosition(210010000, 3.75f, 0, 0, 0, InstanceId: 1);

		Assert.False(PositionUtilService.IsInNpcTalkRange(player, npc, npcTalkDistance: 2, npcBoundRadius: 0.5f));
		Assert.True(PositionUtilService.IsInNpcTalkRange(player, npc, npcTalkDistance: 2, npcBoundRadius: 0.5f, creatureBoundRadius: 0.5f));
	}

	[Theory]
	[InlineData(3.24f, true)]
	[InlineData(3.25f, false)]
	public void IsInObjectTalkRange_ModelsHouseObjectTalkingDistance(float xOffset, bool expected)
	{
		var player = new WorldPosition(210010000, 0, 0, 0, 0, InstanceId: 1);
		var houseObject = new WorldPosition(210010000, xOffset, 0, 0, 0, InstanceId: 1);

		var result = PositionUtilService.IsInObjectTalkRange(
			player,
			houseObject,
			targetTalkingDistance: 2,
			targetBoundRadius: 0,
			creatureBoundRadius: Player.DefaultBoundRadius);

		Assert.Equal(expected, result);
	}

	// --- New UOW-1758 tests ---

	[Fact]
	public void GetDistance2D_PythagoreanTriple()
	{
		Assert.Equal(5.0, PositionUtilService.GetDistance2D(0, 0, 3, 4), 6);
	}

	[Fact]
	public void GetDistance3D_PythagoreanTriple()
	{
		Assert.Equal(5.0, PositionUtilService.GetDistance3D(0, 0, 0, 3, 4, 0), 6);
	}

	[Fact]
	public void IsInRange_StrictlyLessThan()
	{
		// Java: dx*dx+dy*dy+dz*dz < range*range (strict)
		Assert.True(PositionUtilService.IsInRange(0, 0, 0, 3, 4, 0, 6f));
		Assert.False(PositionUtilService.IsInRange(0, 0, 0, 3, 4, 0, 5f)); // exactly at range → false
	}

	[Fact]
	public void IsInRangeLimited_WithinBoundsReturnsTrue()
	{
		Assert.True(PositionUtilService.IsInRangeLimited(0, 0, 0, 3, 4, 0, 3f, 8f));
		Assert.False(PositionUtilService.IsInRangeLimited(0, 0, 0, 1, 0, 0, 5f, 10f)); // too close
	}

	[Fact]
	public void CheckAngleDiff_WrapsAroundCorrectly()
	{
		// 350° and 10° → shortest diff = 20° ≤ 90° → true
		Assert.True(PositionUtilService.CheckAngleDiff(350f, 10f, 90f));
		// 90° diff from 0° and 91° → false
		Assert.False(PositionUtilService.CheckAngleDiff(0f, 91f, 90f));
	}

	[Fact]
	public void IsInFrontOf_AlwaysTrueAt180Degrees()
	{
		// Java: if maxAngleDiff >= 180 → always true
		Assert.True(PositionUtilService.IsInFrontOf(100f, 100f, 0f, 0f, (byte)0, 180f));
	}
}
