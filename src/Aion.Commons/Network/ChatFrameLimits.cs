namespace Aion.Commons.Network;

/// <summary>
/// Java Chat <c>PacketFrameDecoder</c> boundary. The two-byte length includes the
/// length field itself and every valid frame contains at least one opcode byte.
/// </summary>
public static class ChatFrameLimits
{
	public const int MaxPacketLength = 8192 * 2;

	public static bool IsValid(int frameLength)
	{
		return frameLength is >= 3 and <= MaxPacketLength;
	}
}
