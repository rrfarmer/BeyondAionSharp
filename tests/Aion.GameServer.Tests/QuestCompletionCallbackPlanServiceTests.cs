using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestCompletionCallbackPlanServiceTests
{
	[Fact]
	public void CreatePlan_DeduplicatesRegistrationsAndPreservesFirstJavaOrder()
	{
		var plan = QuestCompletionCallbackPlanService.CreatePlan(
			1001,
		[
			Registration(2001, "_2001First.java"),
			Registration(3001, "_3001Second.java"),
			Registration(2001, "_2001Duplicate.java"),
		]);

		Assert.Equal(QuestCompletionCallbackPlanStatus.Ready, plan.Status);
		Assert.True(plan.HasHandlers);
		Assert.All(plan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.All(plan.Descriptors, descriptor => Assert.True(descriptor.UsesSharedQuestEnv));
		Assert.Equal([2001, 3001], plan.Descriptors.Select(descriptor => descriptor.RegisteredQuestId));
		Assert.Equal([1001, 1001], plan.Descriptors.Select(descriptor => descriptor.CompletedQuestId));
		Assert.Equal([1, 2], plan.Descriptors.Select(descriptor => descriptor.Order));
		Assert.Equal(
		[
			"_2001First.java",
			"_3001Second.java",
		], plan.Descriptors.Select(descriptor => descriptor.HandlerJavaSource));
	}

	[Fact]
	public void CreatePlan_RecordsMissingHandlerLookupWithoutStoppingLoop()
	{
		var plan = QuestCompletionCallbackPlanService.CreatePlan(
			1001,
		[
			Registration(2001, "_2001Missing.java", handlerExists: false),
			Registration(3001, "_3001Second.java"),
		]);

		Assert.Equal(QuestCompletionCallbackPlanStatus.Ready, plan.Status);
		Assert.Equal(
		[
			QuestCompletionCallbackAction.SkipMissingHandler,
			QuestCompletionCallbackAction.InvokeHandler,
		], plan.Descriptors.Select(descriptor => descriptor.Action));
		Assert.All(plan.Descriptors, descriptor => Assert.False(descriptor.StopsRemainingHandlers));
	}

	[Fact]
	public void CreatePlan_StopsAfterThrowingHandlerLikeJavaWholeLoopCatch()
	{
		var plan = QuestCompletionCallbackPlanService.CreatePlan(
			1001,
		[
			Registration(2001, "_2001First.java"),
			Registration(3001, "_3001Throws.java", throwsBeforeReturning: true),
			Registration(4001, "_4001NeverReached.java"),
		]);

		Assert.Equal(QuestCompletionCallbackPlanStatus.StoppedByHandlerException, plan.Status);
		Assert.Equal([2001, 3001], plan.Descriptors.Select(descriptor => descriptor.RegisteredQuestId));
		Assert.False(plan.Descriptors[0].StopsRemainingHandlers);
		Assert.True(plan.Descriptors[1].StopsRemainingHandlers);
	}

	[Fact]
	public void CreatePlan_CarriesDefaultFollowUpMetadataWithoutExecutingHandler()
	{
		var plan = QuestCompletionCallbackPlanService.CreatePlan(
			1001,
		[
			Registration(
				2001,
				"_2001FollowUp.java",
				usesDefaultFollowUp: true,
				followUpQuestId: 2001),
		]);

		var descriptor = Assert.Single(plan.Descriptors);
		Assert.Equal(QuestCompletionCallbackAction.InvokeHandler, descriptor.Action);
		Assert.True(descriptor.UsesDefaultFollowUp);
		Assert.Equal(2001, descriptor.FollowUpQuestId);
		Assert.False(descriptor.IsLive);
	}

	[Fact]
	public void CreatePlan_CarriesDetailedFollowUpPlanWithoutExecutingHandler()
	{
		var followUpPlan = QuestCompletionFollowUpPlanService.CreatePlan(
		[
			new QuestCompletionFollowUpRequest(
				FollowUpQuestId: 2001,
				Decision: QuestCompletionFollowUpDecision.Lock,
				StartConditionsEvaluatedByCaller: true),
		]);

		var plan = QuestCompletionCallbackPlanService.CreatePlan(
			1001,
		[
			new QuestCompletionCallbackRegistration(
				RegisteredQuestId: 2001,
				HandlerJavaSource: "_2001FollowUp.java",
				UsesDefaultFollowUp: true,
				FollowUpQuestId: 2001,
				FollowUpPlan: followUpPlan),
		]);

		var descriptor = Assert.Single(plan.Descriptors);
		Assert.Same(followUpPlan, descriptor.FollowUpPlan);
		Assert.NotNull(descriptor.FollowUpPlan);
		var followUpDescriptor = Assert.Single(descriptor.FollowUpPlan!.Descriptors);
		Assert.Equal(QuestCompletionFollowUpPacketAction.Add, followUpDescriptor.PacketAction);
		Assert.Equal("LOCKED", followUpDescriptor.TargetQuestStatus);
		Assert.True(followUpDescriptor.StartConditionsEvaluatedByCaller);
		Assert.False(followUpDescriptor.IsLive);
	}

	[Fact]
	public void CreatePlan_DoesNotCarryDuplicateFollowUpPlanForDuplicateRegistration()
	{
		var firstFollowUpPlan = QuestCompletionFollowUpPlanService.CreatePlan(
		[
			new QuestCompletionFollowUpRequest(2001, QuestCompletionFollowUpDecision.Lock),
		]);
		var duplicateFollowUpPlan = QuestCompletionFollowUpPlanService.CreatePlan(
		[
			new QuestCompletionFollowUpRequest(2001, QuestCompletionFollowUpDecision.Start),
		]);

		var plan = QuestCompletionCallbackPlanService.CreatePlan(
			1001,
		[
			new QuestCompletionCallbackRegistration(2001, "_2001First.java", FollowUpPlan: firstFollowUpPlan),
			new QuestCompletionCallbackRegistration(2001, "_2001Duplicate.java", FollowUpPlan: duplicateFollowUpPlan),
		]);

		var descriptor = Assert.Single(plan.Descriptors);
		Assert.Same(firstFollowUpPlan, descriptor.FollowUpPlan);
		Assert.Equal("LOCKED", Assert.Single(descriptor.FollowUpPlan!.Descriptors).TargetQuestStatus);
	}

	[Fact]
	public void CreatePlan_CarriesThrowingHandlerFollowUpPlanThenStopsRemainingHandlers()
	{
		var followUpPlan = QuestCompletionFollowUpPlanService.CreatePlan(
		[
			new QuestCompletionFollowUpRequest(3001, QuestCompletionFollowUpDecision.Start),
		]);

		var plan = QuestCompletionCallbackPlanService.CreatePlan(
			1001,
		[
			Registration(2001, "_2001First.java"),
			new QuestCompletionCallbackRegistration(
				RegisteredQuestId: 3001,
				HandlerJavaSource: "_3001Throws.java",
				ThrowsBeforeReturning: true,
				FollowUpPlan: followUpPlan),
			Registration(4001, "_4001NeverReached.java"),
		]);

		Assert.Equal(QuestCompletionCallbackPlanStatus.StoppedByHandlerException, plan.Status);
		Assert.Equal([2001, 3001], plan.Descriptors.Select(descriptor => descriptor.RegisteredQuestId));
		Assert.Same(followUpPlan, plan.Descriptors[1].FollowUpPlan);
		Assert.True(plan.Descriptors[1].StopsRemainingHandlers);
	}

	[Fact]
	public void CreatePlan_ReturnsNoHandlersForEmptyRegistrationList()
	{
		var plan = QuestCompletionCallbackPlanService.CreatePlan(1001, []);

		Assert.Equal(QuestCompletionCallbackPlanStatus.NoHandlers, plan.Status);
		Assert.False(plan.HasHandlers);
		Assert.Empty(plan.Descriptors);
	}

	private static QuestCompletionCallbackRegistration Registration(
		int registeredQuestId,
		string handlerJavaSource,
		bool handlerExists = true,
		bool throwsBeforeReturning = false,
		bool usesDefaultFollowUp = false,
		int? followUpQuestId = null)
	{
		return new QuestCompletionCallbackRegistration(
			registeredQuestId,
			handlerJavaSource,
			handlerExists,
			throwsBeforeReturning,
			usesDefaultFollowUp,
			followUpQuestId);
	}
}
