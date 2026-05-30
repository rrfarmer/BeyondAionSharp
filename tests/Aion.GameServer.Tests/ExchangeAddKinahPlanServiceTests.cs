using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ExchangeAddKinahPlanServiceTests
{
	[Fact]
	public void CreatePlan_NormalRequestAddsExactAmount()
	{
		var plan = ExchangeAddKinahPlanService.CreatePlan(
			requestedAmount: 500, inventoryKinah: 1000, alreadyInExchangeKinah: 0);

		Assert.Equal(ExchangeAddKinahPlanStatus.CanAdd, plan.Status);
		Assert.Equal(500, plan.CountToAdd);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldSendToSelf);
		Assert.True(plan.ShouldSendToOther);
		Assert.NotNull(plan.SelfPacket);
		Assert.NotNull(plan.OtherPacket);
	}

	[Fact]
	public void CreatePlan_RequestExceedsAvailableUsesAvailable()
	{
		// availableCount = 1000 - 400 = 600; min(600, 800) = 600
		var plan = ExchangeAddKinahPlanService.CreatePlan(
			requestedAmount: 800, inventoryKinah: 1000, alreadyInExchangeKinah: 400);

		Assert.Equal(ExchangeAddKinahPlanStatus.CanAdd, plan.Status);
		Assert.Equal(600, plan.CountToAdd);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void CreatePlan_ZeroOrNegativeRequestBlocksSilently(long amount)
	{
		var plan = ExchangeAddKinahPlanService.CreatePlan(amount, inventoryKinah: 1000, alreadyInExchangeKinah: 0);

		Assert.Equal(ExchangeAddKinahPlanStatus.BlockedZeroOrNegativeAmount, plan.Status);
		Assert.Equal(0, plan.CountToAdd);
		Assert.Null(plan.SelfPacket);
	}

	[Fact]
	public void CreatePlan_AlreadyAddedAllKinahBlocks()
	{
		// availableCount = 500 - 500 = 0
		var plan = ExchangeAddKinahPlanService.CreatePlan(
			requestedAmount: 100, inventoryKinah: 500, alreadyInExchangeKinah: 500);

		Assert.Equal(ExchangeAddKinahPlanStatus.BlockedNoAvailableKinah, plan.Status);
		Assert.Equal(0, plan.CountToAdd);
	}

	[Fact]
	public void SmExchangeAddKinah_WritesJavaPayload()
	{
		// Java parity: SM_EXCHANGE_ADD_KINAH.writeImpl writes action(C) then kinahCount(Q=long).
		var packet = new SmExchangeAddKinah(kinahCount: 1234567, action: SmExchangeAddKinah.ActionSelf);
		var bytes = SerializeUnencryptedPayload(packet);

		using var reader = new PacketBuffer(bytes);
		Assert.Equal(0, (int)reader.ReadC()); // action = 0 (self)
		Assert.Equal(1234567L, reader.ReadQ()); // kinah count
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void SmExchangeAddKinah_OpcodeIs77()
	{
		Assert.Equal(77, SmExchangeAddKinah.PacketOpCode);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
