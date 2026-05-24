using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public class PlayerSummonCastSpellServiceTests
{
	[Fact]
	public void Handle_ConsumesMatchingQueuedPetOrderForRepresentedSummon()
	{
		var player = CreatePlayer();
		player.SetSummonKnownObject(7001, PlayerSummonKnownObjectKind.Creature);
		player.AddPetSkillOrder(new PlayerPetSkillOrder(22107, SkillLevel: 1, TargetObjectId: 7001, Hate: 5, Release: true));
		var packet = CreatePacket(summonObjectId: 8001, skillId: 22107, skillLevel: 1, targetObjectId: 7001);

		var result = new PlayerSummonCastSpellService().Handle(player, packet);

		Assert.Equal(PlayerSummonCastSpellStatus.Executed, result.Status);
		Assert.False(result.SkillMismatch);
		Assert.Null(result.Warning);
		Assert.Equal(7001, result.ResolvedTarget?.ObjectId);
		Assert.Equal(PlayerSummonKnownObjectKind.Creature, result.ResolvedTarget?.Kind);
		Assert.False(result.ResolvedTarget?.IsActorSelfTarget);
		var order = Assert.IsType<PlayerPetSkillOrder>(result.ExecutedOrder);
		Assert.Equal(22107, order.SkillId);
		Assert.Equal(1, order.SkillLevel);
		Assert.Equal(7001, order.TargetObjectId);
		Assert.Equal(5, order.Hate);
		Assert.True(order.Release);
		Assert.Empty(player.PetSkillOrders);
	}

	[Fact]
	public void Handle_UsesQueuedOrderWhenClientSkillDiffersAndMarksMismatch()
	{
		var player = CreatePlayer();
		player.SetSummonKnownObject(7001, PlayerSummonKnownObjectKind.Creature);
		player.AddPetSkillOrder(new PlayerPetSkillOrder(22107, SkillLevel: 1, TargetObjectId: 7001, Hate: 0, Release: false));
		var packet = CreatePacket(summonObjectId: 8001, skillId: 9999, skillLevel: 3, targetObjectId: 7001);

		var result = new PlayerSummonCastSpellService().Handle(player, packet);

		Assert.Equal(PlayerSummonCastSpellStatus.Executed, result.Status);
		Assert.True(result.SkillMismatch);
		Assert.Equal(22107, result.ExecutedOrder?.SkillId);
		Assert.Equal(1, result.ExecutedOrder?.SkillLevel);
		var warning = Assert.IsType<PlayerSummonCastSpellWarning>(result.Warning);
		Assert.Equal(PlayerSummonCastSpellWarningKind.SkillMismatch, warning.Kind);
		Assert.Equal(9999, warning.PacketSkillId);
		Assert.Equal(3, warning.PacketSkillLevel);
		Assert.Equal(22107, warning.QueuedSkillId);
		Assert.Equal(1, warning.QueuedSkillLevel);
		Assert.Empty(player.PetSkillOrders);
	}

	[Fact]
	public void Handle_ConsumesQueuedOrderWithoutExecutionWhenTargetDoesNotMatch()
	{
		var player = CreatePlayer();
		player.SetSummonKnownObject(7002, PlayerSummonKnownObjectKind.Creature);
		player.AddPetSkillOrder(new PlayerPetSkillOrder(22107, SkillLevel: 1, TargetObjectId: 7001, Hate: 0, Release: false));
		var packet = CreatePacket(summonObjectId: 8001, skillId: 22107, skillLevel: 1, targetObjectId: 7002);

		var result = new PlayerSummonCastSpellService().Handle(player, packet);

		Assert.Equal(PlayerSummonCastSpellStatus.TargetMismatch, result.Status);
		Assert.Equal(7002, result.TargetObjectId);
		Assert.Equal(7002, result.ResolvedTarget?.ObjectId);
		Assert.False(result.ResolvedTarget?.IsActorSelfTarget);
		Assert.Equal(7001, result.ExecutedOrder?.TargetObjectId);
		Assert.Null(result.Warning);
		var skippedExecution = Assert.IsType<PlayerSummonCastSpellSkippedExecution>(result.SkippedExecution);
		Assert.Equal(PlayerSummonCastSpellSkippedExecutionKind.TargetMismatch, skippedExecution.Kind);
		Assert.Equal(7001, skippedExecution.QueuedTargetObjectId);
		Assert.Equal(7002, skippedExecution.PacketTargetObjectId);
		Assert.Empty(player.PetSkillOrders);
	}

	[Fact]
	public void Handle_AllowsSummonSelfTargetWithoutKnownListLookup()
	{
		var player = CreatePlayer();
		player.AddPetSkillOrder(new PlayerPetSkillOrder(22107, SkillLevel: 1, TargetObjectId: 8001, Hate: 0, Release: false));
		var packet = CreatePacket(summonObjectId: 8001, skillId: 22107, skillLevel: 1, targetObjectId: 8001);

		var result = new PlayerSummonCastSpellService().Handle(player, packet);

		Assert.Equal(PlayerSummonCastSpellStatus.Executed, result.Status);
		Assert.Equal(8001, result.TargetObjectId);
		Assert.Equal(8001, result.ResolvedTarget?.ObjectId);
		Assert.True(result.ResolvedTarget?.IsActorSelfTarget);
		Assert.Empty(player.PetSkillOrders);
	}

	[Fact]
	public void Handle_UnknownOrNonCreatureKnownTargetReturnsBeforeConsumingOrder()
	{
		var service = new PlayerSummonCastSpellService();
		var unknownTargetPlayer = CreatePlayer();
		unknownTargetPlayer.AddPetSkillOrder(new PlayerPetSkillOrder(22107, SkillLevel: 1, TargetObjectId: 7001, Hate: 0, Release: false));

		var unknownTarget = service.Handle(
			unknownTargetPlayer,
			CreatePacket(summonObjectId: 8001, skillId: 22107, skillLevel: 1, targetObjectId: 7001));

		var nonCreatureTargetPlayer = CreatePlayer();
		nonCreatureTargetPlayer.SetSummonKnownObject(7001, PlayerSummonKnownObjectKind.VisibleObject);
		nonCreatureTargetPlayer.AddPetSkillOrder(new PlayerPetSkillOrder(22107, SkillLevel: 1, TargetObjectId: 7001, Hate: 0, Release: false));

		var nonCreatureTarget = service.Handle(
			nonCreatureTargetPlayer,
			CreatePacket(summonObjectId: 8001, skillId: 22107, skillLevel: 1, targetObjectId: 7001));

		Assert.Equal(PlayerSummonCastSpellStatus.UnknownTarget, unknownTarget.Status);
		Assert.Equal(7001, unknownTarget.TargetObjectId);
		Assert.Null(unknownTarget.Audit);
		Assert.Single(unknownTargetPlayer.PetSkillOrders);
		Assert.Equal(PlayerSummonCastSpellStatus.NonCreatureTarget, nonCreatureTarget.Status);
		Assert.Equal(7001, nonCreatureTarget.TargetObjectId);
		var audit = Assert.IsType<PlayerSummonCastSpellAudit>(nonCreatureTarget.Audit);
		Assert.Equal(PlayerSummonCastSpellAuditKind.WrongTarget, audit.Kind);
		Assert.Equal(7001, audit.TargetObjectId);
		Assert.Equal(PlayerSummonKnownObjectKind.VisibleObject, audit.TargetKind);
		Assert.Single(nonCreatureTargetPlayer.PetSkillOrders);
	}

	[Fact]
	public void Handle_RequiresRepresentedPetSummonAndQueuedOrder()
	{
		var service = new PlayerSummonCastSpellService();
		var noSummon = service.Handle(new Player(), CreatePacket(8001, 22107, 1, 7001));

		var player = CreatePlayer();
		var noOrder = service.Handle(player, CreatePacket(8001, 22107, 1, 8001));

		var wrongSummon = CreatePlayer();
		wrongSummon.AddPetSkillOrder(new PlayerPetSkillOrder(22107, SkillLevel: 1, TargetObjectId: 7001, Hate: 0, Release: false));
		var wrongSummonResult = service.Handle(wrongSummon, CreatePacket(8002, 22107, 1, 7001));

		Assert.Equal(PlayerSummonCastSpellStatus.PetRequired, noSummon.Status);
		Assert.Equal(PlayerSummonOrMercenaryKind.None, noSummon.ActorKind);
		Assert.Equal(PlayerSummonCastSpellStatus.NoQueuedOrder, noOrder.Status);
		Assert.Equal(PlayerSummonOrMercenaryKind.PetSummon, noOrder.ActorKind);
		Assert.Equal(PlayerSummonCastSpellStatus.PetRequired, wrongSummonResult.Status);
		Assert.Equal(PlayerSummonOrMercenaryKind.None, wrongSummonResult.ActorKind);
		Assert.Single(wrongSummon.PetSkillOrders);
	}

	[Fact]
	public void Handle_DistinguishesRepresentedNonPetSummonAndMercenary()
	{
		var service = new PlayerSummonCastSpellService();
		var nonPetSummon = new Player
		{
			RepresentedSummonOrMercenaryObjectId = 8001,
			RepresentedSummonOrMercenaryKind = PlayerSummonOrMercenaryKind.NonPetSummon,
		};
		nonPetSummon.AddPetSkillOrder(new PlayerPetSkillOrder(22107, SkillLevel: 1, TargetObjectId: 7001, Hate: 0, Release: false));

		var nonPetResult = service.Handle(
			nonPetSummon,
			CreatePacket(summonObjectId: 8001, skillId: 22107, skillLevel: 1, targetObjectId: 7001));

		var mercenary = new Player
		{
			RepresentedSummonOrMercenaryObjectId = 8002,
			RepresentedSummonOrMercenaryKind = PlayerSummonOrMercenaryKind.Mercenary,
		};

		var mercenaryResult = service.Handle(
			mercenary,
			CreatePacket(summonObjectId: 8002, skillId: 22107, skillLevel: 1, targetObjectId: 8002));

		Assert.Equal(PlayerSummonCastSpellStatus.PetRequired, nonPetResult.Status);
		Assert.Equal(PlayerSummonOrMercenaryKind.NonPetSummon, nonPetResult.ActorKind);
		Assert.Single(nonPetSummon.PetSkillOrders);
		Assert.Equal(PlayerSummonCastSpellStatus.MercenaryReady, mercenaryResult.Status);
		Assert.Equal(PlayerSummonOrMercenaryKind.Mercenary, mercenaryResult.ActorKind);
		Assert.Equal(8002, mercenaryResult.TargetObjectId);
	}

	[Fact]
	public void Handle_RecognizesCreatorOwnedKnownMercenaryMetadata()
	{
		var service = new PlayerSummonCastSpellService();
		var player = new Player { ObjectId = 1 };
		player.SetSummonKnownObject(new PlayerSummonKnownObject(
			ObjectId: 8002,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary));

		var mercenaryResult = service.Handle(
			player,
			CreatePacket(summonObjectId: 8002, skillId: 22107, skillLevel: 1, targetObjectId: 8002));

		var wrongCreator = new Player { ObjectId = 1 };
		wrongCreator.SetSummonKnownObject(new PlayerSummonKnownObject(
			ObjectId: 8003,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 2,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary));

		var wrongCreatorResult = service.Handle(
			wrongCreator,
			CreatePacket(summonObjectId: 8003, skillId: 22107, skillLevel: 1, targetObjectId: 8003));

		Assert.Equal(PlayerSummonCastSpellStatus.MercenaryReady, mercenaryResult.Status);
		Assert.Equal(PlayerSummonOrMercenaryKind.Mercenary, mercenaryResult.ActorKind);
		Assert.Equal(833288, player.GetSummonOrMercenaryNpcId(8002));
		Assert.Equal(PlayerSummonCastSpellStatus.PetRequired, wrongCreatorResult.Status);
		Assert.Equal(PlayerSummonOrMercenaryKind.None, wrongCreatorResult.ActorKind);
		Assert.Equal(0, wrongCreator.GetSummonOrMercenaryNpcId(8003));
	}

	private static Player CreatePlayer()
	{
		return new Player
		{
			HasPetSummon = true,
			PetSummonObjectId = 8001,
			PetSummonNpcId = 833288,
		};
	}

	private static CmSummonCastSpell CreatePacket(int summonObjectId, int skillId, int skillLevel, int targetObjectId)
	{
		var packet = new CmSummonCastSpell(205, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteD(summonObjectId);
		buffer.WriteH(skillId);
		buffer.WriteC(skillLevel);
		buffer.WriteD(targetObjectId);
		buffer.WriteD(0);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}
}
