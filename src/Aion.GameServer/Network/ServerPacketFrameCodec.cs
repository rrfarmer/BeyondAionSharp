using System.Buffers.Binary;
using Aion.Commons.Network;

namespace Aion.GameServer.Network;

public static class ServerPacketFrameCodec
{
	public static byte[] CreateFrame(ReadOnlySpan<byte> payload)
	{
		if (payload.Length > ushort.MaxValue - 2)
			throw new ArgumentOutOfRangeException(nameof(payload), "Packet payload is too large for the Aion length prefix.");

		var frame = new byte[payload.Length + 2];
		BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(0, 2), (ushort)frame.Length);
		payload.CopyTo(frame.AsSpan(2));
		return frame;
	}

	public static PacketBuffer CreatePayloadBuffer(byte[] frame, bool strictReads = false)
	{
		if (frame.Length < 3)
			throw new ArgumentException("Frame must include a two-byte length and at least one opcode byte.", nameof(frame));

		var declaredLength = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(0, 2));
		if (declaredLength != frame.Length)
			throw new InvalidOperationException($"Frame length mismatch. Declared {declaredLength}, actual {frame.Length}.");

		return new PacketBuffer(frame[2..], strictReads);
	}
}
