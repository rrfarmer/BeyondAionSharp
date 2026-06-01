using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class FindGroupSideEffectDispatchAuditServiceTests
{
	[Fact]
	public void CreateAuditPlan_RecordsDirectPacketIntentWithoutLiveDispatch()
	{
		var directIntent = new FindGroupDirectPacketIntent(
			RecipientObjectId: 1001,
			SmFindGroup.RegisterInstanceGroup(
			[
				new FindGroupInstanceGroupRegistrationSnapshot(
					GroupEntryId: 1001,
					InstanceMaskId: 0x11223344,
					MemberCount: 1,
					MinMembers: 3,
					RecruiterObjectId: 1001,
					MinLevel: 65,
					MaxLevel: 65,
					LastUpdate: 123,
					RecruiterName: "Recruiter",
					Message: "Entry"),
			]),
			"PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(14, List.of(instanceGroup)))");

		var plan = FindGroupSideEffectDispatchAuditService.CreateAuditPlan([directIntent]);

		Assert.False(plan.DispatchLiveSideEffects);
		var audit = Assert.Single(plan.DirectPackets);
		Assert.Equal(1001, audit.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), audit.PacketType);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(14, List.of(instanceGroup)))", audit.JavaSource);
		Assert.Empty(plan.WorldBroadcasts);
	}

	[Fact]
	public void CreateAuditPlan_RecordsWorldBroadcastRaceFilterWithoutLiveDispatch()
	{
		var player = CreatePlayer(1001, "Applicant");
		var service = new FindGroupRecruitmentPlanService();
		service.AddApplication(player, "Need group", groupType: 2, classId: 5, level: 65, nowEpochSeconds: 100);
		var removal = service.RemoveApplication(player);

		var plan = FindGroupSideEffectDispatchAuditService.CreateAuditPlan(
			worldBroadcastIntents: [removal.WorldBroadcastIntent]);

		Assert.False(plan.DispatchLiveSideEffects);
		Assert.Empty(plan.DirectPackets);
		var audit = Assert.Single(plan.WorldBroadcasts);
		Assert.Equal("ELYOS", audit.Race);
		Assert.Equal(nameof(SmFindGroup), audit.PacketType);
		Assert.Equal("p -> p.getRace() == recorded race", audit.JavaFilter);
		Assert.Equal("PacketSendUtility.broadcastToWorld(..., p -> p.getRace() == application.getPlayer().getRace())", audit.JavaSource);
	}

	[Fact]
	public void CreateAuditPlan_IgnoresNullBroadcastIntentAndStaysNonLive()
	{
		var plan = FindGroupSideEffectDispatchAuditService.CreateAuditPlan(
			directPacketIntents: [],
			worldBroadcastIntents: [null]);

		Assert.False(plan.DispatchLiveSideEffects);
		Assert.Empty(plan.DirectPackets);
		Assert.Empty(plan.WorldBroadcasts);
	}

	private static Player CreatePlayer(int objectId, string name)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 65,
			Position = new WorldPosition(210010000, 11, 22, 33, 0),
		};
	}
}
