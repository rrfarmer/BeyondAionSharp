using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class TamperingMutationServiceTests
{
	[Fact]
	public async Task SetTemperingLevel_ParsesStaticDataMaxTamperingForRealJavaWingItem()
	{
		using var temp = TempDirectory.Create();
		var repoRoot = FindRepoRoot();
		var manager = await DataManager.LoadAsync(
			repoRoot,
			cacheDirectory: temp.Path,
			validateWhenCacheChanges: false);
		var staticData = manager.StaticData;

		var template = staticData.ItemTemplates.GetItemTemplate(187000128);

		Assert.NotNull(template);
		Assert.Equal(10, template.MaxTampering);
	}

	[Fact]
	public void SetTemperingLevel_EquippedNonPlumeMarksEquipmentDirtyAndPreservesRandomBonus()
	{
		var item = CreateItem(isEquipped: true, tempering: 2, randomPlumeBonus: 9);
		var template = new ItemTemplateSummary(120001486, "Authorize Test Earring", 0, 0, 1, "EARRING", "NORMAL", "COMMON", "PC_ALL", 1, 0, 1, TemperingName: "TEST_1", MaxTampering: 10);

		var result = TamperingMutationService.SetTemperingLevel(item, template, temperingLevel: 3);

		Assert.Equal(TamperingDirtyTarget.Equipment, result.DirtyTarget);
		Assert.Equal(3, result.UpdatedItem.Tempering);
		Assert.Equal(9, result.UpdatedItem.RandomPlumeBonus);
	}

	[Fact]
	public void SetTemperingLevel_UnequippedPlumeAboveFourAddsPhysicalRandomBonusPerLevel()
	{
		var item = CreateItem(isEquipped: false, tempering: 4, randomPlumeBonus: 7);
		var template = new ItemTemplateSummary(187100011, "Physical Plume", 0, 0, 1, "PLUME", "NORMAL", "COMMON", "PC_ALL", 1, 0, 1, TemperingName: "TSHIRT_PHYSICAL", MaxTampering: 255);
		var rolls = new Queue<int>([1, 3]);

		var result = TamperingMutationService.SetTemperingLevel(
			item,
			template,
			temperingLevel: 6,
			nextInclusiveRandom: (_, _) => rolls.Dequeue());

		Assert.Equal(TamperingDirtyTarget.InventoryStorage, result.DirtyTarget);
		Assert.Equal(6, result.UpdatedItem.Tempering);
		Assert.Equal(11, result.UpdatedItem.RandomPlumeBonus);
	}

	[Fact]
	public void SetTemperingLevel_PlumeAtOrBelowFourResetsRandomBonus()
	{
		var item = CreateItem(isEquipped: false, tempering: 6, randomPlumeBonus: 18);
		var template = new ItemTemplateSummary(187100012, "Magical Plume", 0, 0, 1, "PLUME", "NORMAL", "COMMON", "PC_ALL", 1, 0, 1, TemperingName: "TSHIRT_MAGICAL", MaxTampering: 255);

		var result = TamperingMutationService.SetTemperingLevel(item, template, temperingLevel: 4);

		Assert.Equal(4, result.UpdatedItem.Tempering);
		Assert.Equal(0, result.UpdatedItem.RandomPlumeBonus);
	}

	private static InventoryItem CreateItem(bool isEquipped, int tempering, int randomPlumeBonus)
	{
		return new InventoryItem
		{
			ObjectId = 1001,
			ItemId = 187100011,
			Count = 1,
			Location = 0,
			IsEquipped = isEquipped,
			Tempering = tempering,
			RandomPlumeBonus = randomPlumeBonus,
		};
	}

	private static string FindRepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "game-server", "data", "static_data", "static_data.xml")))
			{
				return directory.FullName;
			}

			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Could not locate repo root containing game-server/data/static_data/static_data.xml.");
	}

	private sealed class TempDirectory : IDisposable
	{
		private TempDirectory(string path)
		{
			Path = path;
		}

		public string Path { get; }

		public static TempDirectory Create()
		{
			var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
			Directory.CreateDirectory(path);
			return new TempDirectory(path);
		}

		public void Dispose()
		{
			if (Directory.Exists(Path))
			{
				Directory.Delete(Path, recursive: true);
			}
		}
	}
}
