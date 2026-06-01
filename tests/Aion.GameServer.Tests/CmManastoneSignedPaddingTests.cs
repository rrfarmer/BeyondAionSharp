using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmManastoneSignedPaddingTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaManastoneOpcodeAsInGameOnly()
	{
		Assert.IsType<CmManastone>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(74, buffer =>
				{
					buffer.WriteC(3);
					buffer.WriteC(1);
					buffer.WriteD(7001);
					buffer.WriteC(4);
					buffer.WriteC(0);
					buffer.WriteH(0xffff);
					buffer.WriteD(9001);
				}),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(74, buffer =>
			{
				buffer.WriteC(3);
				buffer.WriteC(1);
				buffer.WriteD(7001);
				buffer.WriteC(4);
				buffer.WriteC(0);
				buffer.WriteH(0xffff);
				buffer.WriteD(9001);
			}),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_RemoveManastoneHighBitPaddingDoesNotShiftNpcObjectId()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(3);
		buffer.WriteC(1);
		buffer.WriteD(7001);
		buffer.WriteC(4);
		buffer.WriteC(0x7f);
		buffer.WriteH(0xffff);
		buffer.WriteD(9001);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(3, packet.ActionType);
		Assert.Equal(1, packet.TargetFusedSlot);
		Assert.Equal(7001, packet.TargetItemObjectId);
		Assert.Equal(4, packet.SlotNumber);
		Assert.Equal(9001, packet.NpcObjectId);
	}

	private static CmManastone CreatePacket()
	{
		return new CmManastone(74, new HashSet<GameConnectionState> { GameConnectionState.InGame });
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
