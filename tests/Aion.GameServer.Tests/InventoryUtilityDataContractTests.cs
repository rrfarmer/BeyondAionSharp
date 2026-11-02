using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

public sealed class InventoryUtilityDataContractTests
{
	[Fact]
	public async Task G6UsesExistingWrappableGearOreRewardsBoxChoicesAndCubeService()
	{
		var data = (await RealStaticData.LoadAsync()).StaticData;
		var weapon = data.ItemDataDh.GetItemTemplate(InventoryUtilityScenario.WeaponId);
		var scroll = data.ItemDataDh.GetItemTemplate(InventoryUtilityScenario.WrapId);
		Assert.False(weapon.IsTradeable());
		Assert.False(weapon.CanTune());
		Assert.True(weapon.GetPackCount() > 0);
		Assert.True(scroll.GetLevel() >= weapon.GetLevel());
		Assert.Equal(weapon.GetItemQuality(), scroll.GetItemQuality());
		var pack = Assert.IsType<PackAction>(Assert.Single(scroll.GetActions().GetItemActions()));
		Assert.Equal(UseTarget.WEAPON, pack.GetTarget());
		var ore = data.ItemDataDh.GetItemTemplate(InventoryUtilityScenario.OreId);
		Assert.Equal(3000, ore.GetCastingDelay());
		Assert.Equal(0, ore.GetUseLimits().GetDelayTime());
		var ironOre = data.DecomposableItemsDataDh.Get(InventoryUtilityScenario.OreId);
		Assert.False(ironOre.IsSelectable());
		var oreSet = Assert.Single(ironOre.GetSets());
		var reward = Assert.Single(oreSet.GetItems());
		Assert.Equal(InventoryUtilityScenario.ProductId, reward.GetItemId());
		Assert.Equal(3, reward.GetCount());
		Assert.Equal(100, reward.GetChance());
		// Upstream d254ea172 reshaped this box from a selection window into an only_one box with a branch per class.
		var box = data.DecomposableItemsDataDh.Get(InventoryUtilityScenario.BoxId);
		Assert.False(box.IsSelectable());
		var gladiator = Assert.Single(box.GetSets(), set => set.PlayerClassesRaw == nameof(PlayerClass.GLADIATOR));
		Assert.Equal(InventoryUtilityScenario.GladiatorRolls, gladiator.GetItems().Select(item => item.GetItemId()).Order());
		// The branch covers the whole roll, so the scenario's Gladiator always receives exactly one of its weapons.
		Assert.Equal(100f, gladiator.GetItems().Sum(item => item.GetChance()), 2);
		foreach (var item in gladiator.GetItems())
			Assert.Equal(1, item.GetCount());
		Assert.True(data.NpcDataDh.GetNpcTemplate(InventoryUtilityScenario.NpcId).SupportsAction(DialogAction.EXTEND_INVENTORY));
		Assert.Equal(1000, data.CubeExpandDataDh.GetCubeExpansionTemplate(InventoryUtilityScenario.NpcId).GetPrice(1));
		var spawns = XDocument.Load(Path.Combine(RealStaticData.RepoRoot(), "game-server/data/static_data/spawns/Npcs/110010000_Sanctum.xml"));
		var spawn = Assert.Single(spawns.Descendants("spawn"), spawn => (int?)spawn.Attribute("npc_id") == InventoryUtilityScenario.NpcId);
		var spot = Assert.Single(spawn.Elements("spot"));
		Assert.Equal(InventoryUtilityScenario.Position.X, (float)spot.Attribute("x")!);
		Assert.Equal(InventoryUtilityScenario.Position.Y, (float)spot.Attribute("y")!);
		Assert.Equal(InventoryUtilityScenario.Position.Z, (float)spot.Attribute("z")!);
	}
}
