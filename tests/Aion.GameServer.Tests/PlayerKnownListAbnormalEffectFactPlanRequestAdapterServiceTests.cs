using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerKnownListAbnormalEffectFactPlanRequestAdapterServiceTests
{
	[Fact]
	public void AttachAbnormalEffectResolution_AddsResolverResultForAbnormalRequest()
	{
		var service = new PlayerKnownListAbnormalEffectFactPlanRequestAdapterService();
		var subject = CreatePlayer(SubjectPlayerObjectId);
		subject.AbnormalState = PlayerAbnormalState.Root;
		var request = CreateRequest(subject);

		var adapted = service.AttachAbnormalEffectResolution(
			request,
			new Dictionary<int, IReadOnlyList<PlayerKnownListAbnormalEffectSnapshotEntry>>
			{
				[SubjectPlayerObjectId] = [CreateEntry(skillId: 1200)],
			});

		Assert.NotSame(request, adapted);
		Assert.NotNull(adapted.AbnormalEffectResolution);
		Assert.Equal(PlayerKnownListAbnormalEffectFactResolutionStatus.ResolvedSnapshot, adapted.AbnormalEffectResolution.Status);
		Assert.Equal((int)PlayerAbnormalState.Root, adapted.AbnormalEffectResolution.Facts!.AbnormalEffectMask);
		Assert.Equal(1200, Assert.Single(adapted.AbnormalEffectResolution.Facts.Effects).SkillId);
	}

	[Fact]
	public void AttachAbnormalEffectResolution_PreservesSuppliedFactsAndExplicitResolution()
	{
		var service = new PlayerKnownListAbnormalEffectFactPlanRequestAdapterService();
		var suppliedRequest = CreateRequest(
			CreatePlayer(SubjectPlayerObjectId),
			abnormalEffects: [],
			abnormalEffectMask: 0x20);
		var explicitResolution = new PlayerKnownListAbnormalEffectFactResolution(
			PlayerKnownListAbnormalEffectFactResolutionStatus.MissingEffectSnapshot,
			Facts: null,
			NeedsJavaEffectControllerParity: true,
			IsLive: false,
			IsJavaEffectControllerParity: false,
			"com.aionemu.gameserver.controllers.effect.EffectController.getAbnormalEffects",
			"Already resolved.");
		var explicitRequest = CreateRequest(CreatePlayer(SubjectPlayerObjectId)) with
		{
			AbnormalEffectResolution = explicitResolution,
		};

		var suppliedAdapted = service.AttachAbnormalEffectResolution(
			suppliedRequest,
			new Dictionary<int, IReadOnlyList<PlayerKnownListAbnormalEffectSnapshotEntry>>
			{
				[SubjectPlayerObjectId] = [CreateEntry(skillId: 1200)],
			});
		var explicitAdapted = service.AttachAbnormalEffectResolution(
			explicitRequest,
			new Dictionary<int, IReadOnlyList<PlayerKnownListAbnormalEffectSnapshotEntry>>
			{
				[SubjectPlayerObjectId] = [CreateEntry(skillId: 1200)],
			});

		Assert.Same(suppliedRequest, suppliedAdapted);
		Assert.Same(explicitRequest, explicitAdapted);
		Assert.Same(explicitResolution, explicitAdapted.AbnormalEffectResolution);
	}

	[Fact]
	public void AttachAbnormalEffectResolution_NonAbnormalRequestIsUnchanged()
	{
		var service = new PlayerKnownListAbnormalEffectFactPlanRequestAdapterService();
		var request = new PlayerKnownListPacketConstructionFactPlanRequest(
			CreatePlayer(ViewerPlayerObjectId),
			CreatePlayer(SubjectPlayerObjectId),
			new PlayerKnownListOperationSideEffectDirectionFacts());

		var adapted = service.AttachAbnormalEffectResolution(
			request,
			new Dictionary<int, IReadOnlyList<PlayerKnownListAbnormalEffectSnapshotEntry>>
			{
				[SubjectPlayerObjectId] = [CreateEntry(skillId: 1200)],
			});

		Assert.Same(request, adapted);
		Assert.Null(adapted.AbnormalEffectResolution);
	}

	[Fact]
	public void AttachAbnormalEffectResolution_WithOptInMissingSnapshotAddsBlockedMetadata()
	{
		var service = new PlayerKnownListAbnormalEffectFactPlanRequestAdapterService();
		var request = CreateRequest(CreatePlayer(SubjectPlayerObjectId));

		var adapted = service.AttachAbnormalEffectResolution(
			request,
			new Dictionary<int, IReadOnlyList<PlayerKnownListAbnormalEffectSnapshotEntry>>());

		Assert.NotNull(adapted.AbnormalEffectResolution);
		Assert.Equal(PlayerKnownListAbnormalEffectFactResolutionStatus.MissingEffectSnapshot, adapted.AbnormalEffectResolution.Status);
		Assert.Null(adapted.AbnormalEffectResolution.Facts);
	}

	private static PlayerKnownListPacketConstructionFactPlanRequest CreateRequest(
		Player subject,
		IReadOnlyList<Aion.GameServer.Network.Aion.ServerPackets.SmAbnormalEffectEntry>? abnormalEffects = null,
		int? abnormalEffectMask = null) =>
		new(
			CreatePlayer(ViewerPlayerObjectId),
			subject,
			new PlayerKnownListOperationSideEffectDirectionFacts(SubjectHasAbnormalEffects: true),
			AbnormalEffects: abnormalEffects,
			AbnormalEffectMask: abnormalEffectMask);

	private static Player CreatePlayer(int objectId) =>
		new()
		{
			ObjectId = objectId,
			Race = "ELYOS",
			Gender = "MALE",
			PlayerClass = "GLADIATOR",
		};

	private static PlayerKnownListAbnormalEffectSnapshotEntry CreateEntry(int skillId) =>
		new(
			EffectorObjectId: 7001,
			skillId,
			SkillLevel: 3,
			TargetSlotId: 1,
			TargetSlotOrdinal: 0,
			RemainingTimeToDisplayMillis: 30_000);

	private const int ViewerPlayerObjectId = 9001;
	private const int SubjectPlayerObjectId = 9002;
}
