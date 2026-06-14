using Aion.Commons.Network;

namespace Aion.GameServer.Network.LoginServer.ServerPackets;

/// <summary>
/// Java parity: gameserver/network/loginserver/serverpackets/SM_LS_PONG (opcode 12). Empty-payload reply
/// to the login server's keep-alive ping (CM_LS_PING). Sent so the LS does not drop the GS bridge on ping timeout.
/// </summary>
public sealed class SmLsPong : LoginServerPacket
{
	protected override void WritePayload(PacketBuffer buffer)
	{
		// Java parity: SM_LS_PONG super(12); writeImpl is empty (opcode-only).
		buffer.WriteC(0x0C);
	}
}
