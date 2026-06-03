using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmGatherTests
{
	// Java parity: CM_GATHER registered at opcode 19 in AionClientPacketFactory (InGame only).
	[Fact]
	public void TryCreatePacket_RegistersJavaGatherOpcodeAsInGameOnly()
	{
		Assert.IsType<CmGather>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(19, buffer => buffer.WriteD(0)),
				GameConnectionState.InGame));

		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(19, buffer => buffer.WriteD(0)),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_ParsesActionIdZeroAsStartGather()
	{
		// Java parity: CM_GATHER.readImpl actionId = readD(); 0 = start gathering.
		var packet = CreatePacket(actionId: 0);

		Assert.Equal(0, packet.ActionId);
	}

	[Fact]
	public void ReadFrom_ParsesActionIdNegativeOneAsCancelGather()
	{
		// Java parity: CM_GATHER.readImpl actionId -1 = cancel gathering.
		var packet = CreatePacket(actionId: -1);

		Assert.Equal(-1, packet.ActionId);
	}

	[Fact]
	public void ReadFrom_ParsesActionId128AsAttackChatStartGather()
	{
		// Java parity: CM_GATHER.readImpl actionId 128 = start via /attack chat command.
		var packet = CreatePacket(actionId: 128);

		Assert.Equal(128, packet.ActionId);
	}

	private static CmGather CreatePacket(int actionId)
	{
		var packet = new CmGather(19, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteD(actionId);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
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
