using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PositionUtilServiceTests
{
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
}
