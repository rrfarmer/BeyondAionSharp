using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class QuestSharePlanServiceTests
{
	private const int QuestId = 1234;

	[Fact]
	public void Plan_NullTemplate_SendsCannotShareToSharer()
	{
		var sharer = CreatePlayer(1, "Sharer");

		var plan = QuestSharePlanService.Plan(sharer, QuestId, template: null, InProgress(), [], _ => true);

		var instruction = Assert.Single(plan.Instructions);
		Assert.Equal(sharer.ObjectId, instruction.RecipientObjectId);
		AssertSystemMessage(instruction.Packet, QuestSharePlanService.MsgCannotShare);
	}

	[Fact]
	public void Plan_CannotShareTemplate_SendsCannotShareToSharer()
	{
		var sharer = CreatePlayer(1, "Sharer");
		var member = CreatePlayer(2, "Member");

		var plan = QuestSharePlanService.Plan(sharer, QuestId, Template(cannotShare: true), InProgress(), [member], _ => true);

		var instruction = Assert.Single(plan.Instructions);
		Assert.Equal(sharer.ObjectId, instruction.RecipientObjectId);
		AssertSystemMessage(instruction.Packet, QuestSharePlanService.MsgCannotShare);
	}

	[Fact]
	public void Plan_NullQuestState_ReturnsEmptyPlan()
	{
		var sharer = CreatePlayer(1, "Sharer");
		var member = CreatePlayer(2, "Member");

		var plan = QuestSharePlanService.Plan(sharer, QuestId, Template(), questState: null, [member], _ => true);

		Assert.Empty(plan.Instructions);
	}

	[Fact]
	public void Plan_CompleteQuestState_ReturnsEmptyPlan()
	{
		var sharer = CreatePlayer(1, "Sharer");
		var member = CreatePlayer(2, "Member");
		var complete = new PlayerQuestState(QuestId, "COMPLETE", 0, 0, 1);

		var plan = QuestSharePlanService.Plan(sharer, QuestId, Template(), complete, [member], _ => true);

		Assert.Empty(plan.Instructions);
	}

	[Fact]
	public void Plan_NoMembers_GroupTarget_SendsNoGroupMembers()
	{
		var sharer = CreatePlayer(1, "Sharer");

		var plan = QuestSharePlanService.Plan(sharer, QuestId, Template(target: "NONE"), InProgress(), [], _ => true);

		var instruction = Assert.Single(plan.Instructions);
		Assert.Equal(sharer.ObjectId, instruction.RecipientObjectId);
		AssertSystemMessage(instruction.Packet, QuestSharePlanService.MsgNoGroupMembers);
	}

	[Fact]
	public void Plan_NoMembers_AllianceTarget_SendsNoAllianceMembers()
	{
		var sharer = CreatePlayer(1, "Sharer");

		var plan = QuestSharePlanService.Plan(sharer, QuestId, Template(target: "ALLIANCE"), InProgress(), [], _ => true);

		var instruction = Assert.Single(plan.Instructions);
		AssertSystemMessage(instruction.Packet, QuestSharePlanService.MsgNoAllianceMembers);
	}

	[Fact]
	public void Plan_ExcludesSharerFromMembers()
	{
		var sharer = CreatePlayer(1, "Sharer");

		// The sharer is present in the team online-member list and must be filtered out (Java allExcept(player)).
		var plan = QuestSharePlanService.Plan(sharer, QuestId, Template(), InProgress(), [sharer], _ => true);

		var instruction = Assert.Single(plan.Instructions);
		AssertSystemMessage(instruction.Packet, QuestSharePlanService.MsgNoGroupMembers);
	}

	[Fact]
	public void Plan_OutOfRangeMember_IsFiltered()
	{
		var sharer = CreatePlayer(1, "Sharer", x: 0);
		var farMember = CreatePlayer(2, "Far", x: 1000); // > GROUP_MAX_DISTANCE (100)

		var plan = QuestSharePlanService.Plan(sharer, QuestId, Template(), InProgress(), [farMember], _ => true);

		var instruction = Assert.Single(plan.Instructions);
		AssertSystemMessage(instruction.Packet, QuestSharePlanService.MsgNoGroupMembers);
	}

	[Fact]
	public void Plan_DifferentWorldMember_IsFiltered()
	{
		var sharer = CreatePlayer(1, "Sharer", worldId: 210010000);
		var otherWorld = CreatePlayer(2, "Other", worldId: 220010000);

		var plan = QuestSharePlanService.Plan(sharer, QuestId, Template(), InProgress(), [otherWorld], _ => true);

		var instruction = Assert.Single(plan.Instructions);
		AssertSystemMessage(instruction.Packet, QuestSharePlanService.MsgNoGroupMembers);
	}

	[Fact]
	public void Plan_EligibleMember_StartConditionsPass_SharesAndConfirms()
	{
		var sharer = CreatePlayer(1, "Sharer");
		var member = CreatePlayer(2, "Member");

		var plan = QuestSharePlanService.Plan(sharer, QuestId, Template(), InProgress(), [member], _ => true);

		Assert.Equal(2, plan.Instructions.Count);
		// Java: SM_QUEST_ACTION.SHARE to the member.
		Assert.Equal(member.ObjectId, plan.Instructions[0].RecipientObjectId);
		Assert.IsType<SmQuestAction>(plan.Instructions[0].Packet);
		Assert.Equal(SmQuestAction.PacketOpCode, ((SmQuestAction)plan.Instructions[0].Packet).OpCode);
		// Java: SM_SYSTEM_MESSAGE 1100002 (shared with %0) to the sharer.
		Assert.Equal(sharer.ObjectId, plan.Instructions[1].RecipientObjectId);
		AssertSystemMessage(plan.Instructions[1].Packet, QuestSharePlanService.MsgSharedWith, "Member");
	}

	[Fact]
	public void Plan_EligibleMember_StartConditionsFail_SendsFailedToShare()
	{
		var sharer = CreatePlayer(1, "Sharer");
		var member = CreatePlayer(2, "Member");

		var plan = QuestSharePlanService.Plan(sharer, QuestId, Template(), InProgress(), [member], _ => false);

		var instruction = Assert.Single(plan.Instructions);
		Assert.Equal(sharer.ObjectId, instruction.RecipientObjectId);
		AssertSystemMessage(instruction.Packet, QuestSharePlanService.MsgFailedToShare, "Member");
	}

	[Fact]
	public void Plan_MixedMembers_PerMemberDecisions()
	{
		var sharer = CreatePlayer(1, "Sharer");
		var passing = CreatePlayer(2, "Pass");
		var failing = CreatePlayer(3, "Fail");

		var plan = QuestSharePlanService.Plan(
			sharer, QuestId, Template(), InProgress(), [passing, failing],
			member => member.ObjectId == passing.ObjectId);

		Assert.Equal(3, plan.Instructions.Count);
		// passing -> SHARE to member + 1100002 to sharer
		Assert.IsType<SmQuestAction>(plan.Instructions[0].Packet);
		Assert.Equal(passing.ObjectId, plan.Instructions[0].RecipientObjectId);
		AssertSystemMessage(plan.Instructions[1].Packet, QuestSharePlanService.MsgSharedWith, "Pass");
		// failing -> 1100003 to sharer
		AssertSystemMessage(plan.Instructions[2].Packet, QuestSharePlanService.MsgFailedToShare, "Fail");
	}

	private static PlayerQuestState InProgress()
	{
		// Java: questState != null && status != COMPLETE.
		return new PlayerQuestState(QuestId, "START", 0, 0, 0);
	}

	private static NearbyQuestTemplateSummary Template(bool cannotShare = false, string target = "NONE")
	{
		return new NearbyQuestTemplateSummary(QuestId, CannotShare: cannotShare, Target: target);
	}

	private static Player CreatePlayer(
		int objectId,
		string name,
		float x = 10,
		int worldId = 210010000,
		PlayerTeamMembership membership = PlayerTeamMembership.Group)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 50,
			IsOnline = true,
			TeamMembership = membership,
			CurrentTeamId = 900,
			Position = new WorldPosition(worldId, x, 20, 30, 0),
		};
	}

	private static void AssertSystemMessage(object packet, int expectedMessageId, params string[] expectedParameters)
	{
		var message = Assert.IsType<SmSystemMessage>(packet);
		Assert.Equal(expectedMessageId, message.MessageId);
		Assert.Equal(expectedParameters, message.Parameters);
	}
}
