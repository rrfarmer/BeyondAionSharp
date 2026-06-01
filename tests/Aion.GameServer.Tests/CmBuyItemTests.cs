using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmBuyItemTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaBuySellOpcodeAsInGameOnly()
	{
		var packet = Assert.IsType<CmBuyItem>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(51, buffer =>
				{
					buffer.WriteD(SellerObjectId);
					buffer.WriteH(2);
					buffer.WriteH(0);
				}),
				GameConnectionState.InGame));

		Assert.Equal(51, packet.OpCode);
		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(51, buffer =>
			{
				buffer.WriteD(SellerObjectId);
				buffer.WriteH(2);
				buffer.WriteH(0);
			}),
			GameConnectionState.Authed));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(13)]
	[InlineData(14)]
	[InlineData(15)]
	[InlineData(16)]
	[InlineData(17)]
	public void ReadFrom_ReadsTradeListActionsAmountAndItemsLikeJava(int tradeActionId)
	{
		var packet = new CmBuyItem(51, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteD(SellerObjectId);
		buffer.WriteH(tradeActionId);
		buffer.WriteH(2);
		buffer.WriteD(100000001);
		buffer.WriteQ(1);
		buffer.WriteD(100000002);
		buffer.WriteQ(5);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(SellerObjectId, packet.SellerObjectId);
		Assert.Equal(tradeActionId, packet.TradeActionId);
		Assert.Equal(2, packet.Amount);
		Assert.False(packet.IsAudit);
		Assert.Null(packet.AuditItem);
		Assert.Equal([new CmBuyItemEntry(100000001, 1), new CmBuyItemEntry(100000002, 5)], packet.Items);
	}

	[Fact]
	public void ReadFrom_AmountAboveJavaMaximumAuditsBeforeReadingItems()
	{
		var packet = new CmBuyItem(51, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteD(SellerObjectId);
		buffer.WriteH(2);
		buffer.WriteH(37);
		buffer.WriteD(101);
		buffer.WriteQ(1);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.True(packet.IsAudit);
		Assert.Equal(37, packet.Amount);
		Assert.Empty(packet.Items);
		Assert.Null(packet.AuditItem);
	}

	[Theory]
	[InlineData(2)]
	[InlineData(13)]
	public void ReadFrom_NegativeCountAuditsAndLeavesOnlyPriorValidItems(int tradeActionId)
	{
		var packet = new CmBuyItem(51, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteD(SellerObjectId);
		buffer.WriteH(tradeActionId);
		buffer.WriteH(2);
		buffer.WriteD(101);
		buffer.WriteQ(1);
		buffer.WriteD(102);
		buffer.WriteQ(-1);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.True(packet.IsAudit);
		Assert.Equal([new CmBuyItemEntry(101, 1)], packet.Items);
		Assert.Equal(new CmBuyItemEntry(102, -1), packet.AuditItem);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(13)]
	[InlineData(14)]
	[InlineData(15)]
	[InlineData(16)]
	[InlineData(17)]
	public void ReadFrom_NonPositiveItemObjectIdAuditsForNonPrivateStoreActions(int tradeActionId)
	{
		var packet = new CmBuyItem(51, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteD(SellerObjectId);
		buffer.WriteH(tradeActionId);
		buffer.WriteH(1);
		buffer.WriteD(0);
		buffer.WriteQ(1);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.True(packet.IsAudit);
		Assert.Empty(packet.Items);
		Assert.Equal(new CmBuyItemEntry(0, 1), packet.AuditItem);
	}

	[Fact]
	public void ReadFrom_PrivateStoreActionAllowsNonPositiveItemIndexAndJavaMaxCount()
	{
		var packet = new CmBuyItem(51, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteD(SellerObjectId);
		buffer.WriteH(0);
		buffer.WriteH(1);
		buffer.WriteD(0);
		buffer.WriteQ(CmBuyItem.MaxItemCount);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.False(packet.IsAudit);
		Assert.Equal([new CmBuyItemEntry(0, CmBuyItem.MaxItemCount)], packet.Items);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(13)]
	[InlineData(14)]
	[InlineData(15)]
	[InlineData(16)]
	[InlineData(17)]
	public void ReadFrom_CountAboveJavaMaximumAudits(int tradeActionId)
	{
		var packet = new CmBuyItem(51, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteD(SellerObjectId);
		buffer.WriteH(tradeActionId);
		buffer.WriteH(1);
		buffer.WriteD(101);
		buffer.WriteQ(20_001);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.True(packet.IsAudit);
		Assert.Empty(packet.Items);
		Assert.Equal(new CmBuyItemEntry(101, 20_001), packet.AuditItem);
	}

	private static byte[] CreateClientPayload(int opcode, Action<PacketBuffer> writePayload)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		writePayload(buffer);
		return buffer.ToArray();
	}

	private const int SellerObjectId = 7001;
}
