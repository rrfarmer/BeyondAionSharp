using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerAccountRuntimeStateServiceTests
{
	[Fact]
	public void ApplyLoginAccountState_CarriesLoginServerCreationMillisToActivePlayer()
	{
		var player = new Player { ObjectId = 1001, AccountId = 10 };
		var state = new PlayerAccountRuntimeState(
			AccessLevel: 3,
			Membership: 10,
			AccountCreationEpochMillis: 1_655_510_400_000L);

		PlayerAccountRuntimeStateService.ApplyLoginAccountState(player, state);

		Assert.Equal(3, player.AccessLevel);
		Assert.Equal(10, player.AccountMembership);
		Assert.Equal(1_655_510_400_000L, player.AccountCreationEpochMillis);
	}

	[Fact]
	public void ApplyLoginAccountState_PreservesMissingCreationMillisAsNull()
	{
		var player = new Player
		{
			ObjectId = 1001,
			AccountId = 10,
			AccessLevel = 1,
			AccountMembership = 2,
			AccountCreationEpochMillis = 1_655_510_400_000L,
		};
		var state = new PlayerAccountRuntimeState(
			AccessLevel: 0,
			Membership: 0,
			AccountCreationEpochMillis: null);

		PlayerAccountRuntimeStateService.ApplyLoginAccountState(player, state);

		Assert.Equal(0, player.AccessLevel);
		Assert.Equal(0, player.AccountMembership);
		Assert.Null(player.AccountCreationEpochMillis);
	}
}
