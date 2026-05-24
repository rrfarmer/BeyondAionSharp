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
		Register(5, states => new CmRevive(5, states), GameConnectionState.InGame);
		Register(8, states => new CmEnterWorld(8, states), GameConnectionState.Authed);
		Register(9, states => new CmLevelReady(9, states), GameConnectionState.InGame);
		Register(10, states => new CmUiSettings(10, states), GameConnectionState.InGame);
		Register(11, states => new CmObjectSearch(11, states), GameConnectionState.InGame);
		Register(12, states => new CmCustomSettings(12, states), GameConnectionState.InGame);
		Register(15, states => new CmTeleportAnimationDone(15, states), GameConnectionState.InGame);
		Register(17, states => new CmPositionSelf(17, states), GameConnectionState.InGame);
		Register(18, states => new CmTimeCheck(18, states), GameConnectionState.Connected, GameConnectionState.Authed, GameConnectionState.InGame);
		Register(27, states => new CmChatMessagePublic(27, states), GameConnectionState.InGame);
		Register(28, states => new CmChatMessageWhisper(28, states), GameConnectionState.InGame);
		Register(31, states => new CmTargetSelect(31, states), GameConnectionState.InGame);
		Register(32, states => new CmAttack(32, states), GameConnectionState.InGame);
		Register(33, states => new CmCastSpell(33, states), GameConnectionState.InGame);
		Register(37, states => new CmUseItem(37, states), GameConnectionState.InGame);
		Register(38, states => new CmEquipItem(38, states), GameConnectionState.InGame);
		Register(39, states => new CmChatPlayerInfo(39, states), GameConnectionState.InGame);
		Register(40, states => new CmPlayerListener(40, states), GameConnectionState.InGame);
		Register(43, states => new CmEmotion(43, states), GameConnectionState.InGame);
		Register(44, states => new CmPing(44, states), GameConnectionState.Authed, GameConnectionState.InGame);
		Register(48, states => new CmMove(48, states), GameConnectionState.InGame);
		Register(49, states => new CmMoveInAir(49, states), GameConnectionState.InGame);
		Register(50, states => new CmQuestionResponse(50, states), GameConnectionState.InGame);
		Register(52, states => new CmShowDialog(52, states), GameConnectionState.InGame);
		Register(54, states => new CmDialogSelect(54, states), GameConnectionState.InGame);
		Register(58, states => new CmSetNote(58, states), GameConnectionState.InGame);
		Register(61, states => new CmChatGroupInfo(61, states), GameConnectionState.InGame);
		Register(62, states => new CmCheckPak(62, states), GameConnectionState.InGame);
		Register(63, states => new CmExchangeRequest(63, states), GameConnectionState.InGame);
		Register(67, states => new CmExchangeLock(67, states), GameConnectionState.InGame);
		Register(68, states => new CmExchangeOk(68, states), GameConnectionState.InGame);
		Register(69, states => new CmExchangeCancel(69, states), GameConnectionState.InGame);
		Register(71, states => new CmMotion(71, states), GameConnectionState.InGame);
		Register(72, states => new CmHouseKick(72, states), GameConnectionState.InGame);
		Register(73, states => new CmHouseSettings(73, states), GameConnectionState.InGame);
		Register(74, states => new CmManastone(74, states), GameConnectionState.InGame);
		Register(75, states => new CmHouseDecorate(75, states), GameConnectionState.InGame);
		Register(78, states => new CmChargeItem(78, states), GameConnectionState.InGame);
		Register(80, states => new CmDeleteQuest(80, states), GameConnectionState.InGame);
		Register(81, states => new CmPlayMovieEnd(81, states), GameConnectionState.InGame);
		Register(82, states => new CmHouseEdit(82, states), GameConnectionState.InGame);
		Register(89, states => new CmRecipeDelete(89, states), GameConnectionState.InGame);
		Register(90, states => new CmItemRemodel(90, states), GameConnectionState.InGame);
		Register(92, states => new CmSecurityToken(92, states), GameConnectionState.Connected, GameConnectionState.Authed, GameConnectionState.InGame);
		Register(96, states => new CmPlayerStatusInfo(96, states), GameConnectionState.InGame);
		Register(97, states => new CmInviteToGroup(97, states), GameConnectionState.InGame);
		Register(103, states => new CmPingRequest(103, states), GameConnectionState.InGame);
		Register(107, states => new CmClientCommandRoll(107, states), GameConnectionState.InGame);
		Register(110, states => new CmMarkFriendList(110, states), GameConnectionState.InGame);
		Register(111, states => new CmFriendAdd(111, states), GameConnectionState.InGame);
		Register(112, states => new CmFriendDelete(112, states), GameConnectionState.InGame);
		Register(114, states => new CmDuelRequest(114, states), GameConnectionState.InGame);
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
		Register(145, states => new CmQuestionnaire(145, states), GameConnectionState.InGame);
		Register(146, states => new CmRejectRevive(146, states), GameConnectionState.InGame);
		Register(147, states => new CmHeadingUpdate(147, states), GameConnectionState.InGame);
		Register(149, states => new CmL2AuthLoginCheck(149, states), GameConnectionState.Connected);
		Register(150, states => new CmCharacterList(150, states), GameConnectionState.Authed);
		Register(151, states => new CmCreateCharacter(151, states), GameConnectionState.Authed);
		Register(152, states => new CmDeleteCharacter(152, states), GameConnectionState.Authed);
		Register(153, states => new CmRestoreCharacter(153, states), GameConnectionState.Authed);
		Register(154, states => new CmStartLoot(154, states), GameConnectionState.InGame);
		Register(155, states => new CmLootItem(155, states), GameConnectionState.InGame);
		Register(158, states => new CmShowBlockList(158, states), GameConnectionState.InGame);
		Register(162, states => new CmReadExpressMail(162, states), GameConnectionState.InGame);
		Register(163, states => new CmSubzoneChange(163, states), GameConnectionState.InGame);
		Register(166, states => new CmBlockAdd(166, states), GameConnectionState.InGame);
		Register(167, states => new CmBlockDelete(167, states), GameConnectionState.InGame);
		Register(170, states => new CmFriendStatus(170, states), GameConnectionState.InGame);
		Register(172, states => new CmChangeChannel(172, states), GameConnectionState.InGame);
		Register(174, states => new CmChatAuth(174, states), GameConnectionState.InGame);
		Register(175, states => new CmMacroCreate(175, states), GameConnectionState.InGame);
		Register(176, states => new CmMacroDelete(176, states), GameConnectionState.InGame);
		Register(177, states => new CmCheckNickname(177, states), GameConnectionState.Authed);
		Register(179, states => new CmBlockSetReason(179, states), GameConnectionState.InGame);
		Register(181, states => new CmShowBrand(181, states), GameConnectionState.InGame);
		Register(183, states => new CmReconnectAuth(183, states), GameConnectionState.Authed);
		Register(186, states => new CmMayLoginIntoGame(186, states), GameConnectionState.Authed);
		Register(189, states => new CmMacAddress(189, states), GameConnectionState.Connected);
		Register(191, states => new CmReportPlayer(191, states), GameConnectionState.InGame);
		Register(192, states => new CmInstanceInfo(192, states), GameConnectionState.InGame);
		Register(194, states => new CmShowRestrictions(194, states), GameConnectionState.InGame);
		Register(196, states => new CmShowMap(196, states), GameConnectionState.InGame);
		Register(197, states => new CmAppearance(197, states), GameConnectionState.InGame);
		Register(203, states => new CmSummonAttack(203, states), GameConnectionState.InGame);
		Register(205, states => new CmSummonCastSpell(205, states), GameConnectionState.InGame);
		Register(208, states => new CmCompositeStones(208, states), GameConnectionState.InGame);
		Register(209, states => new CmTimeCheckQuit(209, states), GameConnectionState.InGame);
		Register(210, states => new CmCharacterPasskey(210, states), GameConnectionState.Authed);
		Register(213, states => new CmCheckMailUnknown(213, states), GameConnectionState.InGame);
		Register(218, states => new CmGetHouseBids(218, states), GameConnectionState.InGame);
		Register(219, states => new CmRegisterHouse(219, states), GameConnectionState.InGame);
		Register(221, states => new CmPlaceBid(221, states), GameConnectionState.InGame);
		Register(223, states => new CmHousePayRent(223, states), GameConnectionState.InGame);
		Register(224, states => new CmUseHouseObject(224, states), GameConnectionState.InGame);
		Register(225, states => new CmReleaseObject(225, states), GameConnectionState.InGame);
		Register(230, states => new CmShowFriendList(230, states), GameConnectionState.InGame);
		Register(233, states => new CmBonusTitle(233, states), GameConnectionState.InGame);
		Register(234, states => new CmUseChargeSkill(234, states), GameConnectionState.InGame);
		Register(236, states => new CmSelectDecomposable(236, states), GameConnectionState.InGame);
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
