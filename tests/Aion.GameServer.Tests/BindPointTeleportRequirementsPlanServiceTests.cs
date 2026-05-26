using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportRequirementsPlanServiceTests
{
	[Fact]
	public void CreatePlan_RejectsInvalidStartWorldBeforeRaceKinahAndCooldown()
	{
		var plan = BindPointTeleportRequirementsPlanService.CreatePlan(
			hotspotId: 5001,
			playerWorldId: 110010000,
			hotspotWorldId: 120010000,
			playerRace: "ELYOS",
			hotspotRace: "ASMODIANS",
			currentKinah: 0,
			requiredPrice: 10_000,
			cooldownTimeLeftSeconds: 30);

		Assert.False(plan.IsLive);
		Assert.False(plan.CanTeleport);
		Assert.Equal(BindPointTeleportRequirementStatus.InvalidStartWorld, plan.Status);
		Assert.Equal("STR_CANNOT_MOVE_TO_AIRPORT_NO_ROUTE", plan.SystemMessage);
		Assert.Contains("invalid start world 110010000, expected 120010000", plan.AuditMessage, StringComparison.Ordinal);
		Assert.Contains("BindPointTeleportService.checkRequirements", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_RejectsInvalidRaceWithoutSystemMessage()
	{
		var plan = BindPointTeleportRequirementsPlanService.CreatePlan(
			hotspotId: 5002,
			playerWorldId: 120010000,
			hotspotWorldId: 120010000,
			playerRace: "ELYOS",
			hotspotRace: "ASMODIANS",
			currentKinah: 10_000,
			requiredPrice: 1_000);

		Assert.Equal(BindPointTeleportRequirementStatus.InvalidRace, plan.Status);
		Assert.Null(plan.SystemMessage);
		Assert.Equal("tried to use hotspot teleport 5002 for invalid race ELYOS, expected ASMODIANS", plan.AuditMessage);
	}

	[Fact]
	public void CreatePlan_AllowsPcAllPlayerRaceLikeJava()
	{
		var plan = BindPointTeleportRequirementsPlanService.CreatePlan(
			hotspotId: 5003,
			playerWorldId: 120010000,
			hotspotWorldId: 120010000,
			playerRace: "PC_ALL",
			hotspotRace: "ASMODIANS",
			currentKinah: 10_000,
			requiredPrice: 1_000);

		Assert.Equal(BindPointTeleportRequirementStatus.Ready, plan.Status);
		Assert.True(plan.CanTeleport);
	}

	[Fact]
	public void CreatePlan_RejectsNotEnoughKinahBeforeCooldown()
	{
		var plan = BindPointTeleportRequirementsPlanService.CreatePlan(
			hotspotId: 5004,
			playerWorldId: 120010000,
			hotspotWorldId: 120010000,
			playerRace: "ASMODIANS",
			hotspotRace: "ASMODIANS",
			currentKinah: 999,
			requiredPrice: 1_000,
			cooldownTimeLeftSeconds: 30);

		Assert.Equal(BindPointTeleportRequirementStatus.NotEnoughKinah, plan.Status);
		Assert.Equal("STR_CANNOT_MOVE_TO_AIRPORT_NOT_ENOUGH_FEE", plan.SystemMessage);
		Assert.Null(plan.AuditMessage);
	}

	[Fact]
	public void CreatePlan_RejectsActiveCooldownAfterKinahPasses()
	{
		var plan = BindPointTeleportRequirementsPlanService.CreatePlan(
			hotspotId: 5005,
			playerWorldId: 120010000,
			hotspotWorldId: 120010000,
			playerRace: "ASMODIANS",
			hotspotRace: "ASMODIANS",
			currentKinah: 1_000,
			requiredPrice: 1_000,
			cooldownTimeLeftSeconds: 1);

		Assert.Equal(BindPointTeleportRequirementStatus.CooldownNotReady, plan.Status);
		Assert.Equal("STR_FLYING_TIME_NOT_READY", plan.SystemMessage);
		Assert.Equal(1, plan.CooldownTimeLeftSeconds);
	}

	[Fact]
	public void CreatePlan_AllowsMissingOrExpiredCooldown()
	{
		var noCooldown = BindPointTeleportRequirementsPlanService.CreatePlan(
			hotspotId: 5006,
			playerWorldId: 120010000,
			hotspotWorldId: 120010000,
			playerRace: "ELYOS",
			hotspotRace: "ELYOS",
			currentKinah: 1_000,
			requiredPrice: 1_000);
		var expiredCooldown = BindPointTeleportRequirementsPlanService.CreatePlan(
			hotspotId: 5007,
			playerWorldId: 120010000,
			hotspotWorldId: 120010000,
			playerRace: "ELYOS",
			hotspotRace: "ELYOS",
			currentKinah: 1_000,
			requiredPrice: 1_000,
			cooldownTimeLeftSeconds: 0);

		Assert.Equal(BindPointTeleportRequirementStatus.Ready, noCooldown.Status);
		Assert.True(noCooldown.CanTeleport);
		Assert.Equal(BindPointTeleportRequirementStatus.Ready, expiredCooldown.Status);
		Assert.True(expiredCooldown.CanTeleport);
	}
}
