using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public class GamePacketProcessorTests
{
	[Fact]
	public async Task ProcessAsync_PreservesOrderForSameConnection()
	{
		var observed = new List<int>();
		await using var processor = new GamePacketProcessor<int>(async (packet, _) =>
		{
			await Task.Delay(10);
			observed.Add(packet.OpCode);
		});

		var first = processor.ProcessAsync(1, NewPacket(149));
		var second = processor.ProcessAsync(1, NewPacket(150));
		var third = processor.ProcessAsync(1, NewPacket(186));

		await Task.WhenAll(first, second, third);

		Assert.Equal([149, 150, 186], observed);
		Assert.Equal(0, processor.ActiveConnectionQueueCount);
	}

	private static CmMayLoginIntoGame NewPacket(int opcode)
	{
		return new CmMayLoginIntoGame(opcode, new HashSet<GameConnectionState> { GameConnectionState.Authed });
	}
}
