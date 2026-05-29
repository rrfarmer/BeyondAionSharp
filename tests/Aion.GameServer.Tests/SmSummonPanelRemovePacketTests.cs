using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SmSummonPanelRemovePacketTests
{
	[Fact]
	public void SmSummonPanelRemove_WritesSkillIdAndNonzeroFlagLikeJava()
	{
		var packet = new SmSummonPanelRemove(skillId: 12001);

		AssertSmSummonPanelRemovePayload(packet, skillId: 12001, flag: 1);
	}

	[Fact]
	public void SmSummonPanelRemove_WritesZeroFlagWhenSkillIdIsZeroLikeJava()
	{
		var packet = new SmSummonPanelRemove(skillId: 0);

		AssertSmSummonPanelRemovePayload(packet, skillId: 0, flag: 0);
	}

	[Fact]
	public void CreateSendToMasterPlan_CreatesPacketIntentForSummonsServiceRelease()
	{
		var plan = SummonPanelRemovePacketPlanService.CreateSendToMasterPlan(summonedBySkillId: 12001);

		Assert.Equal(SummonPanelRemovePacketPlanStatus.PacketCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldSendToMaster);
		Assert.NotNull(plan.Packet);
		Assert.Contains("SummonsService.ReleaseSummonTask.run", plan.JavaSource);
		AssertSmSummonPanelRemovePayload(plan.Packet!, skillId: 12001, flag: 1);
	}

	[Fact]
	public void CreateSendToMasterPlan_AllowsZeroSkillIdLikeJavaPacketBranch()
	{
		var plan = SummonPanelRemovePacketPlanService.CreateSendToMasterPlan(summonedBySkillId: 0);

		Assert.Equal(SummonPanelRemovePacketPlanStatus.PacketCreated, plan.Status);
		Assert.True(plan.ShouldSendToMaster);
		Assert.NotNull(plan.Packet);
		AssertSmSummonPanelRemovePayload(plan.Packet!, skillId: 0, flag: 0);
	}

	[Fact]
	public void CreateSendToMasterPlan_BlocksNegativeSkillIdBeforePacketCreation()
	{
		var plan = SummonPanelRemovePacketPlanService.CreateSendToMasterPlan(summonedBySkillId: -1);

		Assert.Equal(SummonPanelRemovePacketPlanStatus.BlockedNegativeSkillId, plan.Status);
		Assert.False(plan.ShouldSendToMaster);
		Assert.Null(plan.Packet);
	}

	private static void AssertSmSummonPanelRemovePayload(SmSummonPanelRemove packet, int skillId, int flag)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmSummonPanelRemove.PacketOpCode, packet.OpCode);
		Assert.Equal(skillId, reader.ReadH());
		Assert.Equal(flag, reader.ReadC());
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
