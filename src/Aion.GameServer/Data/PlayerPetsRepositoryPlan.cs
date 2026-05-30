namespace Aion.GameServer.Data;

public sealed record PlayerPetRepositoryCommandPlan(string JavaArtifact, string Sql, IReadOnlyList<PlayerPetRepositoryParameter> Parameters);

public sealed record PlayerPetRepositoryParameter(string Name, object? Value);

public sealed record PlayerPetInsertRow(
	int PetObjectId,
	int PlayerObjectId,
	int TemplateId,
	int Decoration,
	string Name,
	DateTime? DespawnTime,
	int ExpireTime
);

public sealed record PlayerPetMoodData(
	int PetObjectId,
	long MoodStartedMillis,
	int ShuggleCounter,
	long MoodCooldownStartedMillis,
	long GiftCooldownStartedMillis,
	DateTime? DespawnTime
);

public static class PlayerPetsRepositoryPlan
{
	// Java parity: PlayerPetsDAO owns the player_pets persistence contract for feed status, doping bag,
	// reuse time, inserts, deletes, loads, name updates, mood data, and used-id queries. This planner
	// keeps those DAO operations as explicit SQL command plans without executing repository I/O.
	public const string SaveFeedStatusSql = "UPDATE player_pets SET hungry_level = ?, feed_progress = ?, reuse_time = ? WHERE id = ?";

	public const string SaveDopingBagSql = "UPDATE player_pets SET dopings = ? WHERE id = ?";

	public const string SetTimeSql = "UPDATE player_pets SET reuse_time = ? WHERE id = ?";

	public const string InsertPlayerPetSql =
		"INSERT INTO player_pets(id, player_id, template_id, decoration, name, despawn_time, expire_time) VALUES(?, ?, ?, ?, ?, ?, ?)";

	public const string RemovePlayerPetSql = "DELETE FROM player_pets WHERE id = ?";

	public const string LoadPlayerPetsSql = "SELECT * FROM player_pets WHERE player_id = ?";

	public const string UpdatePetNameSql = "UPDATE player_pets SET name = ? WHERE id = ?";

	public const string SavePetMoodDataSql =
		"UPDATE player_pets SET mood_started = ?, counter = ?, mood_cd_started = ?, gift_cd_started = ?, despawn_time = ? WHERE id = ?";

	public const string LoadUsedIdsSql = "SELECT id FROM player_pets";

	public static PlayerPetRepositoryCommandPlan SaveFeedStatus(int petObjectId, int hungryLevel, int feedProgress, long reuseTime)
	{
		return new PlayerPetRepositoryCommandPlan(
			"com.aionemu.gameserver.dao.PlayerPetsDAO.saveFeedStatus",
			SaveFeedStatusSql,
			[new("hungry_level", hungryLevel), new("feed_progress", feedProgress), new("reuse_time", reuseTime), new("id", petObjectId)]
		);
	}

	public static PlayerPetRepositoryCommandPlan SaveDopingBag(int petObjectId, IReadOnlyList<int> itemIds)
	{
		return new PlayerPetRepositoryCommandPlan(
			"com.aionemu.gameserver.dao.PlayerPetsDAO.saveDopingBag",
			SaveDopingBagSql,
			[new("dopings", string.Join(",", itemIds)), new("id", petObjectId)]
		);
	}

	public static PlayerPetRepositoryCommandPlan SetTime(int petObjectId, long time)
	{
		return new PlayerPetRepositoryCommandPlan(
			"com.aionemu.gameserver.dao.PlayerPetsDAO.setTime",
			SetTimeSql,
			[new("reuse_time", time), new("id", petObjectId)]
		);
	}

	public static PlayerPetRepositoryCommandPlan InsertPlayerPet(PlayerPetInsertRow row)
	{
		return new PlayerPetRepositoryCommandPlan(
			"com.aionemu.gameserver.dao.PlayerPetsDAO.insertPlayerPet",
			InsertPlayerPetSql,
			[
				new("id", row.PetObjectId),
				new("player_id", row.PlayerObjectId),
				new("template_id", row.TemplateId),
				new("decoration", row.Decoration),
				new("name", row.Name),
				new("despawn_time", row.DespawnTime),
				new("expire_time", row.ExpireTime),
			]
		);
	}

	public static PlayerPetRepositoryCommandPlan RemovePlayerPet(int petObjectId)
	{
		return new PlayerPetRepositoryCommandPlan(
			"com.aionemu.gameserver.dao.PlayerPetsDAO.removePlayerPet",
			RemovePlayerPetSql,
			[new("id", petObjectId)]
		);
	}

	public static PlayerPetRepositoryCommandPlan LoadPlayerPets(int playerObjectId)
	{
		return new PlayerPetRepositoryCommandPlan(
			"com.aionemu.gameserver.dao.PlayerPetsDAO.getPlayerPets",
			LoadPlayerPetsSql,
			[new("player_id", playerObjectId)]
		);
	}

	public static PlayerPetRepositoryCommandPlan UpdatePetName(int petObjectId, string name)
	{
		return new PlayerPetRepositoryCommandPlan(
			"com.aionemu.gameserver.dao.PlayerPetsDAO.updatePetName",
			UpdatePetNameSql,
			[new("name", name), new("id", petObjectId)]
		);
	}

	public static PlayerPetRepositoryCommandPlan SavePetMoodData(PlayerPetMoodData moodData)
	{
		return new PlayerPetRepositoryCommandPlan(
			"com.aionemu.gameserver.dao.PlayerPetsDAO.savePetMoodData",
			SavePetMoodDataSql,
			[
				new("mood_started", moodData.MoodStartedMillis),
				new("counter", moodData.ShuggleCounter),
				new("mood_cd_started", moodData.MoodCooldownStartedMillis),
				new("gift_cd_started", moodData.GiftCooldownStartedMillis),
				new("despawn_time", moodData.DespawnTime),
				new("id", moodData.PetObjectId),
			]
		);
	}

	public static PlayerPetRepositoryCommandPlan LoadUsedIds()
	{
		return new PlayerPetRepositoryCommandPlan("com.aionemu.gameserver.dao.PlayerPetsDAO.getUsedIDs", LoadUsedIdsSql, []);
	}
}
