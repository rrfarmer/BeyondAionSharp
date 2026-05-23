using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcCombatEventServiceTests
{
	[Fact]
	public void NotifyAttackedObservers_RecordsAttackerAndSkill()
	{
		var service = new WorldNpcCombatEventService();

		var notification = service.NotifyAttackedObservers(
			npcObjectId: 1,
			attackerObjectId: 1001,
			skillId: 1234);

		Assert.Equal(1, notification.NpcObjectId);
		Assert.Equal(1001, notification.AttackerObjectId);
		Assert.Equal(1234, notification.SkillId);
		Assert.True(notification.Sequence > 0);
		Assert.True(service.TryGetState(1, out var state));
		Assert.Equal(notification, Assert.Single(state!.AttackedObserverNotifications));
		Assert.Empty(state.SupportAiRequests);
	}

	[Fact]
	public void NotifyNearbySupportAi_RecordsVisibleNpcSupportRequests()
	{
		var service = new WorldNpcCombatEventService();
		var attacked = CreateNpc(1, 203090, new WorldPosition(210010000, 10, 10, 10, 0));
		var nearby = CreateNpc(2, 203091, new WorldPosition(210010000, 25, 10, 10, 0));
		var farAway = CreateNpc(3, 203092, new WorldPosition(210010000, 200, 10, 10, 0));
		var otherWorld = CreateNpc(4, 203093, new WorldPosition(220010000, 25, 10, 10, 0));

		var requests = service.NotifyNearbySupportAi(attacked, [attacked, nearby, farAway, otherWorld]);

		var request = Assert.Single(requests);
		Assert.Equal(2, request.SupportNpcObjectId);
		Assert.Equal(1, request.AttackedNpcObjectId);
		Assert.Equal(WorldNpcAiEventType.CreatureNeedsSupport, request.EventType);
		Assert.True(request.Sequence > 0);
		Assert.True(service.TryGetState(1, out var state));
		Assert.Equal(request, Assert.Single(state!.SupportAiRequests));
		Assert.Empty(state.AttackedObserverNotifications);
	}

	[Fact]
	public void Clear_RemovesNpcCombatEventState()
	{
		var service = new WorldNpcCombatEventService();
		var attacked = CreateNpc(1, 203090, new WorldPosition(210010000, 10, 10, 10, 0));
		var nearby = CreateNpc(2, 203091, new WorldPosition(210010000, 25, 10, 10, 0));

		service.NotifyAttackedObservers(1, 1001, skillId: 42);
		service.NotifyNearbySupportAi(attacked, [nearby]);
		service.Clear(1);

		Assert.False(service.TryGetState(1, out _));
	}

	private static WorldNpc CreateNpc(int objectId, int templateId, WorldPosition position)
	{
		return new WorldNpc(
			objectId,
			templateId,
			new NpcTemplateSummary(
				templateId,
				$"npc-{templateId}",
				NameId: templateId,
				Level: 10,
				Rank: "NORMAL",
				Rating: "NORMAL",
				Race: "ELYOS",
				Tribe: "GENERAL",
				Type: "GENERAL",
				MaxHp: 100),
			position);
	}
}
