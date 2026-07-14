using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests;

public sealed class PlayerTimestampParityTests
{
	[Theory]
	[InlineData(1_768_496_400L)] // 2026-01-15 12:00 America/New_York (UTC-05)
	[InlineData(1_784_131_200L)] // 2026-07-15 12:00 America/New_York (UTC-04)
	public void LastOnline_PlayerDaoAndSelectionEpochContractMatchesJavaTimestamp(long epochSeconds)
	{
		// Both PlayerDAO and MySqlCharacterSelectionRepository read UNIX_TIMESTAMP(last_online).
		// PlayerDAO materializes the shared UTC instant; selection returns the same epoch directly.
		var loadedByPlayerDao = DatabaseTimestamp.FromUnixTimeSeconds(epochSeconds);
		var commonData = new PlayerCommonData(1001);
		commonData.SetLastOnline(loadedByPlayerDao);

		Assert.Equal(DateTimeKind.Utc, commonData.GetLastOnline()!.Value.Kind);
		Assert.Equal(checked((int)epochSeconds), commonData.GetLastOnlineEpochSeconds());
		Assert.Equal(
			commonData.GetLastOnlineEpochSeconds(),
			checked((int)epochSeconds));
	}

	[Fact]
	public void LastOnline_ModelRejectsDriverStyleUnspecifiedDateTime()
	{
		var commonData = new PlayerCommonData(1001);
		var driverWallClock = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Unspecified);

		Assert.Throws<ArgumentException>(() => commonData.SetLastOnline(driverWallClock));
	}

	[Fact]
	public void EpochFields_UseJavaLongToIntWrapAfter2038()
	{
		const long firstSecondPastInt32 = 2_147_483_648L;
		DateTimeOffset post2038 = DateTimeOffset.FromUnixTimeSeconds(firstSecondPastInt32);
		var commonData = new PlayerCommonData(1001);
		commonData.SetLastOnline(post2038.UtcDateTime);
		var pet = new PlayerOwnedPet(2001, 9001, "Post2038", 0, Birthday: post2038);
		var skill = new PlayerSkill { SkillId = 1, SkillLevel = 1, SkillType = 0 };

		Assert.Equal(int.MinValue, commonData.GetLastOnlineEpochSeconds());
		Assert.Equal(int.MinValue, pet.BirthdayEpochSeconds);
		Assert.Equal(int.MinValue, skill.GetClientFlag(post2038));
	}
}
