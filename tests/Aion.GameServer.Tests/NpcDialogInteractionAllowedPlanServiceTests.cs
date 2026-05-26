using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class NpcDialogInteractionAllowedPlanServiceTests
{
	[Fact]
	public void CreatePlan_AllowsNpcWithoutSummonOwnerOrSubDialog()
	{
		var plan = NpcDialogInteractionAllowedPlanService.CreatePlan(new NpcDialogInteractionAllowedInput(1001));

		Assert.True(plan.IsAllowed);
		Assert.Equal(NpcDialogInteractionAllowedStatus.Allowed, plan.Status);
		Assert.False(plan.IsLive);
	}

	[Theory]
	[InlineData(NpcDialogSummonOwnerType.Private, false, false, false, false, false)]
	[InlineData(NpcDialogSummonOwnerType.Group, false, true, false, false, true)]
	[InlineData(NpcDialogSummonOwnerType.Alliance, false, false, true, false, true)]
	[InlineData(NpcDialogSummonOwnerType.Legion, false, false, false, true, true)]
	[InlineData(NpcDialogSummonOwnerType.Private, true, false, false, false, true)]
	public void CreatePlan_AppliesSummonOwnerRulesBeforeSubDialog(
		NpcDialogSummonOwnerType ownerType,
		bool playerIsCreator,
		bool groupHasCreator,
		bool allianceHasCreator,
		bool legionHasCreator,
		bool expectedAllowedByOwner)
	{
		var playerObjectId = playerIsCreator ? 2002 : 1001;
		var plan = NpcDialogInteractionAllowedPlanService.CreatePlan(
			new NpcDialogInteractionAllowedInput(
				playerObjectId,
				NpcCreatorObjectId: 2002,
				SummonOwnerType: ownerType,
				SubDialogType: NpcSubDialogType.Level,
				SubDialogValue: 50,
				PlayerGroupHasCreator: groupHasCreator,
				PlayerAllianceHasCreator: allianceHasCreator,
				PlayerLegionHasCreator: legionHasCreator,
				PlayerLevel: 50));

		Assert.Equal(expectedAllowedByOwner, plan.IsAllowed);
		Assert.Equal(
			expectedAllowedByOwner ? NpcDialogInteractionAllowedStatus.Allowed : NpcDialogInteractionAllowedStatus.RejectedBySummonOwner,
			plan.Status);
	}

	[Fact]
	public void CreatePlan_RejectsSubDialogWhenFortCaptureFactsDoNotMatch()
	{
		var plan = NpcDialogInteractionAllowedPlanService.CreatePlan(
			new NpcDialogInteractionAllowedInput(
				1001,
				SubDialogType: NpcSubDialogType.FortCapture,
				PlayerHasLegion: true,
				FortZoneFound: true,
				FortressCapturedByPlayerLegion: false));

		Assert.False(plan.IsAllowed);
		Assert.Equal(NpcDialogInteractionAllowedStatus.RejectedByFortCapture, plan.Status);
	}

	[Theory]
	[InlineData(NpcSubDialogType.SkillId, false, NpcDialogInteractionAllowedStatus.RejectedByMissingSkill)]
	[InlineData(NpcSubDialogType.ItemId, false, NpcDialogInteractionAllowedStatus.RejectedByMissingItem)]
	[InlineData(NpcSubDialogType.Return, false, NpcDialogInteractionAllowedStatus.RejectedByMissingReturnItem)]
	[InlineData(NpcSubDialogType.PcBang, false, NpcDialogInteractionAllowedStatus.RejectedByUnhandledSubDialog)]
	public void CreatePlan_RejectsMissingOrUnhandledSubDialogs(
		NpcSubDialogType subDialogType,
		bool expectedAllowed,
		NpcDialogInteractionAllowedStatus expectedStatus)
	{
		var plan = NpcDialogInteractionAllowedPlanService.CreatePlan(
			new NpcDialogInteractionAllowedInput(1001, SubDialogType: subDialogType));

		Assert.Equal(expectedAllowed, plan.IsAllowed);
		Assert.Equal(expectedStatus, plan.Status);
	}

	[Theory]
	[InlineData(NpcSubDialogType.AbyssRank, 4, 5, false)]
	[InlineData(NpcSubDialogType.AbyssRank, 4, 5, true)]
	[InlineData(NpcSubDialogType.AbyssRanking, 11, 10, false)]
	[InlineData(NpcSubDialogType.Level, 49, 50, false)]
	[InlineData(NpcSubDialogType.LevelLow, 51, 50, false)]
	[InlineData(NpcSubDialogType.LevelHigh, 49, 50, false)]
	public void CreatePlan_AppliesRankRankingAndLevelRestrictions(
		NpcSubDialogType subDialogType,
		int playerValue,
		int requiredValue,
		bool playerIsStaff)
	{
		var plan = NpcDialogInteractionAllowedPlanService.CreatePlan(
			new NpcDialogInteractionAllowedInput(
				1001,
				SubDialogType: subDialogType,
				SubDialogValue: requiredValue,
				PlayerIsStaff: playerIsStaff,
				PlayerAbyssRankId: playerValue,
				PlayerAbyssRankingPosition: playerValue,
				PlayerLevel: playerValue));

		Assert.Equal(playerIsStaff && subDialogType == NpcSubDialogType.AbyssRank, plan.IsAllowed);
	}

	[Theory]
	[InlineData(NpcSubDialogType.TargetLegionDominion, 4, 4, 0, false, true)]
	[InlineData(NpcSubDialogType.TargetLegionDominion, 4, 3, 0, false, false)]
	[InlineData(NpcSubDialogType.TargetLegionDominion, 4, 4, 0, true, false)]
	[InlineData(NpcSubDialogType.LegionDominionNpc, 4, 0, 4, false, true)]
	[InlineData(NpcSubDialogType.LegionDominionNpc, 4, 0, 3, false, false)]
	public void CreatePlan_AppliesLegionDominionRestrictions(
		NpcSubDialogType subDialogType,
		int requiredValue,
		int currentDominion,
		int occupiedDominion,
		bool calculationActive,
		bool expectedAllowed)
	{
		var plan = NpcDialogInteractionAllowedPlanService.CreatePlan(
			new NpcDialogInteractionAllowedInput(
				1001,
				SubDialogType: subDialogType,
				SubDialogValue: requiredValue,
				PlayerHasLegion: true,
				LegionDominionCalculationActive: calculationActive,
				PlayerCurrentLegionDominion: currentDominion,
				PlayerOccupiedLegionDominion: occupiedDominion));

		Assert.Equal(expectedAllowed, plan.IsAllowed);
	}
}
