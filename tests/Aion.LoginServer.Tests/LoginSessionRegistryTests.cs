using Aion.LoginServer.Model;
using Aion.LoginServer.Network.Aion;
using Aion.LoginServer.Network.Aion.ServerPackets;
using Aion.LoginServer.Services;

namespace Aion.LoginServer.Tests;

public class LoginSessionRegistryTests
{
	[Fact]
	public async Task RegisterLoginSession_StoresSessionByAccountId()
	{
		var registry = new LoginSessionRegistry();
		var session = new FakeLoginSession(1);

		var result = await registry.RegisterLoginSessionAsync(session);

		Assert.Equal(LoginSessionRegisterResult.Registered, result);
		Assert.Same(session, registry.GetLoginSession(1));
	}

	[Fact]
	public async Task RegisterLoginSession_DuplicateKicksExistingSessionAndRejectsNewLogin()
	{
		var registry = new LoginSessionRegistry();
		var existing = new FakeLoginSession(1);
		var incoming = new FakeLoginSession(1);
		await registry.RegisterLoginSessionAsync(existing);

		var result = await registry.RegisterLoginSessionAsync(incoming);

		Assert.Equal(LoginSessionRegisterResult.AlreadyLoggedIn, result);
		Assert.True(existing.Closed);
		Assert.IsType<SmAccountKick>(existing.ClosePacket);
		Assert.Null(registry.GetLoginSession(1));
	}

	[Fact]
	public async Task RemoveLoginSession_RemovesOnlyMatchingSession()
	{
		var registry = new LoginSessionRegistry();
		var session = new FakeLoginSession(1);
		await registry.RegisterLoginSessionAsync(session);

		registry.RemoveLoginSession(session.Account, new FakeLoginSession(1));

		Assert.Same(session, registry.GetLoginSession(1));

		registry.RemoveLoginSession(session.Account, session);

		Assert.Null(registry.GetLoginSession(1));
	}

	[Fact]
	public async Task ConsumeLoginSession_RemovesOnlyMatchingSessionKey()
	{
		var registry = new LoginSessionRegistry();
		var session = new FakeLoginSession(1);
		await registry.RegisterLoginSessionAsync(session);

		var wrong = registry.ConsumeLoginSession(new SessionKey(1, 9, 9, 9));
		var consumed = registry.ConsumeLoginSession(session.SessionKey);

		Assert.Null(wrong);
		Assert.Same(session, consumed);
		Assert.Null(registry.GetLoginSession(1));
	}

	[Fact]
	public void ReconnectingAccount_IsConsumedOnlyByMatchingKey()
	{
		var registry = new LoginSessionRegistry();
		var account = new Account { Id = 7, Name = "player" };
		registry.AddReconnectingAccount(new ReconnectingAccount(account, 1234));

		var ok = registry.TryConsumeReconnectingAccount(7, 1234, out var reconnectingAccount);

		Assert.True(ok);
		Assert.NotNull(reconnectingAccount);
		Assert.Same(account, reconnectingAccount.Account);
		Assert.False(registry.TryConsumeReconnectingAccount(7, 1234, out _));
	}

	[Fact]
	public void CharacterCounts_TracksUntilEveryKnownGameServerResponds()
	{
		var registry = new LoginSessionRegistry();
		registry.BeginGameServerCharacterCountLoad(1, new Dictionary<byte, int> { [2] = 0 });

		Assert.False(registry.HasAllGameServerCharacterCounts(1, 2));

		registry.AddGameServerCharacterCount(1, 1, 3);

		Assert.True(registry.HasAllGameServerCharacterCounts(1, 2));
		var counts = registry.GetGameServerCharacterCounts(1);
		Assert.Equal(3, counts[1]);
		Assert.Equal(0, counts[2]);
	}

	private sealed class FakeLoginSession : ILoginClientSession
	{
		public FakeLoginSession(int accountId)
		{
			Account = new Account { Id = accountId, Name = $"account{accountId}" };
			SessionKey = new SessionKey(accountId, 1, 2, 3);
		}

		public Account Account { get; }

		public SessionKey SessionKey { get; }

		public bool JoinedGameServer { get; init; }

		public bool Closed { get; private set; }

		public AionServerPacket? ClosePacket { get; private set; }

		public AionServerPacket? SentPacket { get; private set; }

		public Task SendPacketAsync(AionServerPacket packet)
		{
			SentPacket = packet;
			return Task.CompletedTask;
		}

		public Task CloseWithPacketAsync(AionServerPacket packet)
		{
			Closed = true;
			ClosePacket = packet;
			return Task.CompletedTask;
		}
	}
}
