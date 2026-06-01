using System.Text;
using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmCharacterPasskeyTests
{
	[Fact]
	public void ReadFrom_HighBitTypeIsSignedShortAndDoesNotReadNewPasskey()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH(0xffff);
		WriteFixedUtf16Bytes(buffer, "old-pass");
		WriteFixedUtf16Bytes(buffer, "new-pass");

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(-1, packet.Type);
		Assert.Equal("old-pass", packet.Passkey);
		Assert.Equal(string.Empty, packet.NewPasskey);
	}

	[Fact]
	public void ReadFrom_UpdateTypeReadsNewPasskey()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH(2);
		WriteFixedUtf16Bytes(buffer, "old-pass");
		WriteFixedUtf16Bytes(buffer, "new-pass");

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(2, packet.Type);
		Assert.Equal("old-pass", packet.Passkey);
		Assert.Equal("new-pass", packet.NewPasskey);
	}

	private static CmCharacterPasskey CreatePacket()
	{
		return new CmCharacterPasskey(210, new HashSet<GameConnectionState> { GameConnectionState.Authed });
	}

	private static void WriteFixedUtf16Bytes(PacketBuffer buffer, string value)
	{
		var bytes = new byte[48];
		var encoded = Encoding.Unicode.GetBytes(value);
		Array.Copy(encoded, bytes, Math.Min(encoded.Length, bytes.Length));
		buffer.WriteB(bytes);
	}
}
