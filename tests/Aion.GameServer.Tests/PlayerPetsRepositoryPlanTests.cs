using Aion.GameServer.Data;

namespace Aion.GameServer.Tests;

public sealed class PlayerPetsRepositoryPlanTests
{
	[Fact]
	public void SaveFeedStatusUsesJavaSqlAndParameterOrder()
	{
		var plan = PlayerPetsRepositoryPlan.SaveFeedStatus(
			petObjectId: 7001,
			hungryLevel: 2,
			feedProgress: 0x12345550,
			reuseTime: 123_456);

		Assert.Equal("com.aionemu.gameserver.dao.PlayerPetsDAO.saveFeedStatus", plan.JavaArtifact);
		Assert.Equal(PlayerPetsRepositoryPlan.SaveFeedStatusSql, plan.Sql);
		AssertParameterValues(plan, 2, 0x12345550, 123_456L, 7001);
	}

	[Fact]
	public void SaveDopingBagSerializesJavaCommaSeparatedSlotOrder()
	{
		var plan = PlayerPetsRepositoryPlan.SaveDopingBag(
			petObjectId: 7001,
			itemIds: [166000001, 162000001, 164000001, 0, 0]);

		Assert.Equal(PlayerPetsRepositoryPlan.SaveDopingBagSql, plan.Sql);
		AssertParameterValues(plan, "166000001,162000001,164000001,0,0", 7001);
	}

	[Fact]
	public void SetTimeUsesJavaReuseTimeSql()
	{
		var plan = PlayerPetsRepositoryPlan.SetTime(petObjectId: 7001, time: 123_456);

		Assert.Equal(PlayerPetsRepositoryPlan.SetTimeSql, plan.Sql);
		AssertParameterValues(plan, 123_456L, 7001);
	}

	[Fact]
	public void InsertPlayerPetUsesJavaColumnOrder()
	{
		var despawnTime = new DateTime(2026, 5, 27, 12, 30, 0, DateTimeKind.Utc);
		var plan = PlayerPetsRepositoryPlan.InsertPlayerPet(new PlayerPetInsertRow(
			PetObjectId: 7001,
			PlayerObjectId: 1001,
			TemplateId: 900001,
			Decoration: 12345,
			Name: "Tog",
			DespawnTime: despawnTime,
			ExpireTime: 3600));

		Assert.Equal(PlayerPetsRepositoryPlan.InsertPlayerPetSql, plan.Sql);
		AssertParameterValues(plan, 7001, 1001, 900001, 12345, "Tog", despawnTime, 3600);
	}

	[Fact]
	public void RemovePlayerPetUsesJavaDeleteSql()
	{
		var plan = PlayerPetsRepositoryPlan.RemovePlayerPet(petObjectId: 7001);

		Assert.Equal(PlayerPetsRepositoryPlan.RemovePlayerPetSql, plan.Sql);
		AssertParameterValues(plan, 7001);
	}

	[Fact]
	public void LoadPlayerPetsUsesJavaSelectAllByPlayerSql()
	{
		var plan = PlayerPetsRepositoryPlan.LoadPlayerPets(playerObjectId: 1001);

		Assert.Equal(PlayerPetsRepositoryPlan.LoadPlayerPetsSql, plan.Sql);
		AssertParameterValues(plan, 1001);
	}

	[Fact]
	public void UpdatePetNameUsesJavaNameSqlAndParameterOrder()
	{
		var plan = PlayerPetsRepositoryPlan.UpdatePetName(petObjectId: 7001, name: "Tog II");

		Assert.Equal(PlayerPetsRepositoryPlan.UpdatePetNameSql, plan.Sql);
		AssertParameterValues(plan, "Tog II", 7001);
	}

	[Fact]
	public void SavePetMoodDataUsesJavaColumnOrder()
	{
		var despawnTime = new DateTime(2026, 5, 27, 12, 30, 0, DateTimeKind.Utc);
		var plan = PlayerPetsRepositoryPlan.SavePetMoodData(new PlayerPetMoodData(
			PetObjectId: 7001,
			MoodStartedMillis: 10_000,
			ShuggleCounter: 3,
			MoodCooldownStartedMillis: 20_000,
			GiftCooldownStartedMillis: 30_000,
			DespawnTime: despawnTime));

		Assert.Equal(PlayerPetsRepositoryPlan.SavePetMoodDataSql, plan.Sql);
		AssertParameterValues(plan, 10_000L, 3, 20_000L, 30_000L, despawnTime, 7001);
	}

	[Fact]
	public void LoadUsedIdsUsesJavaPetIdSqlWithoutParameters()
	{
		var plan = PlayerPetsRepositoryPlan.LoadUsedIds();

		Assert.Equal(PlayerPetsRepositoryPlan.LoadUsedIdsSql, plan.Sql);
		Assert.Empty(plan.Parameters);
	}

	private static void AssertParameterValues(PlayerPetRepositoryCommandPlan plan, params object?[] expected)
	{
		Assert.Equal(expected.Length, plan.Parameters.Count);
		for (var i = 0; i < expected.Length; i++)
		{
			Assert.Equal(expected[i], plan.Parameters[i].Value);
		}
	}
}
