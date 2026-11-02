using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>G6: ordinary wrapping/unwrapping, timed decomposition, a per-class random box and paid cube expansion.</summary>
public static class InventoryUtilityScenario
{
	public const int MapId = 110010000, NpcId = 798011;
	public static BotPosition Position { get; } = new(1407.79f, 1434.3f, 573.483f, 98);
	public const int WeaponId = 100001129, WrapId = 165020008;
	public const int OreId = 152000103, ProductId = 152000102, BoxId = 188051396;
	/// <summary>
	/// The weapons the box can yield to this scenario's Gladiator. Upstream d254ea172 reshaped 188051396 from a
	/// selection window into an only_one box with a branch per class, so the applicable branch is the Gladiator one and
	/// its six chances add up to 100: exactly one of these always arrives. The other classes' branches are never
	/// reachable here, which is what makes this list the whole expected outcome.
	/// </summary>
	public static IReadOnlyList<int> GladiatorRolls { get; } = new[]
	{
		100000275, 100100191, 100200298, 100900197, 101300194, 101700216
	};
	public static IReadOnlyList<(int Id, int Count)> Grants { get; } = new[]
	{
		(BotWorldModel.KinahItemId, 100_000), (WeaponId, 1), (WrapId, 1), (OreId, 2), (BoxId, 1)
	};

	public static async Task RunAsync(IGearSocketDriver driver, CancellationToken token = default)
	{
		int npc = 0;
		await driver.StepAsync("prepare-raw-items-and-approach-cube-expander", async ct =>
		{
			npc = await driver.PrepareAsync(ct);
			await driver.SynchronizeAsync(ct);
		}, token);
		await driver.StepAsync("wrap-ordinary-weapon-with-consumed-scroll", async ct =>
		{
			var before = Copy(); var weapon = Item(WeaponId); var scroll = Item(WrapId);
			Require(weapon.Details.PackCount == 0, "Setup supplied an already wrapped item.");
			driver.Api.Timing.EnsureCanMove();
			await driver.SendAsync(GameClientPackets.UseItem(scroll.ObjectId, 2, weapon.ObjectId), ct);
			await driver.WaitAsync(typeof(SM_INVENTORY_UPDATE_ITEM), p => p.Get<int>("objectId") == weapon.ObjectId, ct);
			await driver.SynchronizeAsync(ct);
			Consume(before, scroll);
			before[weapon.ObjectId] = weapon with { Details = weapon.Details with { PackCount = 1 } };
			AssertInventory(before, "wrap");
			await driver.VerifyPersistenceAsync(ct);
		}, token);
		await driver.StepAsync("unwrap-and-persist-negative-wrap-count", async ct =>
		{
			var before = Copy(); var weapon = Item(WeaponId);
			await driver.SendAsync(driver.Api.UnwrapItem(weapon.ObjectId), ct);
			var receipt = await driver.WaitAsync(typeof(SM_UNWRAP_ITEM), p => p.Get<int>("objectId") == weapon.ObjectId, ct);
			Require(receipt.Get<byte>("count") == 1, "Unwrap receipt lost the prior wrap count.");
			await driver.WaitAsync(typeof(SM_INVENTORY_UPDATE_ITEM), p => p.Get<int>("objectId") == weapon.ObjectId, ct);
			await driver.SynchronizeAsync(ct);
			// Java writeC(-1) is 0xff: the sign records an unwrapped item, not a fresh wrap allowance.
			before[weapon.ObjectId] = weapon with { Details = weapon.Details with { PackCount = 255 } };
			AssertInventory(before, "unwrap");
			await driver.VerifyPersistenceAsync(ct);
		}, token);
		for (int attempt = 1; attempt <= 2; attempt++)
			await driver.StepAsync($"decompose-ore-with-three-second-cast-{attempt}", async ct =>
			{
				var before = Copy(); var ore = Item(OreId);
				driver.Api.Timing.EnsureCanMove();
				await driver.SendAsync(GameClientPackets.UseItem(ore.ObjectId), ct);
				var start = await driver.WaitAsync(typeof(SM_ITEM_USAGE_ANIMATION), p => p.Get<int>("itemObjId") == ore.ObjectId && p.Get<byte>("animationId") == 0, ct);
				Require(start.Get<int>("castTime") == 3000, "Decomposition did not use the template's three-second cast.");
				await driver.DelayAsync(TimeSpan.FromMilliseconds(3100), ct);
				await driver.WaitAsync(typeof(SM_ITEM_USAGE_ANIMATION), p => p.Get<int>("itemObjId") == ore.ObjectId && p.Get<byte>("animationId") == 1, ct);
				await driver.SynchronizeAsync(ct);
				Consume(before, ore);
				AddProduct(before, ProductId, 3);
				AssertInventory(before, "decompose");
			}, token);
		await driver.StepAsync("open-class-box-consuming-it-for-one-rolled-weapon", async ct =>
		{
			var before = Copy(); var box = Item(BoxId);
			driver.Api.Timing.EnsureCanMove();
			await driver.SendAsync(GameClientPackets.UseItem(box.ObjectId), ct);
			// No selection window: the box rolls its class branch, is consumed, and the weapon arrives on its own.
			await driver.WaitAsync(typeof(SM_ITEM_USAGE_ANIMATION), p => p.Get<int>("itemObjId") == box.ObjectId && p.Get<byte>("animationId") == 1, ct);
			await driver.SynchronizeAsync(ct);
			Consume(before, box);
			int[] rolled = [.. driver.Api.World.Inventory.Values.Select(item => item.ItemId).Where(GladiatorRolls.Contains)];
			Require(rolled.Length == 1, "An only_one box granted none or several of its class branch's weapons.");
			AddProduct(before, rolled[0], 1);
			AssertInventory(before, "open class box");
		}, token);
		await driver.StepAsync("decline-then-accept-priced-cube-expansion", async ct =>
		{
			var original = Cube();
			Require(original.Npc == 0, "Setup supplied an already NPC-expanded cube.");
			await driver.SendAsync(driver.Api.Target(npc), ct);
			await driver.SendAsync(driver.Api.TalkTo(npc), ct);
			await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), p => p.Get<int>("targetObjectId") == npc, ct);
			var before = Copy();
			await RequestExpansionAsync(ct);
			await driver.SendAsync(driver.Api.Answer(0), ct);
			await driver.SynchronizeAsync(ct);
			Require(Cube() == original, "Declined expansion changed the cube.");
			AssertInventory(before, "declined expansion");
			await RequestExpansionAsync(ct);
			await driver.SendAsync(driver.Api.Answer(1), ct);
			await driver.WaitAsync(typeof(SM_CUBE_UPDATE), p => p.Get<byte>("action") == 0 && p.Get<byte>("actionValue") == 0 && p.Get<byte>("npcExpands") == 1, ct);
			await driver.SynchronizeAsync(ct);
			Require(Cube() == original with { Npc = 1 } && Cube().Capacity == original.Capacity + 9, "Expansion did not add exactly nine slots.");
			var kinah = before.Values.Single(item => item.ItemId == BotWorldModel.KinahItemId);
			before[kinah.ObjectId] = kinah with { Count = kinah.Count - 1000 };
			AssertInventory(before, "paid expansion");
			await driver.SendAsync(driver.Api.CloseDialog(npc), ct);
			await driver.VerifyPersistenceAsync(ct);
			Require(Cube() == original with { Npc = 1 }, "Cube expansion was lost at relog.");
		}, token);

		async Task RequestExpansionAsync(CancellationToken ct)
		{
			await driver.SendAsync(driver.Api.SelectDialog(npc, DialogAction.EXTEND_INVENTORY), ct);
			var question = await driver.WaitAsync(typeof(SM_QUESTION_WINDOW), p => p.Get<int>("code") == SM_QUESTION_WINDOW.STR_WAREHOUSE_EXPAND_WARNING, ct);
			Require(question.Get<string[]>("params").SequenceEqual(new[] { "1000", "", "" }), "Cube quote differs from the shipped price or its two reserved parameter slots.");
		}
		BotCubeExpansion Cube() => driver.Api.World.CubeExpansion ?? throw new InvalidDataException("No cube-size packet received.");
		BotInventoryItem Item(int id) => driver.Api.World.Inventory.Values.Single(item => item.ItemId == id);
		Dictionary<int, BotInventoryItem> Copy() => driver.Api.World.Inventory.ToDictionary();
		void AddProduct(Dictionary<int, BotInventoryItem> expected, int id, int count)
		{
			var product = Item(id);
			var previous = expected.Values.SingleOrDefault(item => item.ItemId == id);
			Require(product.Count == (previous?.Count ?? 0) + count, "Wrong decomposed/selected reward quantity.");
			if (previous != null)
			{
				Require(product.ObjectId == previous.ObjectId, "Stacked reward changed identity.");
				expected[previous.ObjectId] = previous with { Count = previous.Count + count };
			}
			else expected.Add(product.ObjectId, product); // New identity/metadata are independently checked at relog.
		}
		void AssertInventory(Dictionary<int, BotInventoryItem> expected, string action) =>
			Require(expected.OrderBy(pair => pair.Key).SequenceEqual(driver.Api.World.Inventory.OrderBy(pair => pair.Key)), $"Unexpected inventory delta after {action}.");
	}

	private static void Consume(Dictionary<int, BotInventoryItem> items, BotInventoryItem item)
	{
		if (item.Count == 1) items.Remove(item.ObjectId);
		else items[item.ObjectId] = item with { Count = item.Count - 1 };
	}
	private static void Require(bool condition, string message)
	{
		if (!condition) throw new InvalidDataException(message);
	}
}
