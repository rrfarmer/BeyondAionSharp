using Aion.Commons.Network;
using Aion.LoginServer.Network.GameServer;
using Aion.LoginServer.Network.GameServer.ClientPackets;

namespace Aion.LoginServer.Tests;

public sealed class PlayerTransferControlProtocolTests
{
	[Fact]
	public void GsFactory_DispatchesJavaGoldenPlayerTransferPayload()
	{
		var payload = Convert.FromHexString("0D02443322116E006F00700065000000");

		var packet = Assert.IsType<CmPlayerTransferControl>(
			GsClientPacketFactory.Create(new PacketBuffer(payload), GameServerConnectionState.Authed));
		Assert.Equal(2, packet.ActionId);
		Assert.Equal(0x11223344, packet.TaskId);
		Assert.Equal("nope", packet.Reason);
	}
}
