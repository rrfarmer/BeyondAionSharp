using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class FindGroupConnectionClientActionCompositionPlanServiceTests
{
	[Fact]
	public async Task CreateDisabledPlan_ExtractsConnectionActivePlayerForParsedPacket()
	{
		await using var fixture = await ConnectionFixture.CreateAsync();
		var player = CreatePlayer(0x01020304, "Recruiter", "ELYOS", "CLERIC", 65);
		SetActivePlayer(fixture.Connection, player);
		var service = CreateService(fixture.World);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(2);
				buffer.WriteD(player.ObjectId);
				buffer.WriteS("Need healer");
				buffer.WriteC(3);
			});

		var plan = service.CreateDisabledPlan(fixture.Connection, packet, nowEpochSeconds: 200);

		Assert.Equal(FindGroupConnectionClientActionCompositionStatus.ComposedDisabledPlan, plan.Status);
		Assert.Same(player, plan.ActivePlayer);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.NotNull(plan.ClientActionPlan);
		Assert.Equal(FindGroupClientActionPlanKind.AddRecruitment, plan.ClientActionPlan!.Kind);
		Assert.False(plan.ClientActionPlan.DispatchLiveSideEffects);
		Assert.Equal("Need healer", plan.ClientActionPlan.RecruitmentMutationPlan!.CurrentRecruitment!.Message);
	}

	[Fact]
	public async Task CreateDisabledPlan_MissingActivePlayerRecordsDisabledSkip()
	{
		await using var fixture = await ConnectionFixture.CreateAsync();
		var service = CreateService(fixture.World);
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC(0));

		var plan = service.CreateDisabledPlan(fixture.Connection, packet, nowEpochSeconds: 200);

		Assert.Equal(FindGroupConnectionClientActionCompositionStatus.SkippedMissingActivePlayer, plan.Status);
		Assert.Null(plan.ActivePlayer);
		Assert.Equal(0, plan.Action.Action);
		Assert.Null(plan.ClientActionPlan);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
	}

	[Fact]
	public async Task CreateDisabledPlan_UsesWorldResolverForInstanceApplication()
	{
		await using var fixture = await ConnectionFixture.CreateAsync();
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS", "RANGER", 65);
		var recruiter = CreatePlayer(0x01020307, "Recruiter", "ELYOS", "GLADIATOR", 65);
		SetActivePlayer(fixture.Connection, applicant);
		Assert.True(fixture.World.TryAddObject(recruiter.ObjectId, recruiter));
		var service = CreateService(fixture.World);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(11);
				buffer.WriteD(recruiter.ObjectId);
				buffer.WriteD(0x11223344);
			});

		var plan = service.CreateDisabledPlan(fixture.Connection, packet, nowEpochSeconds: 200);

		Assert.Equal(FindGroupConnectionClientActionCompositionStatus.ComposedDisabledPlan, plan.Status);
		Assert.NotNull(plan.ClientActionPlan);
		Assert.Equal(FindGroupClientActionPlanKind.SendInstanceApplication, plan.ClientActionPlan!.Kind);
		Assert.Equal(FindGroupInstanceApplicationPlanStatus.ApplicationSent, plan.ClientActionPlan.InstanceApplicationPlan!.Status);
		var intent = Assert.Single(plan.ClientActionPlan.InstanceApplicationPlan.DirectPacketIntents);
		Assert.Equal(recruiter.ObjectId, intent.RecipientObjectId);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.ClientActionPlan.DispatchLiveSideEffects);
	}

	[Fact]
	public async Task CreateDisabledPlan_UsesWorldResolverForApplicationResult()
	{
		await using var fixture = await ConnectionFixture.CreateAsync();
		var responder = CreatePlayer(0x01020307, "Responder", "ELYOS", "GLADIATOR", 65);
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS", "RANGER", 65);
		SetActivePlayer(fixture.Connection, responder);
		Assert.True(fixture.World.TryAddObject(applicant.ObjectId, applicant));
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.RegisterInstanceGroup(
			responder,
			instanceMaskId: 0x11223344,
			message: "Entry",
			minMembers: 6,
			nowEpochSeconds: 100);
		var service = CreateService(fixture.World, findGroupService);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(12);
				buffer.WriteD(applicant.ObjectId);
				buffer.WriteC(1);
			});

		var plan = service.CreateDisabledPlan(fixture.Connection, packet, nowEpochSeconds: 200);

		Assert.Equal(FindGroupConnectionClientActionCompositionStatus.ComposedDisabledPlan, plan.Status);
		Assert.NotNull(plan.ClientActionPlan);
		Assert.Equal(FindGroupClientActionPlanKind.SendInstanceApplicationResult, plan.ClientActionPlan!.Kind);
		Assert.Equal(FindGroupInstanceApplicationPlanStatus.AcceptedGroupInvite, plan.ClientActionPlan.InstanceApplicationPlan!.Status);
		Assert.NotNull(plan.ClientActionPlan.InstanceApplicationPlan.InviteIntent);
		Assert.Equal(applicant.ObjectId, plan.ClientActionPlan.InstanceApplicationPlan.InviteIntent!.InvitedObjectId);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.ClientActionPlan.DispatchLiveSideEffects);
	}

	[Fact]
	public async Task CreateDisabledPlan_ExplicitResolverOverridesWorldResolver()
	{
		await using var fixture = await ConnectionFixture.CreateAsync();
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS", "RANGER", 65);
		var worldRecruiter = CreatePlayer(0x01020307, "WorldRecruiter", "ELYOS", "GLADIATOR", 65);
		SetActivePlayer(fixture.Connection, applicant);
		Assert.True(fixture.World.TryAddObject(worldRecruiter.ObjectId, worldRecruiter));
		var service = CreateService(fixture.World);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(11);
				buffer.WriteD(worldRecruiter.ObjectId);
				buffer.WriteD(0x11223344);
			});

		var plan = service.CreateDisabledPlan(
			fixture.Connection,
			packet,
			nowEpochSeconds: 200,
			resolvePlayer: _ => null);

		Assert.Equal(FindGroupConnectionClientActionCompositionStatus.ComposedDisabledPlan, plan.Status);
		Assert.Equal(FindGroupInstanceApplicationPlanStatus.MissingRecipient, plan.ClientActionPlan!.InstanceApplicationPlan!.Status);
		Assert.Empty(plan.ClientActionPlan.InstanceApplicationPlan.DirectPacketIntents);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.ClientActionPlan.DispatchLiveSideEffects);
	}

	[Fact]
	public async Task CreateDisabledPlan_UsesGroupRuntimeForRecruitmentSubject()
	{
		await using var fixture = await ConnectionFixture.CreateAsync();
		var leader = CreatePlayer(0x01020304, "Leader", "ELYOS", "CLERIC", 65);
		var member = CreatePlayer(0x01020305, "Member", "ELYOS", "RANGER", 61);
		SetActivePlayer(fixture.Connection, member);
		Assert.True(fixture.World.TryAddObject(leader.ObjectId, leader));
		Assert.True(fixture.World.TryAddObject(member.ObjectId, member));
		var groupRuntime = new PlayerGroupRuntime();
		groupRuntime.CreateOrUpdateGroup(7001, [leader, member]);
		var service = CreateService(fixture.World, groupRuntime: groupRuntime);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(2);
				buffer.WriteD(member.ObjectId);
				buffer.WriteS("Team listing");
				buffer.WriteC(3);
			});

		var plan = service.CreateDisabledPlan(fixture.Connection, packet, nowEpochSeconds: 200);

		Assert.Equal(FindGroupClientActionPlanKind.AddRecruitment, plan.ClientActionPlan!.Kind);
		var recruitment = plan.ClientActionPlan.RecruitmentMutationPlan!.CurrentRecruitment!;
		Assert.Equal(7001, recruitment.ObjectId);
		Assert.False(recruitment.IsSoloPlayer);
		Assert.Equal("Leader", recruitment.RecruiterName);
		Assert.Equal(2, recruitment.Size);
		Assert.Equal(61, recruitment.MinLevel);
		Assert.Equal(65, recruitment.MaxLevel);
		Assert.Equal(FindGroupRecruitmentSubject.ToJavaClassId("CLERIC"), recruitment.ClassId);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.ClientActionPlan.DispatchLiveSideEffects);
	}

	[Fact]
	public async Task CreateDisabledPlan_UsesGroupRuntimeMembersForInstanceGroupRegistration()
	{
		await using var fixture = await ConnectionFixture.CreateAsync();
		var leader = CreatePlayer(0x01020304, "Leader", "ELYOS", "CLERIC", 65);
		var member = CreatePlayer(0x01020305, "Member", "ELYOS", "RANGER", 61);
		leader.Position = new WorldPosition(210010000, 10, 20, 30, 0);
		member.Position = new WorldPosition(220010000, 11, 21, 31, 0);
		SetActivePlayer(fixture.Connection, leader);
		Assert.True(fixture.World.TryAddObject(leader.ObjectId, leader));
		Assert.True(fixture.World.TryAddObject(member.ObjectId, member));
		var groupRuntime = new PlayerGroupRuntime();
		groupRuntime.CreateOrUpdateGroup(7001, [leader, member]);
		var service = CreateService(fixture.World, groupRuntime: groupRuntime);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(8);
				buffer.WriteD(0x11223344);
				buffer.WriteC(0);
				buffer.WriteS("Entry");
				buffer.WriteC(2);
			});

		var plan = service.CreateDisabledPlan(fixture.Connection, packet, nowEpochSeconds: 200);

		Assert.Equal(FindGroupClientActionPlanKind.RegisterInstanceGroup, plan.ClientActionPlan!.Kind);
		var instanceGroup = plan.ClientActionPlan.InstanceGroupMutationPlan!.CurrentInstanceGroup!;
		Assert.Equal([leader.ObjectId, member.ObjectId], instanceGroup.Members.Select(memberState => memberState.PlayerObjectId));
		Assert.Equal([210010000, 220010000], instanceGroup.Members.Select(memberState => memberState.WorldId));
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.ClientActionPlan.DispatchLiveSideEffects);
	}

	[Fact]
	public async Task CreateDisabledPlan_ExplicitCurrentTeamOverridesRuntimeTeamFact()
	{
		await using var fixture = await ConnectionFixture.CreateAsync();
		var leader = CreatePlayer(0x01020304, "Leader", "ELYOS", "CLERIC", 65);
		var member = CreatePlayer(0x01020305, "Member", "ELYOS", "RANGER", 61);
		SetActivePlayer(fixture.Connection, member);
		Assert.True(fixture.World.TryAddObject(leader.ObjectId, leader));
		Assert.True(fixture.World.TryAddObject(member.ObjectId, member));
		var groupRuntime = new PlayerGroupRuntime();
		groupRuntime.CreateOrUpdateGroup(7001, [leader, member]);
		var service = CreateService(fixture.World, groupRuntime: groupRuntime);
		var overrideTeam = new FindGroupRecruitmentSubject(
			ObjectId: 9001,
			Race: "ELYOS",
			IsSoloPlayer: false,
			RecruiterName: "Override",
			Size: 3,
			MinLevel: 50,
			MaxLevel: 55,
			ClassId: 10);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(2);
				buffer.WriteD(member.ObjectId);
				buffer.WriteS("Override listing");
				buffer.WriteC(3);
			});

		var plan = service.CreateDisabledPlan(
			fixture.Connection,
			packet,
			nowEpochSeconds: 200,
			currentTeam: overrideTeam);

		var recruitment = plan.ClientActionPlan!.RecruitmentMutationPlan!.CurrentRecruitment!;
		Assert.Equal(9001, recruitment.ObjectId);
		Assert.Equal("Override", recruitment.RecruiterName);
		Assert.Equal(3, recruitment.Size);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.ClientActionPlan.DispatchLiveSideEffects);
	}

	[Fact]
	public async Task CreateDisabledPlan_UsesAllianceRuntimeForRecruitmentSubject()
	{
		await using var fixture = await ConnectionFixture.CreateAsync();
		var leader = CreatePlayer(0x01020304, "AllianceLeader", "ELYOS", "CLERIC", 65);
		var member = CreatePlayer(0x01020305, "AllianceMember", "ELYOS", "RANGER", 61);
		SetActivePlayer(fixture.Connection, member);
		Assert.True(fixture.World.TryAddObject(leader.ObjectId, leader));
		Assert.True(fixture.World.TryAddObject(member.ObjectId, member));
		var allianceRuntime = new PlayerAllianceRuntime();
		allianceRuntime.CreateAlliance(8001, leader);
		allianceRuntime.AddMember(8001, member);
		var service = CreateService(fixture.World, allianceRuntime: allianceRuntime);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(2);
				buffer.WriteD(member.ObjectId);
				buffer.WriteS("Alliance listing");
				buffer.WriteC(5);
			});

		var plan = service.CreateDisabledPlan(fixture.Connection, packet, nowEpochSeconds: 200);

		Assert.Equal(FindGroupClientActionPlanKind.AddRecruitment, plan.ClientActionPlan!.Kind);
		var recruitment = plan.ClientActionPlan.RecruitmentMutationPlan!.CurrentRecruitment!;
		Assert.Equal(8001, recruitment.ObjectId);
		Assert.False(recruitment.IsSoloPlayer);
		Assert.Equal("AllianceLeader", recruitment.RecruiterName);
		Assert.Equal(2, recruitment.Size);
		Assert.Equal(61, recruitment.MinLevel);
		Assert.Equal(65, recruitment.MaxLevel);
		Assert.Equal(FindGroupRecruitmentSubject.ToJavaClassId("CLERIC"), recruitment.ClassId);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.ClientActionPlan.DispatchLiveSideEffects);
	}

	[Fact]
	public async Task CreateDisabledPlan_UsesAllianceRuntimeMembersForInstanceGroupRegistration()
	{
		await using var fixture = await ConnectionFixture.CreateAsync();
		var leader = CreatePlayer(0x01020304, "AllianceLeader", "ELYOS", "CLERIC", 65);
		var member = CreatePlayer(0x01020305, "AllianceMember", "ELYOS", "RANGER", 61);
		leader.Position = new WorldPosition(210010000, 10, 20, 30, 0);
		member.Position = new WorldPosition(220010000, 11, 21, 31, 0);
		SetActivePlayer(fixture.Connection, leader);
		Assert.True(fixture.World.TryAddObject(leader.ObjectId, leader));
		Assert.True(fixture.World.TryAddObject(member.ObjectId, member));
		var allianceRuntime = new PlayerAllianceRuntime();
		allianceRuntime.CreateAlliance(8001, leader);
		allianceRuntime.AddMember(8001, member);
		var service = CreateService(fixture.World, allianceRuntime: allianceRuntime);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(8);
				buffer.WriteD(0x11223344);
				buffer.WriteC(0);
				buffer.WriteS("Alliance entry");
				buffer.WriteC(12);
			});

		var plan = service.CreateDisabledPlan(fixture.Connection, packet, nowEpochSeconds: 200);

		Assert.Equal(FindGroupClientActionPlanKind.RegisterInstanceGroup, plan.ClientActionPlan!.Kind);
		var instanceGroup = plan.ClientActionPlan.InstanceGroupMutationPlan!.CurrentInstanceGroup!;
		Assert.Equal([leader.ObjectId, member.ObjectId], instanceGroup.Members.Select(memberState => memberState.PlayerObjectId));
		Assert.Equal([210010000, 220010000], instanceGroup.Members.Select(memberState => memberState.WorldId));
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.ClientActionPlan.DispatchLiveSideEffects);
	}

	[Fact]
	public async Task CreateDisabledPlan_UsesAutoGroupTableForTargetNpcInstanceMasks()
	{
		await using var fixture = await ConnectionFixture.CreateAsync();
		var player = CreatePlayer(0x01020304, "Player", "ELYOS", "RANGER", 65);
		player.TargetObjectId = 0x02030405;
		SetActivePlayer(fixture.Connection, player);
		Assert.True(fixture.World.TryAddObject(player.ObjectId, player));
		Assert.True(fixture.World.TryAddObject(player.TargetObjectId, CreateNpc(player.TargetObjectId, templateId: 700001)));
		var autoGroups = new AutoGroupTable(
		[
			new AutoGroupSummary(302, 300110000, 0, 0, 0, 0, false, false, false, [700001]),
			new AutoGroupSummary(303, 300120000, 0, 0, 0, 0, false, false, false, [700002]),
			new AutoGroupSummary(401, 300600000, 0, 0, 0, 0, false, false, false, [700001]),
		]);
		var service = CreateService(fixture.World, autoGroups: autoGroups);
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC(10));

		var plan = service.CreateDisabledPlan(
			fixture.Connection,
			packet,
			nowEpochSeconds: 200,
			formInstanceGroupAnywhere: true);

		Assert.Equal(FindGroupClientActionPlanKind.ShowInstanceGroups, plan.ClientActionPlan!.Kind);
		var showPlan = plan.ClientActionPlan.InstanceGroupClientShowPlan!;
		Assert.Equal([302, 401], showPlan.EnabledInstanceMaskIds);
		Assert.NotNull(showPlan.EnableRegisterForInstancesIntent);
		Assert.False(showPlan.IsUpdate);
		Assert.True(showPlan.FormInstanceGroupAnywhere);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.ClientActionPlan.DispatchLiveSideEffects);
	}

	[Fact]
	public async Task CreateDisabledPlan_FallsBackToAllAutoGroupMasksWhenTargetNpcHasNoMasks()
	{
		await using var fixture = await ConnectionFixture.CreateAsync();
		var player = CreatePlayer(0x01020304, "Player", "ELYOS", "RANGER", 65);
		player.TargetObjectId = 0x02030405;
		SetActivePlayer(fixture.Connection, player);
		Assert.True(fixture.World.TryAddObject(player.TargetObjectId, CreateNpc(player.TargetObjectId, templateId: 799999)));
		var autoGroups = new AutoGroupTable(
		[
			new AutoGroupSummary(302, 300110000, 0, 0, 0, 0, false, false, false, [700001]),
			new AutoGroupSummary(303, 300120000, 0, 0, 0, 0, false, false, false, [700002]),
		]);
		var service = CreateService(fixture.World, autoGroups: autoGroups);
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC(10));

		var plan = service.CreateDisabledPlan(
			fixture.Connection,
			packet,
			nowEpochSeconds: 200,
			formInstanceGroupAnywhere: true);

		var showPlan = plan.ClientActionPlan!.InstanceGroupClientShowPlan!;
		Assert.Equal([302, 303], showPlan.EnabledInstanceMaskIds);
		Assert.NotNull(showPlan.EnableRegisterForInstancesIntent);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.ClientActionPlan.DispatchLiveSideEffects);
	}

	[Fact]
	public async Task CreateDisabledPlan_UsesGameServerOptionsForFormInstanceGroupAnywhere()
	{
		await using var fixture = await ConnectionFixture.CreateAsync();
		var player = CreatePlayer(0x01020304, "Player", "ELYOS", "RANGER", 65);
		SetActivePlayer(fixture.Connection, player);
		var autoGroups = new AutoGroupTable(
		[
			new AutoGroupSummary(302, 300110000, 0, 0, 0, 0, false, false, false, [700001]),
		]);
		var options = new GameServerOptions
		{
			Instance = new GameServerInstanceOptions { FormInstanceGroupAnywhere = true },
		};
		var service = CreateService(fixture.World, autoGroups: autoGroups, options: options);
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC(10));

		var plan = service.CreateDisabledPlan(fixture.Connection, packet, nowEpochSeconds: 200);

		var showPlan = plan.ClientActionPlan!.InstanceGroupClientShowPlan!;
		Assert.True(showPlan.FormInstanceGroupAnywhere);
		Assert.Equal([302], showPlan.EnabledInstanceMaskIds);
		Assert.NotNull(showPlan.EnableRegisterForInstancesIntent);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.ClientActionPlan.DispatchLiveSideEffects);
	}

	[Fact]
	public async Task CreateDisabledPlan_ExplicitFormInstanceGroupAnywhereOverridesOptions()
	{
		await using var fixture = await ConnectionFixture.CreateAsync();
		var player = CreatePlayer(0x01020304, "Player", "ELYOS", "RANGER", 65);
		SetActivePlayer(fixture.Connection, player);
		var autoGroups = new AutoGroupTable(
		[
			new AutoGroupSummary(302, 300110000, 0, 0, 0, 0, false, false, false, [700001]),
		]);
		var options = new GameServerOptions
		{
			Instance = new GameServerInstanceOptions { FormInstanceGroupAnywhere = true },
		};
		var service = CreateService(fixture.World, autoGroups: autoGroups, options: options);
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC(10));

		var plan = service.CreateDisabledPlan(
			fixture.Connection,
			packet,
			nowEpochSeconds: 200,
			formInstanceGroupAnywhere: false);

		var showPlan = plan.ClientActionPlan!.InstanceGroupClientShowPlan!;
		Assert.False(showPlan.FormInstanceGroupAnywhere);
		Assert.Null(showPlan.EnabledInstanceMaskIds);
		Assert.Null(showPlan.EnableRegisterForInstancesIntent);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.ClientActionPlan.DispatchLiveSideEffects);
	}

	private static FindGroupConnectionClientActionCompositionPlanService CreateService(
		GameWorld world,
		FindGroupRecruitmentPlanService? findGroupService = null,
		PlayerGroupRuntime? groupRuntime = null,
		PlayerAllianceRuntime? allianceRuntime = null,
		AutoGroupTable? autoGroups = null,
		GameServerOptions? options = null)
	{
		findGroupService ??= new FindGroupRecruitmentPlanService();
		return new FindGroupConnectionClientActionCompositionPlanService(
			new FindGroupClientActionPlanService(findGroupService),
			world,
			groupRuntime,
			allianceRuntime,
			autoGroups,
			options);
	}

	private static CmFindGroup CreateFindGroupPacket(Action<PacketBuffer> writePayload)
	{
		var packet = GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(77, writePayload),
			GameConnectionState.InGame);
		return Assert.IsType<CmFindGroup>(packet);
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

	private static WorldNpc CreateNpc(int objectId, int templateId)
	{
		var template = new NpcTemplateSummary(
			templateId,
			"Portal",
			NameId: 0,
			Level: 65,
			Rank: string.Empty,
			Rating: string.Empty,
			Race: string.Empty,
			Tribe: string.Empty,
			Type: string.Empty);
		return new WorldNpc(objectId, templateId, template, new WorldPosition(210010000, 10, 20, 30, 0));
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

	private static void SetActivePlayer(GameServerConnection connection, Player player)
	{
		var activePlayerField = typeof(GameServerConnection).GetField("_activePlayer", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(activePlayerField);
		activePlayerField.SetValue(connection, player);

		var stateField = typeof(GameServerConnection).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(stateField);
		stateField.SetValue(connection, GameConnectionState.InGame);
	}

	private sealed class ConnectionFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private ConnectionFixture(TcpClient client, GameServerConnection connection, GameWorld world)
		{
			_client = client;
			Connection = connection;
			World = world;
		}

		public GameServerConnection Connection { get; }

		public GameWorld World { get; }

		public static async Task<ConnectionFixture> CreateAsync()
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			try
			{
				var endpoint = (IPEndPoint)listener.LocalEndpoint;
				var client = new TcpClient();
				var acceptTask = listener.AcceptTcpClientAsync();
				await client.ConnectAsync(endpoint.Address, endpoint.Port);
				var serverClient = await acceptTask;
				var world = new GameWorld(NullLogger<GameWorld>.Instance);
				world.Initialize();
				return new ConnectionFixture(
					client,
					new GameServerConnection(
						NullLogger.Instance,
						serverClient,
						"find-group-composition-test",
						new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
						world: world),
					world);
			}
			finally
			{
				listener.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			await Connection.DisposeAsync();
			_client.Dispose();
		}
	}
}
