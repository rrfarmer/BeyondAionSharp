using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PrivateStoreOpenPlanServiceTests
{
	[Fact]
	public void CreatePlan_WithMessageBroadcastsStoreName()
	{
		var plan = PrivateStoreOpenPlanService.CreatePlan(playerObjectId: 9001, storeMessage: "Selling manastones");

		Assert.Equal(PrivateStoreOpenPlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldBroadcastStoreName);
		Assert.NotNull(plan.PrivateStoreNamePacket);
		Assert.Equal("Selling manastones", plan.StoreMessage);
		Assert.Contains("SM_PRIVATE_STORE_NAME", plan.JavaSource);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void CreatePlan_EmptyMessageBlocksBroadcast(string? message)
	{
		var plan = PrivateStoreOpenPlanService.CreatePlan(playerObjectId: 9001, storeMessage: message);

		Assert.Equal(PrivateStoreOpenPlanStatus.BlockedEmptyStoreMessage, plan.Status);
		Assert.False(plan.ShouldBroadcastStoreName);
		Assert.Null(plan.PrivateStoreNamePacket);
	}

	[Fact]
	public void SmPrivateStoreName_WritesJavaPayload()
	{
		// Java parity: SM_PRIVATE_STORE_NAME.writeImpl writes playerObjId (int) then store name (UTF-16 string).
		var packet = new SmPrivateStoreName(playerObjectId: 1234, storeMessage: "My Shop");
		var bytes = SerializeUnencryptedPayload(packet);

		using var reader = new PacketBuffer(bytes);
		Assert.Equal(1234, reader.ReadD());
		Assert.Equal("My Shop", reader.ReadS());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void SmPrivateStoreName_OpcodeIs145()
	{
		Assert.Equal(145, SmPrivateStoreName.PacketOpCode);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
