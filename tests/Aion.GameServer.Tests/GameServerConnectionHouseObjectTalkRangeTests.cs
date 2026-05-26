using System.Reflection;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionHouseObjectTalkRangeTests
{
	[Theory]
	[InlineData(3.24f, true)]
	[InlineData(3.25f, false)]
	public void TryCreateHouseObjectUseTarget_UsesJavaHouseObjectTalkRange(float xOffset, bool expected)
	{
		var player = new Player
		{
			ObjectId = 1001,
			Name = "player",
			Position = new WorldPosition(710010000, 0, 0, 0, 0),
		};
		var houseObject = new RegisteredHouseObjectSummary(
			ObjectId: 5001,
			TemplateId: 3001000,
			X: xOffset,
			Y: 0,
			Z: 0);
		var house = new WorldHouse(
			ObjectId: 7001,
			AddressId: 101,
			BuildingId: 9001,
			OwnerObjectId: player.ObjectId,
			OwnerName: player.Name,
			LegionId: 0,
			LegionName: string.Empty,
			LegionEmblemId: 0,
			LegionEmblemType: 0,
			LegionEmblemColorA: 0,
			LegionEmblemColorR: 0,
			LegionEmblemColorG: 0,
			LegionEmblemColorB: 0,
			IsInactive: false,
			DoorState: PlayerHouse.DoorClosed,
			ShowOwnerName: true,
			SignNotice: null,
			Position: new WorldPosition(710010000, 0, 0, 0, 0),
			Registry: new HouseRegistrySummary([houseObject], []));
		var templates = new HousingObjectTemplateTable(
		[
			new HousingObjectTemplateSummary(
				TemplateId: 3001000,
				TypeId: 1,
				Kind: "use_item",
				Area: "INTERIOR",
				Location: "FLOOR",
				Limit: "NONE",
				Category: "DECORATION",
				UseDays: 0,
				CanDye: false,
				TalkingDistance: 2),
		]);

		var target = InvokeTryCreateHouseObjectUseTarget(player, house, houseObject.ObjectId, templates);

		Assert.NotNull(target);
		Assert.Equal(expected, GetIsInTalkRange(target));
	}

	private static object? InvokeTryCreateHouseObjectUseTarget(
		Player player,
		WorldHouse house,
		int objectId,
		HousingObjectTemplateTable templates)
	{
		var method = typeof(GameServerConnection).GetMethod(
			"TryCreateHouseObjectUseTarget",
			BindingFlags.Static | BindingFlags.NonPublic);
		Assert.NotNull(method);

		var args = new object?[] { player, house, objectId, templates, null };
		var created = (bool)method.Invoke(null, args)!;
		Assert.True(created);
		return args[4];
	}

	private static bool GetIsInTalkRange(object target)
	{
		var property = target.GetType().GetProperty("IsInTalkRange");
		Assert.NotNull(property);
		return (bool)property.GetValue(target)!;
	}
}
