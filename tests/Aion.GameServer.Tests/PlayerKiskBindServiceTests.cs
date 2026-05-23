using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKiskBindServiceTests
{
	[Fact]
	public void BindMatchesJavaKiskServiceOwnerMemberSlice()
	{
		var player = new Player { ObjectId = 1001 };
		var kisk = new PlayerKiskRuntimeState(
			objectId: 9001,
			ownerObjectId: 1001,
			npcId: 700273,
			maxMembers: 1);

		var bound = PlayerKiskBindService.Bind(player, kisk);
		var duplicate = PlayerKiskBindService.Bind(player, kisk);
		var otherPlayer = new Player { ObjectId = 1002 };
		var full = PlayerKiskBindService.Bind(otherPlayer, kisk);

		Assert.Equal(PlayerKiskBindStatus.Bound, bound.Status);
		Assert.True(bound.IsBound);
		Assert.Equal(9001, player.BoundKiskObjectId);
		Assert.Equal(1, kisk.CurrentMemberCount);
		Assert.Equal(PlayerKiskBindStatus.AlreadyRegistered, duplicate.Status);
		Assert.Equal(PlayerKiskBindStatus.Full, full.Status);
		Assert.Equal(0, otherPlayer.BoundKiskObjectId);
	}
}
