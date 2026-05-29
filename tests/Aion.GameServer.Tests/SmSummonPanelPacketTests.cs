using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SmSummonPanelPacketTests
{
	[Fact]
	public void SmSummonPanel_WritesSnapshotFieldsLikeJava()
	{
		var snapshot = CreateSnapshot();
		var packet = new SmSummonPanel(snapshot);

		AssertSmSummonPanelPayload(packet, snapshot);
	}

	[Fact]
	public void SmSummonPanel_AllowsZeroStatsAndLiveTimeLikeJavaPrimitiveGetters()
	{
		var snapshot = new SummonPanelSnapshot(
			ObjectId: 8001,
			Level: 0,
			CurrentHp: 0,
			MaxHp: 0,
			MainHandPhysicalAttack: 0,
			PhysicalDefense: 0,
			MagicResist: 0,
			LiveTime: 0);

		AssertSmSummonPanelPayload(new SmSummonPanel(snapshot), snapshot);
	}

	[Fact]
	public void CreateSendToMasterPlan_CreatesPacketIntentForSummonsServiceCreateSummon()
	{
		var snapshot = CreateSnapshot();

		var plan = SummonPanelPacketPlanService.CreateSendToMasterPlan(snapshot);

		Assert.Equal(SummonPanelPacketPlanStatus.PacketCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldSendToMaster);
		Assert.NotNull(plan.Packet);
		Assert.Contains("SummonsService.createSummon", plan.JavaSource);
		Assert.Equal(snapshot, plan.Snapshot);
		AssertSmSummonPanelPayload(plan.Packet!, snapshot);
	}

	[Fact]
	public void CreateSendToMasterPlan_BlocksInvalidSnapshotBeforePacketCreation()
	{
		var snapshot = CreateSnapshot() with { ObjectId = 0 };

		var plan = SummonPanelPacketPlanService.CreateSendToMasterPlan(snapshot);

		Assert.Equal(SummonPanelPacketPlanStatus.BlockedInvalidSnapshot, plan.Status);
		Assert.False(plan.ShouldSendToMaster);
		Assert.Null(plan.Packet);
	}

	[Fact]
	public void CreateSendToMasterPlan_BlocksNegativeStatsBeforePacketCreation()
	{
		var snapshot = CreateSnapshot() with { PhysicalDefense = -1 };

		var plan = SummonPanelPacketPlanService.CreateSendToMasterPlan(snapshot);

		Assert.Equal(SummonPanelPacketPlanStatus.BlockedInvalidSnapshot, plan.Status);
		Assert.False(plan.ShouldSendToMaster);
		Assert.Null(plan.Packet);
	}

	private static SummonPanelSnapshot CreateSnapshot()
	{
		return new SummonPanelSnapshot(
			ObjectId: 8001,
			Level: 55,
			CurrentHp: 7100,
			MaxHp: 8200,
			MainHandPhysicalAttack: 415,
			PhysicalDefense: 771,
			MagicResist: 490,
			LiveTime: 120);
	}

	private static void AssertSmSummonPanelPayload(SmSummonPanel packet, SummonPanelSnapshot snapshot)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmSummonPanel.PacketOpCode, packet.OpCode);
		Assert.Equal(snapshot.ObjectId, reader.ReadD());
		Assert.Equal(snapshot.Level, reader.ReadH());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(snapshot.CurrentHp, reader.ReadD());
		Assert.Equal(snapshot.MaxHp, reader.ReadD());
		Assert.Equal(snapshot.MainHandPhysicalAttack, reader.ReadD());
		Assert.Equal(snapshot.PhysicalDefense, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(snapshot.MagicResist, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(snapshot.LiveTime, reader.ReadD());
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
