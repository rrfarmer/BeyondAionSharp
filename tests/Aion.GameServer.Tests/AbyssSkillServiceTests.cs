using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class AbyssSkillServiceTests
{
	[Fact]
	public void UpdateSkills_AddsElyosTransformSkillsAtConfiguredMinimumRank()
	{
		var player = CreatePlayer("ELYOS", rank: 14);

		var result = AbyssSkillService.UpdateSkills(player);

		Assert.True(result.Changed);
		Assert.Empty(result.RemovedSkills);
		Assert.Equal([11885, 11895], result.AddedSkills.Select(skill => skill.SkillId).ToArray());
		Assert.All(result.AddedSkills, skill =>
		{
			Assert.Equal(1, skill.SkillLevel);
			Assert.Equal(0, skill.SkillType);
		});
	}

	[Fact]
	public void UpdateSkills_RemovesOldRaceSkillsBeforeAddingCurrentRankSet()
	{
		var player = CreatePlayer("ELYOS", rank: 15);
		player.Skills =
		[
			new PlayerSkill { SkillId = 37, SkillLevel = 1 },
			new PlayerSkill { SkillId = 11885, SkillLevel = 1 },
			new PlayerSkill { SkillId = 11895, SkillLevel = 1 },
		];

		var result = AbyssSkillService.UpdateSkills(player);

		Assert.Equal([11885, 11895], result.RemovedSkills.Select(skill => skill.SkillId).ToArray());
		Assert.Equal([11886, 11896, 11899], result.AddedSkills.Select(skill => skill.SkillId).ToArray());
		Assert.Equal([37, 11886, 11896, 11899], result.Skills.Select(skill => skill.SkillId).Order().ToArray());
	}

	[Fact]
	public void UpdateSkills_RemovesTransformSkillsBelowMinimumRank()
	{
		var player = CreatePlayer("ASMODIANS", rank: 13);
		player.Skills =
		[
			new PlayerSkill { SkillId = 11890, SkillLevel = 1 },
			new PlayerSkill { SkillId = 11895, SkillLevel = 1 },
		];

		var result = AbyssSkillService.UpdateSkills(player);

		Assert.Equal([11890, 11895], result.RemovedSkills.Select(skill => skill.SkillId).ToArray());
		Assert.Empty(result.AddedSkills);
		Assert.Empty(result.Skills);
	}

	[Fact]
	public void UpdateSkills_UsesAsmodianRankSpecificSkillSet()
	{
		var player = CreatePlayer("ASMODIANS", rank: 18);

		var result = AbyssSkillService.UpdateSkills(player);

		Assert.Equal([11894, 11898, 11902, 11903, 11904, 11905, 11906], result.AddedSkills.Select(skill => skill.SkillId).ToArray());
	}

	[Fact]
	public void UpdateSkills_HonorsConfiguredMinimumRank()
	{
		var player = CreatePlayer("ELYOS", rank: 14);

		var result = AbyssSkillService.UpdateSkills(player, transformMinRank: 17);

		Assert.False(result.Changed);
		Assert.Empty(result.Skills);
	}

	private static Player CreatePlayer(string race, int rank)
	{
		return new Player
		{
			Race = race,
			AbyssRank = PlayerAbyssRank.Default() with { Rank = rank },
		};
	}
}
