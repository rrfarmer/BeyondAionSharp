using System.Buffers.Binary;
using Aion.Commons.Network;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Network.Aion;

public static class GameClientPacketFactory
{
	private static readonly PacketInfo?[] Packets = new PacketInfo[250];

	static GameClientPacketFactory()
	{
		// Java parity: network/aion/AionClientPacketFactory opcode registration table.
		Register(3, states => new CmQuit(3, states), GameConnectionState.Authed, GameConnectionState.InGame);
		Register(4, states => new CmMayQuit(4, states), GameConnectionState.InGame);
		Register(8, states => new CmEnterWorld(8, states), GameConnectionState.Authed);
		Register(9, states => new CmLevelReady(9, states), GameConnectionState.InGame);
		Register(10, states => new CmUiSettings(10, states), GameConnectionState.InGame);
		Register(12, states => new CmCustomSettings(12, states), GameConnectionState.InGame);
		Register(18, states => new CmTimeCheck(18, states), GameConnectionState.Connected, GameConnectionState.Authed, GameConnectionState.InGame);
		Register(27, states => new CmChatMessagePublic(27, states), GameConnectionState.InGame);
		Register(28, states => new CmChatMessageWhisper(28, states), GameConnectionState.InGame);
		Register(31, states => new CmTargetSelect(31, states), GameConnectionState.InGame);
		Register(39, states => new CmChatPlayerInfo(39, states), GameConnectionState.InGame);
		Register(44, states => new CmPing(44, states), GameConnectionState.Authed, GameConnectionState.InGame);
		Register(48, states => new CmMove(48, states), GameConnectionState.InGame);
		Register(49, states => new CmMoveInAir(49, states), GameConnectionState.InGame);
		Register(50, states => new CmQuestionResponse(50, states), GameConnectionState.InGame);
		Register(58, states => new CmSetNote(58, states), GameConnectionState.InGame);
		Register(61, states => new CmChatGroupInfo(61, states), GameConnectionState.InGame);
		Register(71, states => new CmMotion(71, states), GameConnectionState.InGame);
		Register(73, states => new CmHouseSettings(73, states), GameConnectionState.InGame);
		Register(103, states => new CmPingRequest(103, states), GameConnectionState.InGame);
		Register(110, states => new CmMarkFriendList(110, states), GameConnectionState.InGame);
		Register(111, states => new CmFriendAdd(111, states), GameConnectionState.InGame);
		Register(112, states => new CmFriendDelete(112, states), GameConnectionState.InGame);
		Register(117, states => new CmBrokerSellWindow(117, states), GameConnectionState.InGame);
		Register(123, states => new CmBrokerList(123, states), GameConnectionState.InGame);
		Register(124, states => new CmBrokerSearch(124, states), GameConnectionState.InGame);
		Register(125, states => new CmBrokerRegistered(125, states), GameConnectionState.InGame);
		Register(126, states => new CmBuyBrokerItem(126, states), GameConnectionState.InGame);
		Register(127, states => new CmRegisterBrokerItem(127, states), GameConnectionState.InGame);
		Register(128, states => new CmBrokerCancelRegistered(128, states), GameConnectionState.InGame);
		Register(129, states => new CmBrokerSettleList(129, states), GameConnectionState.InGame);
		Register(130, states => new CmBrokerSettleAccount(130, states), GameConnectionState.InGame);
		Register(132, states => new CmSendMail(132, states), GameConnectionState.InGame);
		Register(133, states => new CmCheckMailList(133, states), GameConnectionState.InGame);
		Register(134, states => new CmReadMail(134, states), GameConnectionState.InGame);
		Register(136, states => new CmGetMailAttachment(136, states), GameConnectionState.InGame);
		Register(137, states => new CmDeleteMail(137, states), GameConnectionState.InGame);
		Register(139, states => new CmTitleSet(139, states), GameConnectionState.InGame);
		Register(146, states => new CmRejectRevive(146, states), GameConnectionState.InGame);
		Register(147, states => new CmHeadingUpdate(147, states), GameConnectionState.InGame);
		Register(149, states => new CmL2AuthLoginCheck(149, states), GameConnectionState.Connected);
		Register(150, states => new CmCharacterList(150, states), GameConnectionState.Authed);
		Register(151, states => new CmCreateCharacter(151, states), GameConnectionState.Authed);
		Register(152, states => new CmDeleteCharacter(152, states), GameConnectionState.Authed);
		Register(153, states => new CmRestoreCharacter(153, states), GameConnectionState.Authed);
		Register(158, states => new CmShowBlockList(158, states), GameConnectionState.InGame);
		Register(162, states => new CmReadExpressMail(162, states), GameConnectionState.InGame);
		Register(166, states => new CmBlockAdd(166, states), GameConnectionState.InGame);
		Register(167, states => new CmBlockDelete(167, states), GameConnectionState.InGame);
		Register(170, states => new CmFriendStatus(170, states), GameConnectionState.InGame);
		Register(174, states => new CmChatAuth(174, states), GameConnectionState.InGame);
		Register(179, states => new CmBlockSetReason(179, states), GameConnectionState.InGame);
		Register(183, states => new CmReconnectAuth(183, states), GameConnectionState.Authed);
		Register(186, states => new CmMayLoginIntoGame(186, states), GameConnectionState.Authed);
		Register(189, states => new CmMacAddress(189, states), GameConnectionState.Connected);
		Register(210, states => new CmCharacterPasskey(210, states), GameConnectionState.Authed);
		Register(218, states => new CmGetHouseBids(218, states), GameConnectionState.InGame);
		Register(219, states => new CmRegisterHouse(219, states), GameConnectionState.InGame);
		Register(221, states => new CmPlaceBid(221, states), GameConnectionState.InGame);
		Register(223, states => new CmHousePayRent(223, states), GameConnectionState.InGame);
		Register(230, states => new CmShowFriendList(230, states), GameConnectionState.InGame);
		Register(233, states => new CmBonusTitle(233, states), GameConnectionState.InGame);
		Register(239, states => new CmFriendSetMemo(239, states), GameConnectionState.InGame);
	}

	public static GameClientPacket? TryCreatePacket(ReadOnlySpan<byte> decryptedPayload, GameConnectionState state)
	{
		// Java parity: AionClientPacketFactory handling encoded opcode + valid connection state.
		if (decryptedPayload.Length < 5 || !ValidateClientHeader(decryptedPayload))
			return null;

		var encodedOpcode = BinaryPrimitives.ReadUInt16LittleEndian(decryptedPayload[..2]);
		var opcode = GameCrypt.DecodeClientPacketOpcode(encodedOpcode);
		var packetInfo = opcode < 0 || opcode >= Packets.Length ? null : Packets[opcode];
		if (packetInfo == null || !packetInfo.ValidStates.Contains(state))
			return null;

		using var buffer = new PacketBuffer(decryptedPayload[5..].ToArray(), strictReads: false);
		var packet = packetInfo.Create(packetInfo.ValidStates);
		packet.ReadFrom(buffer);
		return packet;
	}

	private static void Register(int opcode, Func<IReadOnlySet<GameConnectionState>, GameClientPacket> create, params GameConnectionState[] validStates)
	{
		Packets[opcode] = new PacketInfo(create, validStates.ToHashSet());
	}

	private static bool ValidateClientHeader(ReadOnlySpan<byte> payload)
	{
		var encodedOpcode = BinaryPrimitives.ReadUInt16LittleEndian(payload[..2]);
		var flippedOpcode = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(3, 2));
		return payload[2] == 0x65 && (ushort)~encodedOpcode == flippedOpcode;
	}

	private sealed record PacketInfo(Func<IReadOnlySet<GameConnectionState>, GameClientPacket> Create, IReadOnlySet<GameConnectionState> ValidStates);
}
