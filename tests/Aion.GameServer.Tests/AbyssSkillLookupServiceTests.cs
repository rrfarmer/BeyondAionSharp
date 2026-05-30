using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class AbyssSkillLookupServiceTests
{
	[Fact]
	public void CreatePlan_ElyosStar5OfficerHas2Skills()
	{
		var plan = AbyssSkillLookupService.CreatePlan("ELYOS", rankId: 14);

		Assert.True(plan.HasAbyssSkills);
		Assert.Equal(2, plan.SkillIds.Count);
		Assert.Contains(11885, plan.SkillIds);
		Assert.Contains(11895, plan.SkillIds);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_ElyosSupremeCommanderHas7Skills()
	{
		var plan = AbyssSkillLookupService.CreatePlan("ELYOS", rankId: 18);

		Assert.True(plan.HasAbyssSkills);
		Assert.Equal(7, plan.SkillIds.Count);
		Assert.Contains(11889, plan.SkillIds);
	}

	[Fact]
	public void CreatePlan_AsmodiansStar5OfficerHasDifferentSkills()
	{
		var plan = AbyssSkillLookupService.CreatePlan("ASMODIANS", rankId: 14);

		Assert.True(plan.HasAbyssSkills);
		Assert.Equal(2, plan.SkillIds.Count);
		Assert.Contains(11890, plan.SkillIds);
		Assert.Contains(11895, plan.SkillIds);
	}

	[Fact]
	public void CreatePlan_RankBelowStar5OfficerHasNoSkills()
	{
		// Rank 13 = STAR4_OFFICER — no abyss skills
		var plan = AbyssSkillLookupService.CreatePlan("ELYOS", rankId: 13);

		Assert.False(plan.HasAbyssSkills);
		Assert.Empty(plan.SkillIds);
	}

	[Fact]
	public void CreatePlan_SoldierRankHasNoSkills()
	{
		var plan = AbyssSkillLookupService.CreatePlan("ELYOS", rankId: 1);

		Assert.False(plan.HasAbyssSkills);
		Assert.Empty(plan.SkillIds);
	}

	[Fact]
	public void CreatePlan_IsCaseInsensitive()
	{
		var plan = AbyssSkillLookupService.CreatePlan("elyos", rankId: 14);

		Assert.True(plan.HasAbyssSkills);
	}

	[Fact]
	public void MinAbyssSkillRankIdIs14()
	{
		Assert.Equal(14, AbyssSkillLookupService.MinAbyssSkillRankId);
	}
}
