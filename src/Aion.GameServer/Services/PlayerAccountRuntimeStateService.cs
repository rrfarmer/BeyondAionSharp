using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class PlayerAccountRuntimeStateService
{
	public static void ApplyLoginAccountState(Player player, PlayerAccountRuntimeState state)
	{
		// Java parity: AccountService.getAccount stores access level, membership, and creationDate
		// on model/account/Account before Player.getAccount() exposes it to reward services.
		player.AccessLevel = state.AccessLevel;
		player.AccountMembership = state.Membership;
		player.AccountCreationEpochMillis = state.AccountCreationEpochMillis;
	}
}

public readonly record struct PlayerAccountRuntimeState(
	byte AccessLevel,
	byte Membership,
	long? AccountCreationEpochMillis);
