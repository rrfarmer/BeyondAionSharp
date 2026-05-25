using Aion.GameServer.Data;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests;

public sealed class PlayerEnterWorldRepositoryItemStonePersistenceTests
{
	[Fact]
	public void BuildItemStonePersistenceRows_MapsAllInheritedTargetStoneCategories()
	{
		var item = new InventoryItem
		{
			ObjectId = 9001,
			ItemId = 100000002,
			ManaStones =
			[
				new ItemStoneSocket(167000001, 1),
				new ItemStoneSocket(167000002, 3),
			],
			FusionStones =
			[
				new ItemStoneSocket(167100001, 2),
			],
			Godstone = new PlayerGodstone(168000001, ProcCount: 44),
			IdianStone = new PlayerIdianStone(169000001, PolishNumber: 7, PolishCharge: 250),
		};

		var rows = MySqlPlayerEnterWorldRepository.BuildItemStonePersistenceRows(item);

		Assert.Equal(5, rows.Count);
		Assert.Contains(rows, row => row == new ItemStonePersistenceRow(9001, 167000001, Slot: 1, Category: 0, PolishNumber: 0, PolishCharge: 0, ProcCount: 0));
		Assert.Contains(rows, row => row == new ItemStonePersistenceRow(9001, 167000002, Slot: 3, Category: 0, PolishNumber: 0, PolishCharge: 0, ProcCount: 0));
		Assert.Contains(rows, row => row == new ItemStonePersistenceRow(9001, 168000001, Slot: 0, Category: 1, PolishNumber: 0, PolishCharge: 0, ProcCount: 44));
		Assert.Contains(rows, row => row == new ItemStonePersistenceRow(9001, 167100001, Slot: 2, Category: 2, PolishNumber: 0, PolishCharge: 0, ProcCount: 0));
		Assert.Contains(rows, row => row == new ItemStonePersistenceRow(9001, 169000001, Slot: 0, Category: 3, PolishNumber: 7, PolishCharge: 250, ProcCount: 0));
	}

	[Fact]
	public void BuildItemStonePersistenceRows_ReturnsEmptyRowsForPlainItem()
	{
		var item = new InventoryItem { ObjectId = 9002, ItemId = 186000001 };

		var rows = MySqlPlayerEnterWorldRepository.BuildItemStonePersistenceRows(item);

		Assert.Empty(rows);
	}
}

