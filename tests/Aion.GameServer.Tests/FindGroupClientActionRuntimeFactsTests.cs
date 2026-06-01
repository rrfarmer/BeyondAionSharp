using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupClientActionRuntimeFactsTests
{
	[Fact]
	public void ComposeDisabledPlan_UsesRuntimeFactsForActionTenMaskList()
	{
		var service = new FindGroupRecruitmentPlanService();
		var planner = new FindGroupClientActionPlanService(service);
		var player = CreatePlayer(0x01020304, "Recruiter", "ELYOS", "CLERIC", 65);
		service.RegisterInstanceGroup(player, instanceMaskId: 0x11223344, message: "Entry", minMembers: 6, nowEpochSeconds: 100);
		var facts = new FindGroupClientActionRuntimeFacts(
			player,
			NowEpochSeconds: 200,
			FormInstanceGroupAnywhere: true,
			TargetNpcInstanceMaskIds: [300110000],
			AllRecruitableInstanceMaskIds: [300110000, 300150000]);

		var plan = facts.ComposeDisabledPlan(planner, new FindGroupClientAction(10));

		Assert.Equal(FindGroupClientActionPlanKind.ShowInstanceGroups, plan.Kind);
		Assert.False(plan.DispatchLiveSideEffects);
		Assert.NotNull(plan.InstanceGroupClientShowPlan);
		Assert.Equal([300110000], plan.InstanceGroupClientShowPlan!.EnabledInstanceMaskIds);
		Assert.Single(plan.InstanceGroupShowPlan!.InstanceGroups);
	}

	[Fact]
	public void ComposeDisabledPlan_UsesResolverFactForInstanceApplicationResult()
	{
		var service = new FindGroupRecruitmentPlanService();
		var planner = new FindGroupClientActionPlanService(service);
		var responder = CreatePlayer(0x01020307, "Responder", "ELYOS", "GLADIATOR", 65);
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS", "RANGER", 65);
		service.RegisterInstanceGroup(responder, instanceMaskId: 0x11223344, message: "Entry", minMembers: 6, nowEpochSeconds: 100);
		var facts = new FindGroupClientActionRuntimeFacts(
			responder,
			NowEpochSeconds: 200,
			ResolvePlayer: objectId => objectId == applicant.ObjectId ? applicant : null);

		var plan = facts.ComposeDisabledPlan(
			planner,
			new FindGroupClientAction(12, PlayerOrTeamId: applicant.ObjectId, InstanceApplicationReply: 1));

		Assert.Equal(FindGroupClientActionPlanKind.SendInstanceApplicationResult, plan.Kind);
		Assert.NotNull(plan.InstanceApplicationPlan);
		Assert.Equal(FindGroupInstanceApplicationPlanStatus.AcceptedGroupInvite, plan.InstanceApplicationPlan!.Status);
		Assert.NotNull(plan.InstanceApplicationPlan.InviteIntent);
		Assert.Equal(applicant.ObjectId, plan.InstanceApplicationPlan.InviteIntent!.InvitedObjectId);
	}

	[Fact]
	public void ComposeDisabledPlan_CanUseParsedCmFindGroupPacket()
	{
		var service = new FindGroupRecruitmentPlanService();
		var planner = new FindGroupClientActionPlanService(service);
		var player = CreatePlayer(0x01020304, "Recruiter", "ELYOS", "RANGER", 65);
		var packet = Assert.IsType<CmFindGroup>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(77, buffer =>
				{
					buffer.WriteC(2);
					buffer.WriteD(player.ObjectId);
					buffer.WriteS("Need healer");
					buffer.WriteC(3);
				}),
				GameConnectionState.InGame));
		var facts = new FindGroupClientActionRuntimeFacts(player, NowEpochSeconds: 200);

		var plan = facts.ComposeDisabledPlan(planner, packet);

		Assert.Equal(FindGroupClientActionPlanKind.AddRecruitment, plan.Kind);
		Assert.Equal(FindGroupRecruitmentPlanStatus.Added, plan.RecruitmentMutationPlan!.Status);
		Assert.Equal("Need healer", plan.RecruitmentMutationPlan.CurrentRecruitment!.Message);
	}

	private static Player CreatePlayer(int objectId, string name, string race, string playerClass, int level)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			Race = race,
			PlayerClass = playerClass,
			Level = level,
		};
	}

	private static byte[] CreateClientPayload(int opcode, Action<PacketBuffer> writePayload)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		writePayload(buffer);
		return buffer.ToArray();
	}
}
