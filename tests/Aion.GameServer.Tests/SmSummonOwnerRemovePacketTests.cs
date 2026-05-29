using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SmSummonOwnerRemovePacketTests
{
	[Fact]
	public void SmSummonOwnerRemove_WritesSummonObjectIdLikeJava()
	{
		var packet = new SmSummonOwnerRemove(summonObjectId: 8001);

		AssertSmSummonOwnerRemovePayload(packet, summonObjectId: 8001);
	}

	[Fact]
	public void CreateSendToMasterPlan_CreatesPacketIntentForSummonsServiceRelease()
	{
		var plan = SummonOwnerRemovePacketPlanService.CreateSendToMasterPlan(summonObjectId: 8001);

		Assert.Equal(SummonOwnerRemovePacketPlanStatus.PacketCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldSendToMaster);
		Assert.NotNull(plan.Packet);
		Assert.Contains("SummonsService.ReleaseSummonTask.run", plan.JavaSource);
		AssertSmSummonOwnerRemovePayload(plan.Packet!, summonObjectId: 8001);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void CreateSendToMasterPlan_BlocksInvalidSummonObjectIdBeforePacketCreation(int summonObjectId)
	{
		var plan = SummonOwnerRemovePacketPlanService.CreateSendToMasterPlan(summonObjectId);

		Assert.Equal(SummonOwnerRemovePacketPlanStatus.BlockedInvalidSummonObjectId, plan.Status);
		Assert.False(plan.ShouldSendToMaster);
		Assert.Null(plan.Packet);
	}

	private static void AssertSmSummonOwnerRemovePayload(SmSummonOwnerRemove packet, int summonObjectId)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmSummonOwnerRemove.PacketOpCode, packet.OpCode);
		Assert.Equal(summonObjectId, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
