using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class SmVersionCheckTests
{
	[Fact]
	public void WritePayload_IncompatibleClientVersionWritesJavaAnswerIdOnly()
	{
		Assert.Equal(207, SmVersionCheck.InternalVersion);

		var payload = SerializeUnencryptedPayload(new SmVersionCheck(206, EventTheme.None));

		Assert.Equal([0x01], payload);
	}

	[Fact]
	public void WritePayload_SuccessBranchRemainsExplicitRuntimeBoundary()
	{
		var packet = new SmVersionCheck(SmVersionCheck.InternalVersion, EventTheme.Christmas);
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();

		var exception = Assert.Throws<NotSupportedException>(() => packet.SerializeFrame(crypt));

		Assert.Contains("success payload", exception.Message, StringComparison.Ordinal);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
