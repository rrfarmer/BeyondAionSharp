using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerLeagueInvitePlannerTests
{
	[Fact]
	public void CreateRequestSetupPlan_TargetsLeaderAndSendsInviteMessagesLikeJavaService()
	{
		var planner = new PlayerLeagueInvitePlanner();
		var alliances = new PlayerAllianceRuntime();
		var inviter = new Player { ObjectId = 1001, Name = "Inviter", IsOnline = true };
		var selected = new Player { ObjectId = 2002, Name = "Selected", IsOnline = true };
		var invitedLeader = new Player { ObjectId = 2001, Name = "InvitedLeader", IsOnline = true };
		alliances.CreateAlliance(88001, inviter);
		alliances.CreateAlliance(88002, invitedLeader);
		alliances.AddMember(88002, selected);

		var plan = planner.CreateRequestSetupPlan(inviter, selected, alliances);

		Assert.Equal(1001, plan.InviterObjectId);
		Assert.Equal(2002, plan.SelectedPlayerObjectId);
		Assert.Equal(2001, plan.RequestTargetObjectId);
		Assert.Equal("InvitedLeader", plan.RequestTargetName);
		Assert.Equal(88002, plan.InvitedAllianceId);
		Assert.Equal(2, plan.InvitedAllianceSize);
		Assert.Equal(SmQuestionWindow.UnionInviteMe, plan.QuestionCode);
		Assert.Collection(
			plan.RequesterSystemMessages,
			intent =>
			{
				Assert.Equal(1001, intent.RecipientObjectId);
				AssertSystemMessagePayload(intent.Message, 1400559, "Selected", "InvitedLeader");
			},
			intent =>
			{
				Assert.Equal(1001, intent.RecipientObjectId);
				AssertSystemMessagePayload(intent.Message, 1400558, "InvitedLeader", "2");
			});
		Assert.Equal(2001, plan.QuestionWindowIntent.RecipientObjectId);
		AssertQuestionWindowPayload(
			plan.QuestionWindowIntent.QuestionWindow,
			SmQuestionWindow.UnionInviteMe,
			expectedSenderObjectId: 0,
			expectedRangeOrCooldownSeconds: 0,
			"Inviter");
	}

	[Fact]
	public void CreateRequestSetupPlan_SkipsLeaderRedirectionMessageWhenSelectedPlayerIsLeader()
	{
		var planner = new PlayerLeagueInvitePlanner();
		var alliances = new PlayerAllianceRuntime();
		var inviter = new Player { ObjectId = 1001, Name = "Inviter", IsOnline = true };
		var invitedLeader = new Player { ObjectId = 2001, Name = "InvitedLeader", IsOnline = true };
		alliances.CreateAlliance(88001, inviter);
		alliances.CreateAlliance(88002, invitedLeader);

		var plan = planner.CreateRequestSetupPlan(inviter, invitedLeader, alliances);

		var message = Assert.Single(plan.RequesterSystemMessages);
		AssertSystemMessagePayload(message.Message, 1400558, "InvitedLeader", "1");
		Assert.Equal(2001, plan.QuestionWindowIntent.RecipientObjectId);
		AssertQuestionWindowPayload(plan.QuestionWindowIntent.QuestionWindow, SmQuestionWindow.UnionInviteMe, 0, 0, "Inviter");
	}

	[Fact]
	public void TryPutPendingRequest_RegistersOnceLikeJavaResponseRequesterPutRequest()
	{
		var planner = new PlayerLeagueInvitePlanner();
		var alliances = new PlayerAllianceRuntime();
		var inviter = new Player { ObjectId = 1001, Name = "Inviter", IsOnline = true };
		var invitedLeader = new Player { ObjectId = 2001, Name = "InvitedLeader", IsOnline = true };
		alliances.CreateAlliance(88001, inviter);
		alliances.CreateAlliance(88002, invitedLeader);
		var setupPlan = planner.CreateRequestSetupPlan(inviter, invitedLeader, alliances);

		var registered = planner.TryPutPendingRequest(invitedLeader, setupPlan);
		var duplicate = planner.TryPutPendingRequest(invitedLeader, setupPlan);

		Assert.True(registered.Registered);
		Assert.Equal(2001, registered.RequestTargetObjectId);
		Assert.Equal(SmQuestionWindow.UnionInviteMe, registered.QuestionCode);
		Assert.Same(registered.PendingRequest, invitedLeader.PendingLeagueInviteRequest);
		Assert.Equal(SmQuestionWindow.UnionInviteMe, registered.PendingRequest.QuestionId);
		Assert.Equal(1001, registered.PendingRequest.RequesterObjectId);
		Assert.Equal(2001, registered.PendingRequest.RequestTargetObjectId);
		Assert.Equal(2001, registered.PendingRequest.SelectedPlayerObjectId);
		Assert.Equal(88002, registered.PendingRequest.InvitedAllianceId);
		Assert.False(duplicate.Registered);
		Assert.Same(registered.PendingRequest, duplicate.PendingRequest);
		Assert.Same(registered.PendingRequest, invitedLeader.PendingLeagueInviteRequest);
	}

	[Fact]
	public void CreatePendingRequestResponsePlan_DenyClearsRequestAndSendsRejectLikeJavaHandle()
	{
		var planner = new PlayerLeagueInvitePlanner();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var inviter = new Player { ObjectId = 1001, Name = "Inviter", IsOnline = true };
		var invitedLeader = new Player { ObjectId = 2001, Name = "InvitedLeader", IsOnline = true };
		alliances.CreateAlliance(88001, inviter);
		alliances.CreateAlliance(88002, invitedLeader);
		var setupPlan = planner.CreateRequestSetupPlan(inviter, invitedLeader, alliances);
		var pendingPlan = planner.TryPutPendingRequest(invitedLeader, setupPlan);

		var responsePlan = planner.CreatePendingRequestResponsePlan(
			inviter,
			invitedLeader,
			SmQuestionWindow.UnionInviteMe,
			responseCode: 0,
			leagues,
			alliances);

		Assert.Equal(PlayerLeagueInviteResponseStatus.Denied, responsePlan.Status);
		Assert.Equal(SmQuestionWindow.UnionInviteMe, responsePlan.QuestionId);
		Assert.Equal(0, responsePlan.ResponseCode);
		Assert.Same(pendingPlan.PendingRequest, responsePlan.PendingRequest);
		Assert.Null(invitedLeader.PendingLeagueInviteRequest);
		var denyPlan = Assert.IsType<PlayerLeagueInviteDenyPlan>(responsePlan.DenyPlan);
		AssertSystemMessagePayload(denyPlan.SystemMessageIntent.Message, 1300190, "InvitedLeader");
		Assert.Null(responsePlan.AcceptPlan);
		Assert.Null(responsePlan.CanInvitePlan);
	}

	[Fact]
	public void CreatePendingRequestResponsePlan_AcceptJoinsExistingLeagueAndClearsRequestLikeJavaHandle()
	{
		var planner = new PlayerLeagueInvitePlanner();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var inviter = new Player { ObjectId = 1001, Name = "Inviter", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var invitedLeader = new Player { ObjectId = 2001, Name = "InvitedLeader", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		alliances.CreateAlliance(88001, inviter);
		alliances.CreateAlliance(88002, invitedLeader);
		leagues.CreateLeague(77001, leaderAllianceId: 88001);
		var setupPlan = planner.CreateRequestSetupPlan(inviter, invitedLeader, alliances);
		var pendingPlan = planner.TryPutPendingRequest(invitedLeader, setupPlan);

		var responsePlan = planner.CreatePendingRequestResponsePlan(
			inviter,
			invitedLeader,
			SmQuestionWindow.UnionInviteMe,
			responseCode: 1,
			leagues,
			alliances);

		Assert.Equal(PlayerLeagueInviteResponseStatus.AcceptedJoined, responsePlan.Status);
		Assert.Equal(1, responsePlan.ResponseCode);
		Assert.Same(pendingPlan.PendingRequest, responsePlan.PendingRequest);
		Assert.Null(invitedLeader.PendingLeagueInviteRequest);
		Assert.Equal(PlayerLeagueCanInviteStatus.PassedRepresentedChecks, responsePlan.CanInvitePlan?.Status);
		var acceptPlan = Assert.IsType<PlayerLeagueInviteAcceptPlan>(responsePlan.AcceptPlan);
		Assert.Equal(PlayerLeagueInviteAcceptStatus.Joined, acceptPlan.Status);
		Assert.Equal([88001, 88002], leagues.GetAllianceIdsByPosition(77001));
		Assert.Null(responsePlan.DenyPlan);
	}

	[Fact]
	public void CreatePendingRequestResponsePlan_WrongQuestionLeavesPendingRequestLikeJavaRespondMiss()
	{
		var planner = new PlayerLeagueInvitePlanner();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var inviter = new Player { ObjectId = 1001, Name = "Inviter", IsOnline = true };
		var invitedLeader = new Player { ObjectId = 2001, Name = "InvitedLeader", IsOnline = true };
		alliances.CreateAlliance(88001, inviter);
		alliances.CreateAlliance(88002, invitedLeader);
		var setupPlan = planner.CreateRequestSetupPlan(inviter, invitedLeader, alliances);
		var pendingPlan = planner.TryPutPendingRequest(invitedLeader, setupPlan);

		var responsePlan = planner.CreatePendingRequestResponsePlan(
			inviter,
			invitedLeader,
			questionId: 12345,
			responseCode: 1,
			leagueRuntime: leagues,
			allianceRuntime: alliances);

		Assert.Equal(PlayerLeagueInviteResponseStatus.NoPendingRequest, responsePlan.Status);
		Assert.Same(pendingPlan.PendingRequest, responsePlan.PendingRequest);
		Assert.Same(pendingPlan.PendingRequest, invitedLeader.PendingLeagueInviteRequest);
		Assert.Null(responsePlan.AcceptPlan);
		Assert.Null(responsePlan.DenyPlan);
		Assert.Null(responsePlan.CanInvitePlan);
	}

	[Fact]
	public void CreateCanInviteFirstChecksPlan_FollowsJavaFailureOrder()
	{
		var planner = new PlayerLeagueInvitePlanner();
		var inviter = new Player { ObjectId = 1001, Name = "Inviter", IsOnline = true };
		var invited = new Player { ObjectId = 2001, Name = "Invited", IsOnline = false };
		inviter.ReplaceCreatureState(PlayerCreatureState.Dead);

		var deadPlan = planner.CreateCanInviteFirstChecksPlan(inviter, invited);

		AssertCanInviteFailure(deadPlan, PlayerLeagueCanInviteStatus.InviterDead, 1001, 1400570);

		inviter.ReplaceCreatureState(PlayerCreatureState.None);
		var offlinePlan = planner.CreateCanInviteFirstChecksPlan(inviter, invited);

		AssertCanInviteFailure(offlinePlan, PlayerLeagueCanInviteStatus.InvitedOffline, 1001, 1400569);

		invited.IsOnline = true;
		var noAlliancePlan = planner.CreateCanInviteFirstChecksPlan(inviter, invited);

		AssertCanInviteFailure(noAlliancePlan, PlayerLeagueCanInviteStatus.InvitedWithoutAlliance, 1001, 1400567, "Invited");
	}

	[Fact]
	public void CreateCanInviteFirstChecksPlan_PassesWhenRepresentedChecksPass()
	{
		var planner = new PlayerLeagueInvitePlanner();
		var alliances = new PlayerAllianceRuntime();
		var inviter = new Player { ObjectId = 1001, Name = "Inviter", IsOnline = true };
		var invited = new Player { ObjectId = 2001, Name = "Invited", IsOnline = true };
		alliances.CreateAlliance(88001, inviter);
		alliances.CreateAlliance(88002, invited);

		var plan = planner.CreateCanInviteFirstChecksPlan(inviter, invited);

		Assert.Equal(PlayerLeagueCanInviteStatus.PassedRepresentedChecks, plan.Status);
		Assert.Null(plan.SystemMessageIntent);
	}

	[Fact]
	public void CreateCanInviteAllianceChecksPlan_FollowsJavaMiddleFailureOrder()
	{
		var planner = new PlayerLeagueInvitePlanner();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var inviter = new Player { ObjectId = 1001, Name = "Inviter", IsOnline = true };
		var sameAllianceInvitee = new Player { ObjectId = 1002, Name = "SameAlliance", IsOnline = true };
		alliances.CreateAlliance(88001, inviter);
		alliances.AddMember(88001, sameAllianceInvitee);
		leagues.CreateLeague(77001, leaderAllianceId: 88001);

		var ownAlliancePlan = planner.CreateCanInviteAllianceChecksPlan(inviter, sameAllianceInvitee, leagues);

		AssertCanInviteFailure(ownAlliancePlan, PlayerLeagueCanInviteStatus.InvitedInOwnAlliance, 1001, 1400568);

		var invitedInLeague = new Player { ObjectId = 2001, Name = "AlreadyLeague", IsOnline = true };
		alliances.CreateAlliance(88002, invitedInLeague);
		Assert.NotNull(leagues.JoinAlliance(77001, 88002, alliances));

		var alreadyLeaguePlan = planner.CreateCanInviteAllianceChecksPlan(inviter, invitedInLeague, leagues);

		AssertCanInviteFailure(alreadyLeaguePlan, PlayerLeagueCanInviteStatus.InvitedAlreadyInLeague, 1001, 1400603);
	}

	[Fact]
	public void CreateCanInviteAllianceChecksPlan_ReportsFullLeagueAndPassesRepresentedChecks()
	{
		var planner = new PlayerLeagueInvitePlanner();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var inviter = new Player { ObjectId = 1001, Name = "Inviter", IsOnline = true };
		alliances.CreateAlliance(88001, inviter);
		leagues.CreateLeague(77001, leaderAllianceId: 88001);
		for (var index = 2; index <= 8; index++)
		{
			var leader = new Player { ObjectId = index * 1000 + 1, Name = $"Leader{index}", IsOnline = true };
			var allianceId = 88000 + index;
			alliances.CreateAlliance(allianceId, leader);
			leagues.AddAlliance(77001, allianceId);
		}

		var invited = new Player { ObjectId = 9001, Name = "Invited", IsOnline = true };
		alliances.CreateAlliance(88009, invited);

		var fullPlan = planner.CreateCanInviteAllianceChecksPlan(inviter, invited, leagues);

		AssertCanInviteFailure(fullPlan, PlayerLeagueCanInviteStatus.InviterLeagueFull, 1001, 1400565);

		var openLeagues = new PlayerLeagueRuntime();
		openLeagues.CreateLeague(77002, leaderAllianceId: 88001);
		var passPlan = planner.CreateCanInviteAllianceChecksPlan(inviter, invited, openLeagues);

		Assert.Equal(PlayerLeagueCanInviteStatus.PassedRepresentedChecks, passPlan.Status);
		Assert.Null(passPlan.SystemMessageIntent);
	}

	[Fact]
	public void CreateCanInviteAllianceChecksPlan_SameLeagueHitsAlreadyInLeagueBeforeOtherUnionLikeJavaOrder()
	{
		var planner = new PlayerLeagueInvitePlanner();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var inviter = new Player { ObjectId = 1001, Name = "Inviter", IsOnline = true };
		var invited = new Player { ObjectId = 2001, Name = "Invited", IsOnline = true };
		alliances.CreateAlliance(88001, inviter);
		alliances.CreateAlliance(88002, invited);
		leagues.CreateLeague(77001, leaderAllianceId: 88001);
		Assert.NotNull(leagues.JoinAlliance(77001, 88002, alliances));

		var plan = planner.CreateCanInviteAllianceChecksPlan(inviter, invited, leagues);

		AssertCanInviteFailure(plan, PlayerLeagueCanInviteStatus.InvitedAlreadyInLeague, 1001, 1400603);
	}

	[Fact]
	public void CreateAcceptExistingLeaguePlan_JoinsInvitedAllianceLikeJavaAcceptRequest()
	{
		var planner = new PlayerLeagueInvitePlanner();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var leagueLeader = new Player { ObjectId = 1001, Name = "LeagueLeader", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var invitedLeader = new Player { ObjectId = 2001, Name = "InvitedLeader", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		alliances.CreateAlliance(88001, leagueLeader);
		alliances.CreateAlliance(88002, invitedLeader);
		leagues.CreateLeague(77001, leaderAllianceId: 88001);

		var plan = planner.CreateAcceptExistingLeaguePlan(
			requesterAllianceId: 88001,
			invitedAllianceId: 88002,
			leagues,
			alliances);

		Assert.Equal(PlayerLeagueInviteAcceptStatus.Joined, plan.Status);
		Assert.Equal(88001, plan.RequesterAllianceId);
		Assert.Equal(88002, plan.InvitedAllianceId);
		var joinPlan = Assert.IsType<PlayerLeagueJoinPlan>(plan.JoinPlan);
		Assert.Equal(77001, joinPlan.LeagueId);
		Assert.Equal(1, joinPlan.JoinedPosition);
		Assert.Equal([88001, 88002], joinPlan.AllianceIdsByPosition);
		Assert.Equal([88001, 88002], leagues.GetAllianceIdsByPosition(77001));
		Assert.Collection(
			joinPlan.PacketIntents,
			intent =>
			{
				Assert.Equal(1001, intent.RecipientObjectId);
				Assert.Equal(1400561, intent.AllianceInfoPlan?.MessageId);
				Assert.Equal("InvitedLeader", intent.AllianceInfoPlan?.Message);
			},
			intent =>
			{
				Assert.Equal(2001, intent.RecipientObjectId);
				Assert.Equal(1400560, intent.AllianceInfoPlan?.MessageId);
				Assert.Equal("LeagueLeader", intent.AllianceInfoPlan?.Message);
			});
	}

	[Fact]
	public void CreateAcceptExistingLeaguePlan_ReportsDeferredOrNoopBranches()
	{
		var planner = new PlayerLeagueInvitePlanner();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var requester = new Player { ObjectId = 1001, Name = "Requester", IsOnline = true };
		var invited = new Player { ObjectId = 2001, Name = "Invited", IsOnline = true };
		alliances.CreateAlliance(88001, requester);
		alliances.CreateAlliance(88002, invited);

		var missingLeaguePlan = planner.CreateAcceptExistingLeaguePlan(88001, 88002, leagues, alliances);

		Assert.Equal(PlayerLeagueInviteAcceptStatus.RequesterLeagueMissing, missingLeaguePlan.Status);
		Assert.Null(missingLeaguePlan.JoinPlan);

		leagues.CreateLeague(77001, leaderAllianceId: 88001);
		Assert.NotNull(leagues.JoinAlliance(77001, 88002, alliances));

		var alreadyInLeaguePlan = planner.CreateAcceptExistingLeaguePlan(88001, 88002, leagues, alliances);

		Assert.Equal(PlayerLeagueInviteAcceptStatus.InvitedAlreadyInLeague, alreadyInLeaguePlan.Status);
		Assert.Null(alreadyInLeaguePlan.JoinPlan);
		Assert.Equal([88001, 88002], leagues.GetAllianceIdsByPosition(77001));
	}

	[Fact]
	public void CreateDenyPlan_SendsRequesterRejectMessageLikeJavaLeagueInviteEvent()
	{
		var planner = new PlayerLeagueInvitePlanner();

		var plan = planner.CreateDenyPlan(requesterObjectId: 1001, responderName: "Responder");

		Assert.Equal(1001, plan.RequesterObjectId);
		Assert.Equal("Responder", plan.ResponderName);
		Assert.Equal(1001, plan.SystemMessageIntent.RecipientObjectId);
		Assert.Equal(1300190, plan.SystemMessageIntent.Message.MessageId);
		AssertSystemMessagePayload(plan.SystemMessageIntent.Message, 1300190, "Responder");
	}

	[Fact]
	public void CreateDenyPlan_RejectsInvalidRequesterOrResponder()
	{
		var planner = new PlayerLeagueInvitePlanner();

		Assert.Throws<ArgumentOutOfRangeException>(() => planner.CreateDenyPlan(0, "Responder"));
		Assert.Throws<ArgumentException>(() => planner.CreateDenyPlan(1001, ""));
	}

	private static void AssertCanInviteFailure(
		PlayerLeagueCanInvitePlan plan,
		PlayerLeagueCanInviteStatus expectedStatus,
		int expectedRecipientObjectId,
		int expectedMessageId,
		params string[] expectedParameters)
	{
		Assert.Equal(expectedStatus, plan.Status);
		var intent = Assert.IsType<PlayerAllianceSystemMessageIntent>(plan.SystemMessageIntent);
		Assert.Equal(expectedRecipientObjectId, intent.RecipientObjectId);
		Assert.Equal(expectedMessageId, intent.Message.MessageId);
		AssertSystemMessagePayload(intent.Message, expectedMessageId, expectedParameters);
	}

	private static void AssertSystemMessagePayload(
		SmSystemMessage packet,
		int expectedMessageId,
		params string[] expectedParameters)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(25, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(expectedMessageId, reader.ReadD());
		Assert.Equal(expectedParameters.Length, (int)reader.ReadC());
		foreach (var expectedParameter in expectedParameters)
			Assert.Equal(expectedParameter, reader.ReadS());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertQuestionWindowPayload(
		SmQuestionWindow packet,
		int expectedCode,
		int expectedSenderObjectId,
		int expectedRangeOrCooldownSeconds,
		params string?[] expectedParameters)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedCode, reader.ReadD());
		for (var index = 0; index < 3; index++)
			Assert.Equal(index < expectedParameters.Length ? expectedParameters[index] : string.Empty, reader.ReadS());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(expectedRangeOrCooldownSeconds > 0 ? 1 : 0, (int)reader.ReadC());
		Assert.Equal(expectedSenderObjectId, reader.ReadD());
		Assert.Equal(expectedRangeOrCooldownSeconds, reader.ReadD());
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
