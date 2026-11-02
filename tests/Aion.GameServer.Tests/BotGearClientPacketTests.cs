using Aion.Bots.Protocol;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed partial class BotGameClientPacketWriterTests
{
	private static IEnumerable<object[]> GearPacketCases(AionConnection.State game)
	{
		yield return C("unwrap-item", GameClientPackets.UnwrapItem(123), game,
			new Dictionary<string, object?> { ["objectId"] = 123 });
		// Upstream d254ea172 reads the object id as one 64-bit value, so the parser no longer has the second word as
		// its own field; the writer's two 32-bit words are the same bytes and stay as they are.
		yield return C("select-decomposable", GameClientPackets.SelectDecomposable(123, 255), game,
			new Dictionary<string, object?> { ["objectId"] = 123, ["index"] = 255 });
		yield return C("purify-five-materials", GameClientPackets.PurifyItem(1, 2, 3, 4, 5, 6, 7, 8), game,
			new Dictionary<string, object?> { ["playerObjectId"] = 1, ["upgradedItemObjectId"] = 2, ["resultItemId"] = 3,
				["requireItemObjectId1"] = 4, ["requireItemObjectId2"] = 5, ["requireItemObjectId3"] = 6, ["requireItemObjectId4"] = 7, ["requireItemObjectId5"] = 8 });
		yield return C("purify-unused-materials", GameClientPackets.PurifyItem(1, 2, 3, 4), game,
			new Dictionary<string, object?> { ["requireItemObjectId1"] = 4, ["requireItemObjectId2"] = 0, ["requireItemObjectId5"] = 0 });
		yield return C("remodel", GameClientPackets.RemodelItem(1, 2, 3), game,
			new Dictionary<string, object?> { ["keepItemId"] = 2, ["extractItemId"] = 3 });
		yield return C("identify", GameClientPackets.TuneItem(1), game,
			new Dictionary<string, object?> { ["itemObjectId"] = 1, ["tuningScrollObjectId"] = 0 });
		yield return C("retune", GameClientPackets.TuneItem(1, 2), game,
			new Dictionary<string, object?> { ["itemObjectId"] = 1, ["tuningScrollObjectId"] = 2 });
		foreach (bool accepted in new[] { false, true })
			yield return C("tune-result-" + accepted, GameClientPackets.TuneResult(1, accepted), game,
				new Dictionary<string, object?> { ["itemObjectId"] = 1, ["hasAccepted"] = accepted });
		yield return C("charge-items", GameClientPackets.ChargeItems(1, 2, 3, 4), game,
			new Dictionary<string, object?> { ["targetNpcObjectId"] = 1, ["chargeLevel"] = 2, ["itemObjectIds"] = new List<int> { 3, 4 } });
		yield return C("fuse-weapons", GameClientPackets.FuseWeapons(123, 456, 789), game,
			new Dictionary<string, object?> { ["npcObjId"] = 123, ["mainWeaponObjId"] = 456, ["fuseWeaponObjId"] = 789 });
		yield return C("break-weapons", GameClientPackets.BreakWeapons(123, 456), game,
			new Dictionary<string, object?> { ["npcObjId"] = 123, ["weaponObjId"] = 456 });
		yield return C("enchant-item", GameClientPackets.EnchantItem(123, 456, 789), game,
			new Dictionary<string, object?> { ["action"] = ManastoneAction(1), ["targetFusedSlot"] = 1,
				["targetItemUniqueId"] = 123, ["stoneUniqueId"] = 456, ["supplementUniqueId"] = 789 });
		foreach (bool fusion in new[] { false, true })
		{
			yield return C("socket-manastone-" + fusion, GameClientPackets.SocketManastone(123, 456, fusion, 789), game,
				new Dictionary<string, object?> { ["action"] = ManastoneAction(2), ["targetFusedSlot"] = fusion ? 2 : 1,
					["targetItemUniqueId"] = 123, ["stoneUniqueId"] = 456, ["supplementUniqueId"] = 789 });
			yield return C("remove-manastone-" + fusion, GameClientPackets.RemoveManastone(789, 123, 255, fusion), game,
				new Dictionary<string, object?> { ["action"] = ManastoneAction(3), ["targetFusedSlot"] = fusion ? 2 : 1,
					["targetItemUniqueId"] = 123, ["slotNum"] = 255, ["npcObjId"] = 789 });
		}
		yield return C("socket-godstone-inventory", GameClientPackets.SocketGodstone(123, 456), game,
			new Dictionary<string, object?> { ["action"] = ManastoneAction(4), ["targetFusedSlot"] = 1,
				["targetItemUniqueId"] = 123, ["stoneUniqueId"] = 456, ["supplementUniqueId"] = 0 });
	}

	private static object ManastoneAction(int id) => Enum.ToObject(typeof(CM_MANASTONE).GetNestedType("Action", System.Reflection.BindingFlags.NonPublic)!, id);

	[Fact]
	public void GearActionBodiesMatchAuditedJavaFieldOrderAndReservedBytes()
	{
		Assert.Equal("44332211", Convert.ToHexString(GameClientPackets.UnwrapItem(0x11223344).Body));
		Assert.Equal("4433221100000000FF", Convert.ToHexString(GameClientPackets.SelectDecomposable(0x11223344, 255).Body));
		Assert.Equal("0100000002000000030000000400000000000000000000000000000000000000", Convert.ToHexString(GameClientPackets.PurifyItem(1, 2, 3, 4).Body));
		Assert.Equal("01000000020000000300000000000000", Convert.ToHexString(GameClientPackets.RemodelItem(1, 2, 3).Body));
		Assert.Equal("0100000002000000", Convert.ToHexString(GameClientPackets.TuneItem(1, 2).Body));
		Assert.Equal("0100000001", Convert.ToHexString(GameClientPackets.TuneResult(1, true).Body));
		Assert.Equal("010000000202000300000004000000", Convert.ToHexString(GameClientPackets.ChargeItems(1, 2, 3, 4).Body));
		Assert.Throws<ArgumentException>(() => GameClientPackets.PurifyItem(1, 2, 3, 4, 5, 6, 7, 8, 9));
		Assert.Equal("44332211887766550D0C0B0A",
			Convert.ToHexString(GameClientPackets.FuseWeapons(0x11223344, 0x55667788, 0x0A0B0C0D).Body));
		Assert.Equal("4433221188776655",
			Convert.ToHexString(GameClientPackets.BreakWeapons(0x11223344, 0x55667788).Body));
		// CM_MANASTONE readImpl at ce54b7931. Pin bytes independently of the C# reader.
		Assert.Equal("010144332211887766550D0C0B0A",
			Convert.ToHexString(GameClientPackets.EnchantItem(0x11223344, 0x55667788, 0x0A0B0C0D).Body));
		Assert.Equal("030188776655FF00000044332211",
			Convert.ToHexString(GameClientPackets.RemoveManastone(0x11223344, 0x55667788, 255).Body));
		Assert.Equal("0401443322118877665500000000",
			Convert.ToHexString(GameClientPackets.SocketGodstone(0x11223344, 0x55667788).Body));
	}

	[Fact]
	public void GodstoneUsesRegisteredManastoneRouteNotRetiredNpcPacket()
	{
		// Both 4.8 factories retire CM_GODSTONE_SOCKET: godstones now use action 4, without an NPC.
		var packet = GameClientPackets.SocketGodstone(123, 456);
		Assert.Equal(typeof(CM_MANASTONE), packet.PacketType);
		Assert.Equal(74, GamePacketRegistry.Instance.GetClient(packet.PacketType).Opcode);
		Assert.Throws<KeyNotFoundException>(() => GamePacketRegistry.Instance.GetClient(typeof(CM_GODSTONE_SOCKET)));
	}
}
