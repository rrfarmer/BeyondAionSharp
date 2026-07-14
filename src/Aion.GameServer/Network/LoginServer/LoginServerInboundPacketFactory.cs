using Aion.Commons.Network;

namespace Aion.GameServer.Network.LoginServer;

/// <summary>
/// Java parity: gameserver/network/loginserver/LsClientPacketFactory and its CM_* packet readers.
/// The factory owns the opcode/state contract so bridge dispatch cannot silently accept a packet in
/// a state where Java would reject it.
/// </summary>
internal static class LoginServerInboundPacketFactory
{
	internal static bool TryCreate(
		PacketBuffer buffer,
		LoginServerState state,
		out LoginServerInboundPacket? packet,
		out byte opcode)
	{
		opcode = buffer.ReadC();
		packet = null;

		if (!IsValidState(opcode, state))
			return false;

		packet = opcode switch
		{
			0x00 => ReadGameServerAuthResponse(buffer),
			0x01 => ReadAccountAuthResponse(buffer),
			0x02 => new KickAccountPacket(buffer.ReadD(), buffer.ReadC() == 1),
			0x03 => new AccountReconnectKeyPacket(buffer.ReadD(), buffer.ReadD()),
			0x04 => new LoginServerControlResponsePacket(
				buffer.ReadC(), buffer.ReadC(), buffer.ReadD(), buffer.ReadD(), buffer.ReadC() == 1),
			0x05 => new BanResponsePacket(
				buffer.ReadC(), buffer.ReadD(), buffer.ReadS(), buffer.ReadD(), buffer.ReadD(), buffer.ReadC() == 1),
			0x08 => new CharacterCountRequestPacket(buffer.ReadD()),
			0x09 => ReadMacBanList(buffer),
			0x0A => ReadHddBanList(buffer),
			0x0B => new LoginServerPingPacket(),
			0x0C => ReadPlayerTransferResponse(buffer),
			_ => null,
		};

		return packet != null;
	}

	private static bool IsValidState(byte opcode, LoginServerState state)
	{
		if (opcode == 0x00)
			return state == LoginServerState.Connected;

		return state == LoginServerState.Authed && opcode is
			0x01 or 0x02 or 0x03 or 0x04 or 0x05 or 0x08 or 0x09 or 0x0A or 0x0B or 0x0C;
	}

	private static GameServerAuthResponsePacket ReadGameServerAuthResponse(PacketBuffer buffer)
	{
		var response = buffer.ReadC();
		return new GameServerAuthResponsePacket(response, response == 0 ? buffer.ReadC() : (byte)0);
	}

	private static AccountAuthResponsePacket ReadAccountAuthResponse(PacketBuffer buffer)
	{
		var accountId = buffer.ReadD();
		var ok = buffer.ReadC() == 1;
		var result = ok
			? new AccountAuthResult(
				accountId,
				Ok: true,
				AccountName: buffer.ReadS(),
				CreationDate: buffer.ReadQ(),
				AccumulatedOnlineTime: buffer.ReadQ(),
				AccumulatedRestTime: buffer.ReadQ(),
				AccessLevel: buffer.ReadC(),
				Membership: buffer.ReadC(),
				AllowedHddSerial: buffer.ReadS())
			: new AccountAuthResult(accountId, Ok: false);

		return new AccountAuthResponsePacket(result);
	}

	private static MacBanListPacket ReadMacBanList(PacketBuffer buffer)
	{
		var count = buffer.ReadD();
		ValidateBanListCount(count, buffer.Remaining, minimumEntrySize: 12, "MAC");
		var entries = new List<MacBanListEntry>();
		for (var i = 0; i < count; i++)
			entries.Add(new MacBanListEntry(buffer.ReadS(), buffer.ReadQ(), buffer.ReadS()));
		return new MacBanListPacket(entries);
	}

	private static HddBanListPacket ReadHddBanList(PacketBuffer buffer)
	{
		var count = buffer.ReadD();
		ValidateBanListCount(count, buffer.Remaining, minimumEntrySize: 10, "HDD");
		var entries = new List<HddBanListEntry>();
		for (var i = 0; i < count; i++)
			entries.Add(new HddBanListEntry(buffer.ReadS(), buffer.ReadQ()));
		return new HddBanListPacket(entries);
	}

	private static void ValidateBanListCount(int count, int remainingBytes, int minimumEntrySize, string listName)
	{
		// Java's packet helpers return empty/zero values after buffer underflow, which the production
		// lenient PacketBuffer mirrors. Bound positive peer counts before looping so a malformed frame
		// cannot turn that parity behavior into unbounded CPU work or collection growth. Negative counts
		// intentionally remain empty loops, matching both Java readers.
		if (count > remainingBytes / minimumEntrySize)
			throw new EndOfStreamException(
				$"{listName}-ban entry count {count} cannot fit in the remaining {remainingBytes} packet bytes.");
	}

	private static PlayerTransferResponsePacket ReadPlayerTransferResponse(PacketBuffer buffer)
	{
		var actionId = buffer.ReadD();
		return actionId switch
		{
			20 => ReadPlayerTransferInfo(buffer),
			21 => new PlayerTransferOkPacket(buffer.ReadD()),
			22 => new PlayerTransferErrorPacket(buffer.ReadD(), buffer.ReadS()),
			23 => new PlayerTransferPerformActionPacket(
				buffer.ReadC(), buffer.ReadC(), buffer.ReadD(), buffer.ReadD(), buffer.ReadD(), buffer.ReadD()),
			24 or 25 or 26 or 27 or 28 => ReadPlayerTransferData(buffer, actionId),
			_ => new UnknownPlayerTransferResponsePacket(actionId),
		};
	}

	private static PlayerTransferInfoPacket ReadPlayerTransferInfo(PacketBuffer buffer)
	{
		var targetAccountId = buffer.ReadD();
		var taskId = buffer.ReadD();
		var name = buffer.ReadS();
		var accountName = buffer.ReadS();
		var length = buffer.ReadD();
		return new PlayerTransferInfoPacket(targetAccountId, taskId, name, accountName, buffer.ReadB(length));
	}

	private static PlayerTransferDataPacket ReadPlayerTransferData(PacketBuffer buffer, int actionId)
	{
		var taskId = buffer.ReadD();
		var length = buffer.ReadD();
		return new PlayerTransferDataPacket(actionId, taskId, buffer.ReadB(length));
	}
}

internal interface ILoginServerInboundPacketDispatcher
{
	void Dispatch(LoginServerInboundPacket packet);
}

internal abstract record LoginServerInboundPacket(byte Opcode);

internal sealed record GameServerAuthResponsePacket(byte Response, byte GameServerCount)
	: LoginServerInboundPacket(0x00);

internal sealed record AccountAuthResponsePacket(AccountAuthResult Result)
	: LoginServerInboundPacket(0x01);

internal sealed record KickAccountPacket(int AccountId, bool NotifyDoubleLogin)
	: LoginServerInboundPacket(0x02);

internal sealed record AccountReconnectKeyPacket(int AccountId, int ReconnectKey)
	: LoginServerInboundPacket(0x03);

internal sealed record LoginServerControlResponsePacket(
	byte Type,
	byte Param,
	int AccountId,
	int AdminObjectId,
	bool Result)
	: LoginServerInboundPacket(0x04);

internal sealed record BanResponsePacket(
	byte Type,
	int AccountId,
	string Ip,
	int Time,
	int AdminObjectId,
	bool Result)
	: LoginServerInboundPacket(0x05);

internal sealed record CharacterCountRequestPacket(int AccountId)
	: LoginServerInboundPacket(0x08);

internal sealed record MacBanListEntry(string Address, long Time, string Details);

internal sealed record MacBanListPacket(IReadOnlyList<MacBanListEntry> Entries)
	: LoginServerInboundPacket(0x09);

internal sealed record HddBanListEntry(string Serial, long Time);

internal sealed record HddBanListPacket(IReadOnlyList<HddBanListEntry> Entries)
	: LoginServerInboundPacket(0x0A);

internal sealed record LoginServerPingPacket() : LoginServerInboundPacket(0x0B);

internal abstract record PlayerTransferResponsePacket(int ActionId)
	: LoginServerInboundPacket(0x0C);

internal sealed record PlayerTransferInfoPacket(
	int TargetAccountId,
	int TaskId,
	string Name,
	string AccountName,
	byte[] CommonData)
	: PlayerTransferResponsePacket(20);

internal sealed record PlayerTransferOkPacket(int TaskId)
	: PlayerTransferResponsePacket(21);

internal sealed record PlayerTransferErrorPacket(int TaskId, string Reason)
	: PlayerTransferResponsePacket(22);

internal sealed record PlayerTransferPerformActionPacket(
	byte SourceServerId,
	byte TargetServerId,
	int SourceAccountId,
	int TargetAccountId,
	int PlayerId,
	int TaskId)
	: PlayerTransferResponsePacket(23);

internal sealed record PlayerTransferDataPacket(int DataActionId, int TaskId, byte[] Data)
	: PlayerTransferResponsePacket(DataActionId);

internal sealed record UnknownPlayerTransferResponsePacket(int UnknownActionId)
	: PlayerTransferResponsePacket(UnknownActionId);
