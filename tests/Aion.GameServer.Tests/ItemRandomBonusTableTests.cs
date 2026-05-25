using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Tests;

public sealed class ItemRandomBonusTableTests
{
	[Fact]
	public void AreBonusSetsEqual_MatchesJavaGroupCountComparison()
	{
		var table = new ItemRandomBonusTable(
		[
			new ItemRandomBonusSummary("INVENTORY", 1, CreateModifierGroups(2)),
			new ItemRandomBonusSummary("INVENTORY", 2, CreateModifierGroups(2)),
			new ItemRandomBonusSummary("INVENTORY", 3, CreateModifierGroups(1)),
		]);

		Assert.True(table.AreBonusSetsEqual("INVENTORY", 1, 1));
		Assert.True(table.AreBonusSetsEqual("INVENTORY", 1, 2));
		Assert.False(table.AreBonusSetsEqual("INVENTORY", 1, 3));
		Assert.True(table.AreBonusSetsEqual("INVENTORY", 91, 92));
		Assert.False(table.AreBonusSetsEqual("INVENTORY", 1, 92));
	}

	[Fact]
	public void SelectRandomBonusNumber_UsesOneBasedWeightedGroups()
	{
		var table = new ItemRandomBonusTable(
		[
			new ItemRandomBonusSummary(
				"INVENTORY",
				1,
				CreateModifierGroups(3),
				[1d, 3d, 6d]),
		]);

		Assert.Equal(0, table.SelectRandomBonusNumber("INVENTORY", 99, () => 0d));
		Assert.Equal(1, table.SelectRandomBonusNumber("INVENTORY", 1, () => 0d));
		Assert.Equal(2, table.SelectRandomBonusNumber("INVENTORY", 1, () => 0.25d));
		Assert.Equal(3, table.SelectRandomBonusNumber("INVENTORY", 1, () => 0.75d));
	}

	private static IReadOnlyList<IReadOnlyList<ItemStatModifier>> CreateModifierGroups(int count)
	{
		return Enumerable.Range(1, count)
			.Select(index => (IReadOnlyList<ItemStatModifier>)[new ItemStatModifier("add", $"STAT{index}", index, Bonus: true)])
			.ToArray();
	}
}
