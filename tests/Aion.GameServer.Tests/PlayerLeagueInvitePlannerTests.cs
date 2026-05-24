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

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
