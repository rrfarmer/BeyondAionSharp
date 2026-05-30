using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class SmExchangeAddItemPacketTests
{
	[Fact]
	public void SmExchangeAddItem_OpcodeIs75()
	{
		Assert.Equal(75, SmExchangeAddItem.PacketOpCode);
	}

	[Fact]
	public void SmExchangeAddItem_ActionConstants()
	{
		Assert.Equal(0, SmExchangeAddItem.ActionSelf);
		Assert.Equal(1, SmExchangeAddItem.ActionOther);
	}
}
