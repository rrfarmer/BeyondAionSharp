using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmBuyTradeInTradeTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaBuyTradeInTradeOpcodeAsInGameOnly()
	{
		Assert.IsType<CmBuyTradeInTrade>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(88, buffer =>
				{
					buffer.WriteD(7001);
					buffer.WriteC(0x80);
					buffer.WriteD(1001);
					buffer.WriteD(2);
					buffer.WriteH(0);
				}),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(88, buffer =>
			{
				buffer.WriteD(7001);
				buffer.WriteC(0x80);
				buffer.WriteD(1001);
				buffer.WriteD(2);
				buffer.WriteH(0);
			}),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_ReadsUnsignedTradeInListCountAndObjectIdsLikeJava()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteD(7001);
		buffer.WriteC(0x80);
		buffer.WriteD(1001);
		buffer.WriteD(2);
		buffer.WriteH(2);
		buffer.WriteD(2001);
		buffer.WriteD(2002);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(7001, packet.SellerObjectId);
		Assert.Equal(0x80, packet.Mask);
		Assert.Equal(1001, packet.ItemId);
		Assert.Equal(2, packet.Count);
		Assert.Equal(2, packet.TradeInListCount);
		Assert.Equal([2001, 2002], packet.TradeInItemObjectIds);
	}

	private static CmBuyTradeInTrade CreatePacket()
	{
		return new CmBuyTradeInTrade(88, new HashSet<GameConnectionState> { GameConnectionState.InGame });
	}

	private static byte[] CreateClientPayload(int opcode, Action<PacketBuffer> writeBody)
	{
		using var body = new PacketBuffer();
		writeBody(body);
		var bodyBytes = body.ToArray();

		var encodedOpcode = EncodeClientPacketOpcode(opcode);
		using var payload = new PacketBuffer(5 + bodyBytes.Length);
		payload.WriteH(encodedOpcode);
		payload.WriteC(0x65);
		payload.WriteH((ushort)~encodedOpcode);
		payload.WriteB(bodyBytes);
		return payload.ToArray();
	}

	private static int EncodeClientPacketOpcode(int opcode)
	{
		return ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
	}
}
