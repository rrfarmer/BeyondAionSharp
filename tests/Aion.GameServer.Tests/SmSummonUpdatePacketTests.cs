using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SmSummonUpdatePacketTests
{
	[Fact]
	public void SmSummonUpdate_WritesCurrentAndBaseStatsLikeJava()
	{
		var snapshot = CreateSnapshot();

		AssertSmSummonUpdatePayload(new SmSummonUpdate(snapshot), snapshot);
	}

	[Theory]
	[InlineData(SummonUpdateModeId.Attack, 0)]
	[InlineData(SummonUpdateModeId.Guard, 1)]
	[InlineData(SummonUpdateModeId.Rest, 2)]
	[InlineData(SummonUpdateModeId.Release, 3)]
	[InlineData(SummonUpdateModeId.Unknown, 5)]
	public void SmSummonUpdate_WritesJavaSummonModeIds(SummonUpdateModeId mode, int modeId)
	{
		var snapshot = CreateSnapshot() with { Mode = mode };

		var payload = SerializeUnencryptedPayload(new SmSummonUpdate(snapshot));
		using var reader = new PacketBuffer(payload);

		Assert.Equal(snapshot.Level, reader.ReadC());
		Assert.Equal(modeId, reader.ReadH());
	}

	[Fact]
	public void CreateSendToMasterPlan_CreatesPacketIntentForSummonModeUpdates()
	{
		var snapshot = CreateSnapshot();

		var plan = SummonUpdatePacketPlanService.CreateSendToMasterPlan(
			snapshot,
			"SummonsService.guardMode -> PacketSendUtility.sendPacket(master, new SM_SUMMON_UPDATE(summon))");

		Assert.Equal(SummonUpdatePacketPlanStatus.PacketCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldSendToMaster);
		Assert.False(plan.ShouldBroadcastFromSummon);
		Assert.NotNull(plan.Packet);
		Assert.Contains("SummonsService.guardMode", plan.JavaSource);
		Assert.Equal(snapshot, plan.Snapshot);
		AssertSmSummonUpdatePayload(plan.Packet!, snapshot);
	}

	[Fact]
	public void CreateBroadcastFromSummonPlan_CreatesPacketIntentForCreateSummonBroadcast()
	{
		var snapshot = CreateSnapshot();

		var plan = SummonUpdatePacketPlanService.CreateBroadcastFromSummonPlan(snapshot);

		Assert.Equal(SummonUpdatePacketPlanStatus.PacketCreated, plan.Status);
		Assert.False(plan.ShouldSendToMaster);
		Assert.True(plan.ShouldBroadcastFromSummon);
		Assert.NotNull(plan.Packet);
		Assert.Contains("broadcastPacket", plan.JavaSource);
		AssertSmSummonUpdatePayload(plan.Packet!, snapshot);
	}

	[Fact]
	public void CreateSendToMasterPlan_BlocksInvalidModeBeforePacketCreation()
	{
		var snapshot = CreateSnapshot() with { Mode = (SummonUpdateModeId)4 };

		var plan = SummonUpdatePacketPlanService.CreateSendToMasterPlan(snapshot, "invalid mode");

		Assert.Equal(SummonUpdatePacketPlanStatus.BlockedInvalidSnapshot, plan.Status);
		Assert.False(plan.ShouldSendToMaster);
		Assert.False(plan.ShouldBroadcastFromSummon);
		Assert.Null(plan.Packet);
	}

	[Fact]
	public void CreateSendToMasterPlan_BlocksNegativeStatsBeforePacketCreation()
	{
		var snapshot = CreateSnapshot() with { MagicCritical = new SummonUpdateStatSnapshot(Current: 12, Base: -1) };

		var plan = SummonUpdatePacketPlanService.CreateSendToMasterPlan(snapshot, "negative stat");

		Assert.Equal(SummonUpdatePacketPlanStatus.BlockedInvalidSnapshot, plan.Status);
		Assert.Null(plan.Packet);
	}

	private static SummonUpdateSnapshot CreateSnapshot()
	{
		return new SummonUpdateSnapshot(
			Level: 55,
			Mode: SummonUpdateModeId.Guard,
			CurrentHp: 7100,
			MaxHp: new SummonUpdateStatSnapshot(Current: 8200, Base: 7600),
			MainHandPhysicalAttack: new SummonUpdateStatSnapshot(Current: 415, Base: 390),
			PhysicalDefense: new SummonUpdateStatSnapshot(Current: 771, Base: 720),
			MagicResist: new SummonUpdateStatSnapshot(Current: 490, Base: 440),
			MagicDefense: new SummonUpdateStatSnapshot(Current: 615, Base: 580),
			MainHandPhysicalAccuracy: new SummonUpdateStatSnapshot(Current: 1320, Base: 1275),
			MainHandPhysicalCritical: new SummonUpdateStatSnapshot(Current: 250, Base: 230),
			MagicBoost: new SummonUpdateStatSnapshot(Current: 950, Base: 900),
			MagicBoostResist: new SummonUpdateStatSnapshot(Current: 85, Base: 70),
			MagicAccuracy: new SummonUpdateStatSnapshot(Current: 1100, Base: 1050),
			MagicCritical: new SummonUpdateStatSnapshot(Current: 120, Base: 100),
			Parry: new SummonUpdateStatSnapshot(Current: 330, Base: 300),
			Evasion: new SummonUpdateStatSnapshot(Current: 405, Base: 375));
	}

	private static void AssertSmSummonUpdatePayload(SmSummonUpdate packet, SummonUpdateSnapshot snapshot)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmSummonUpdate.PacketOpCode, packet.OpCode);
		Assert.Equal(snapshot.Level, reader.ReadC());
		Assert.Equal((int)snapshot.Mode, reader.ReadH());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(snapshot.CurrentHp, reader.ReadD());
		Assert.Equal(snapshot.MaxHp.Current, reader.ReadD());
		Assert.Equal(snapshot.MainHandPhysicalAttack.Current, reader.ReadD());
		Assert.Equal(snapshot.PhysicalDefense.Current, reader.ReadD());
		Assert.Equal(snapshot.MagicResist.Current, reader.ReadH());
		Assert.Equal(snapshot.MagicDefense.Current, reader.ReadD());
		Assert.Equal(snapshot.MainHandPhysicalAccuracy.Current, reader.ReadH());
		Assert.Equal(snapshot.MainHandPhysicalCritical.Current, reader.ReadH());
		Assert.Equal(snapshot.MagicBoost.Current, reader.ReadH());
		Assert.Equal(snapshot.MagicBoostResist.Current, reader.ReadH());
		Assert.Equal(snapshot.MagicAccuracy.Current, reader.ReadH());
		Assert.Equal(snapshot.MagicCritical.Current, reader.ReadH());
		Assert.Equal(snapshot.Parry.Current, reader.ReadH());
		Assert.Equal(snapshot.Evasion.Current, reader.ReadH());
		Assert.Equal(snapshot.MaxHp.Base, reader.ReadD());
		Assert.Equal(snapshot.MainHandPhysicalAttack.Base, reader.ReadD());
		Assert.Equal(snapshot.PhysicalDefense.Base, reader.ReadD());
		Assert.Equal(snapshot.MagicResist.Base, reader.ReadH());
		Assert.Equal(snapshot.MagicDefense.Base, reader.ReadD());
		Assert.Equal(snapshot.MainHandPhysicalAccuracy.Base, reader.ReadH());
		Assert.Equal(snapshot.MainHandPhysicalCritical.Base, reader.ReadH());
		Assert.Equal(snapshot.MagicBoost.Base, reader.ReadH());
		Assert.Equal(snapshot.MagicBoostResist.Base, reader.ReadH());
		Assert.Equal(snapshot.MagicAccuracy.Base, reader.ReadH());
		Assert.Equal(snapshot.MagicCritical.Base, reader.ReadH());
		Assert.Equal(snapshot.Parry.Base, reader.ReadH());
		Assert.Equal(snapshot.Evasion.Base, reader.ReadH());
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
