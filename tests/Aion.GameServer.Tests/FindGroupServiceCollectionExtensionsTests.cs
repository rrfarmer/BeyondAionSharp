using System.Net;
using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.DependencyInjection;

namespace Aion.GameServer.Tests;

public sealed class FindGroupServiceCollectionExtensionsTests
{
	[Fact]
	public async Task AddFindGroupSingletonGraph_RegistersSharedSocketRuntimeServices()
	{
		await using var provider = CreateServices(includeSocketServer: true).BuildServiceProvider();

		var socketServer = provider.GetRequiredService<GameClientSocketServer>();

		Assert.Same(
			provider.GetRequiredService<PlayerGroupRuntime>(),
			GetPrivateField<PlayerGroupRuntime>(socketServer, "_playerGroupRuntime"));
		Assert.Same(
			provider.GetRequiredService<PlayerAllianceRuntime>(),
			GetPrivateField<PlayerAllianceRuntime>(socketServer, "_playerAllianceRuntime"));
		Assert.Same(
			provider.GetRequiredService<PlayerGroupInviteRequestService>(),
			GetPrivateField<PlayerGroupInviteRequestService>(socketServer, "_playerGroupInviteRequestService"));
		Assert.Same(
			provider.GetRequiredService<PlayerAllianceInviteRequestService>(),
			GetPrivateField<PlayerAllianceInviteRequestService>(socketServer, "_playerAllianceInviteRequestService"));
		Assert.Same(
			provider.GetRequiredService<FindGroupConnectionClientActionCompositionPlanService>(),
			GetPrivateField<FindGroupConnectionClientActionCompositionPlanService>(socketServer, "_findGroupConnectionClientActionCompositionPlanService"));
		Assert.Same(
			provider.GetRequiredService<FindGroupConnectionBoundaryDispatchAdapterService>(),
			GetPrivateField<FindGroupConnectionBoundaryDispatchAdapterService>(socketServer, "_findGroupConnectionBoundaryDispatchAdapterService"));
	}

	[Fact]
	public void AddFindGroupSingletonGraph_GroupInviteUsesSharedFindGroupService()
	{
		using var provider = CreateServices().BuildServiceProvider();
		var findGroupService = provider.GetRequiredService<FindGroupRecruitmentPlanService>();
		var groupRuntime = provider.GetRequiredService<PlayerGroupRuntime>();
		var inviteService = provider.GetRequiredService<PlayerGroupInviteRequestService>();
		var inviter = CreatePlayer(1001, "Inviter");
		var invited = CreatePlayer(1002, "Invited");
		findGroupService.AddRecruitment(inviter, "Need one", groupType: 3, nowEpochSeconds: 100);
		inviteService.SendInvite(inviter, invited);

		var result = inviteService.HandleResponse(
			invited,
			SmQuestionWindow.PartyInvite,
			response: 1,
			groupRuntime,
			allocateGroupId: () => 77001,
			resolveInviter: objectId => objectId == inviter.ObjectId ? inviter : null);

		Assert.Equal(GroupInviteResponseStatus.Accepted, result.Status);
		var recruitment = Assert.Single(findGroupService.ShowRecruitments(inviter.Race, nowEpochSeconds: 200).Recruitments);
		Assert.Equal(77001, recruitment.ObjectId);
		Assert.False(recruitment.IsSoloPlayer);
		Assert.Equal("Need one", recruitment.Message);
	}

	[Fact]
	public void AddFindGroupSingletonGraph_GroupRuntimeDisbandUsesSharedFindGroupService()
	{
		using var provider = CreateServices().BuildServiceProvider();
		var findGroupService = provider.GetRequiredService<FindGroupRecruitmentPlanService>();
		var groupRuntime = provider.GetRequiredService<PlayerGroupRuntime>();
		var leader = CreatePlayer(1001, "Leader");
		var member = CreatePlayer(1002, "Member");
		groupRuntime.CreateOrUpdateGroup(77001, [leader, member]);
		findGroupService.AddRecruitment(
			leader,
			"Team recruitment",
			groupType: 4,
			nowEpochSeconds: 100,
			new FindGroupRecruitmentSubject(77001, "ELYOS", IsSoloPlayer: false, "Leader", Size: 2, MinLevel: 45, MaxLevel: 45, ClassId: 5));

		var plan = Assert.IsType<PlayerGroupLeavePlan>(groupRuntime.RemoveMemberWithLeavePlan(member));

		Assert.True(plan.WouldDisband);
		Assert.Equal(FindGroupRecruitmentPlanStatus.Removed, plan.FindGroupRecruitmentRemoval?.Status);
		Assert.Empty(findGroupService.ShowRecruitments("ELYOS", nowEpochSeconds: 200).Recruitments);
	}

	[Fact]
	public void AddFindGroupSingletonGraph_AllianceRuntimeDisbandUsesSharedFindGroupService()
	{
		using var provider = CreateServices().BuildServiceProvider();
		var findGroupService = provider.GetRequiredService<FindGroupRecruitmentPlanService>();
		var allianceRuntime = provider.GetRequiredService<PlayerAllianceRuntime>();
		var leader = CreatePlayer(1001, "Leader");
		var member = CreatePlayer(1002, "Member");
		allianceRuntime.CreateAlliance(88001, leader);
		allianceRuntime.AddMember(88001, member);
		findGroupService.AddRecruitment(
			leader,
			"Alliance recruitment",
			groupType: 8,
			nowEpochSeconds: 100,
			new FindGroupRecruitmentSubject(88001, "ELYOS", IsSoloPlayer: false, "Leader", Size: 2, MinLevel: 45, MaxLevel: 45, ClassId: 5));

		var plan = Assert.IsType<PlayerAllianceLeaveWorkflowPlan>(allianceRuntime.RemoveMemberWithLeaveWorkflow(member));

		Assert.True(plan.AllianceLeavePlan.WouldDisband);
		Assert.Equal(FindGroupRecruitmentPlanStatus.Removed, plan.FindGroupRecruitmentRemoval?.Status);
		Assert.Empty(findGroupService.ShowRecruitments("ELYOS", nowEpochSeconds: 200).Recruitments);
	}

	private static IServiceCollection CreateServices(bool includeSocketServer = false)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(
			new GameServerOptions
			{
				Network = new GameServerNetworkOptions
				{
					ClientEndPoint = new IPEndPoint(IPAddress.Loopback, 0),
					GameServerId = 7,
					MaxOnlinePlayers = 100,
				},
			});
		services.AddSingleton<GamePacketProcessor<string>>(_ => new GamePacketProcessor<string>((_, _) => Task.CompletedTask));
		services.AddFindGroupSingletonGraph();
		if (includeSocketServer)
			services.AddSingleton<GameClientSocketServer>();
		return services;
	}

	private static Player CreatePlayer(int objectId, string name)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 45,
			IsOnline = true,
			Position = new WorldPosition(210010000, objectId, objectId + 1, objectId + 2, 0),
		};
	}

	private static T GetPrivateField<T>(object instance, string fieldName)
		where T : class
	{
		var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
		Assert.NotNull(field);
		return Assert.IsType<T>(field.GetValue(instance));
	}
}
