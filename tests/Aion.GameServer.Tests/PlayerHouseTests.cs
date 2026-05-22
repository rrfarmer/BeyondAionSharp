using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Tests;

public sealed class PlayerHouseTests
{
	[Fact]
	public void GetGraceSeconds_UsesLastAuctionEndBeforeTwoWeekCap()
	{
		var now = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Local);
		var house = new PlayerHouse(51, 700200, 900200, now, null, IsInactive: true);

		var graceSeconds = house.GetGraceSeconds(() => now);

		Assert.Equal(871200, graceSeconds);
	}

	[Fact]
	public void GetGraceSeconds_UsesConfiguredAuctionEndSchedule()
	{
		var acquiredTime = new DateTime(2026, 5, 19, 18, 0, 0, DateTimeKind.Local);
		var schedule = JavaCronSchedule.WeeklyOrDefault("0 30 18 ? * TUE", DayOfWeek.Sunday, 12);
		var house = new PlayerHouse(51, 700200, 900200, acquiredTime, null, IsInactive: true);

		var graceSeconds = house.GetGraceSeconds(() => acquiredTime, schedule);

		Assert.Equal(606600, graceSeconds);
	}

	[Fact]
	public void HouseRegistrySummary_LoadsJavaRowsAndFiltersInvalidDecor()
	{
		var housingTemplates = new HousingTemplateTable(
			Array.Empty<HousingAddressSummary>(),
			[new HousingBuildingSummary(353000, "HOUSE", 1, PartsMatch: "CP_C")],
			[
				new HousingPartSummary(3520000, "ROOF", new HashSet<string>(["CP_C"], StringComparer.OrdinalIgnoreCase)),
				new HousingPartSummary(3500000, "ROOF", new HashSet<string>(["CP_A"], StringComparer.OrdinalIgnoreCase)),
				new HousingPartSummary(3524000, "INWALL_ANY", new HashSet<string>(["CP_C"], StringComparer.OrdinalIgnoreCase)),
			]);
		var objectTemplates = new HousingObjectTemplateTable(
			[
				new HousingObjectTemplateSummary(3001000, 7, "npc", "EXTERIOR", "FLOOR", "NONE", "NPC", 30, false),
				new HousingObjectTemplateSummary(3190001, 1, "use_item", "INTERIOR", "FLOOR", "NONE", "USE_ITEM", 0, false, UseCount: 3, UseActionCheckType: 2),
			]);
		var registry = HouseRegistrySummary.FromRows(
			353000,
			housingTemplates,
			objectTemplates,
			[
				new HouseRegisteredItemRow(9001, 3001000, 1_200, 0x112233, 0, 0, 0, 0, 0, 0, 0, "NONE", 0),
				new HouseRegisteredItemRow(9002, 3190001, null, null, 0, 1, 2, 10, 20, 30, 60, "INTERIOR", 0),
				new HouseRegisteredItemRow(9101, 3520000, null, null, 0, 0, 0, 0, 0, 0, 0, "DECOR", -1),
				new HouseRegisteredItemRow(9102, 3524000, null, null, 0, 0, 0, 0, 0, 0, 0, "DECOR", 1),
				new HouseRegisteredItemRow(9103, 3500000, null, null, 0, 0, 0, 0, 0, 0, 0, "DECOR", -1),
			],
			() => 1_000);

		Assert.Equal(2, registry.Objects.Count);
		var notSpawned = Assert.Single(registry.NotSpawnedObjects);
		Assert.Equal(9001, notSpawned.ObjectId);
		Assert.Equal(200, notSpawned.ExpirationSeconds);
		Assert.Equal((byte)7, notSpawned.TypeId);
		var notSpawnedWithCooldown = Assert.Single(
			registry.GetNotSpawnedObjects(new Dictionary<int, long> { [9001] = 130_000 }, () => 100_000));
		Assert.Equal(30, notSpawnedWithCooldown.CooldownSeconds);
		var spawned = Assert.Single(registry.Objects, obj => obj.IsSpawnedByPlayer);
		Assert.Equal(180, spawned.Rotation);
		Assert.Equal(new byte[] { 3, 0, 0, 0, 2 }, spawned.UsageData);
		var placedObject = Assert.Single(
			registry.GetSpawnedObjects(
				new PlayerHouse(50, 700100, 353000, DateTime.UtcNow, null, IsInactive: false, Registry: registry),
				1001));
		Assert.Equal(700100, placedObject.AddressId);
		Assert.Equal(1001, placedObject.OwnerPlayerId);
		Assert.Equal(180, placedObject.Rotation);
		Assert.Equal(0, placedObject.NpcObjectId);
		var unusedDecor = Assert.Single(registry.UnusedDecorations);
		Assert.Equal(9101, unusedDecor.ObjectId);
		Assert.True(registry.HasInvalidDecorations);
		Assert.Equal(2, registry.Decorations.Count(decor => decor.IsDeleted));
		Assert.DoesNotContain(registry.WithoutObject(9002).Objects, obj => obj.ObjectId == 9002);
	}

	[Fact]
	public void HousingTemplateTable_ResolvesUsedDecorationPacketLines()
	{
		var housingTemplates = new HousingTemplateTable(
			Array.Empty<HousingAddressSummary>(),
			[
				new HousingBuildingSummary(
					353000,
					"HOUSE",
					1,
					DefaultDecorIds: [3500000, 3501000, 3502000, 3503000, 3505000, 3506000, 3504000, 3504000, 3504000, 3504000, 3504000, 3504000, 3507000, 3507000, 3507000, 3507000, 3507000, 3507000, 3508000]),
			],
			[
				new HousingPartSummary(3524000, "INWALL_ANY", new HashSet<string>(["CP_C"], StringComparer.OrdinalIgnoreCase)),
				new HousingPartSummary(3527000, "INFLOOR_ANY", new HashSet<string>(["CP_C"], StringComparer.OrdinalIgnoreCase)),
			]);
		var registry = new HouseRegistrySummary(
			Array.Empty<RegisteredHouseObjectSummary>(),
			[
				new RegisteredHouseDecorationSummary(9101, 3524000, 1),
				new RegisteredHouseDecorationSummary(9102, 3527000, 5),
			]);

		Assert.True(HousingTemplateTable.TryGetDecorLine(9, out var partType, out var room));
		Assert.Equal("INWALL_ANY", partType);
		Assert.Equal(1, room);
		Assert.True(HousingTemplateTable.TryGetDecorPacketIndex("INFLOOR_ANY", 5, out var floorIndex));
		Assert.Equal(17, floorIndex);
		var decorIds = housingTemplates.GetDecorIds(353000, registry);

		Assert.Equal(3524000, decorIds[7]);
		Assert.Equal(3527000, decorIds[17]);
		Assert.Equal(3504000, decorIds[6]);
	}

	[Fact]
	public void HouseRegistrySummary_AppliesDecorationMutation()
	{
		var registry = new HouseRegistrySummary(
			Array.Empty<RegisteredHouseObjectSummary>(),
			[
				new RegisteredHouseDecorationSummary(9101, 3524000, -1),
				new RegisteredHouseDecorationSummary(9102, 3504000, 2),
			]);

		var updated = registry.WithDecorationMutation(
			[new RegisteredHouseDecorationSummary(9101, 3524000, 2)],
			[9102]);

		Assert.Equal(2, updated.GetDecoration(9101)?.Room);
		Assert.Null(updated.GetDecoration(9102));
	}
}
