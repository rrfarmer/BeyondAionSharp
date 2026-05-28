using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerTargetChangePacketPlanServiceTests
{
	[Fact]
	public void CreatePlan_CreatesOwnerAndSightedPacketsWhenTargetChangesToCreatureLikeJavaController()
	{
		var target = new TargetSelectedSnapshot(
			TargetObjectId: 7002,
			Level: 55,
			MaxHp: 12000,
			CurrentHp: 9876,
			MaxMp: 4500,
			CurrentMp: 3210);

		var plan = PlayerTargetChangePacketPlanService.CreatePlan(
			playerObjectId: 1001,
			currentTargetObjectId: 0,
			target);

		Assert.Equal(PlayerTargetChangePacketPlanStatus.PacketsCreated, plan.Status);
		Assert.True(plan.ShouldUpdatePlayerTargetObjectId);
		Assert.True(plan.ShouldSendOwnerPacket);
		Assert.True(plan.ShouldBroadcastToSightedPlayers);
		Assert.Equal(0, plan.PreviousTargetObjectId);
		Assert.Equal(7002, plan.NewTargetObjectId);
		Assert.NotNull(plan.OwnerPacket);
		Assert.NotNull(plan.SightedPlayersPacket);
		Assert.Contains("PlayerController.onTargetChanged", plan.JavaSource);
		AssertTargetSelectedPayload(plan.OwnerPacket, target);
		AssertTargetUpdatePayload(plan.SightedPlayersPacket, playerObjectId: 1001, targetObjectId: 7002);
	}

	[Fact]
	public void CreatePlan_CreatesClearTargetPacketsWhenTargetChangesToNullLikeJavaController()
	{
		var plan = PlayerTargetChangePacketPlanService.CreatePlan(
			playerObjectId: 1001,
			currentTargetObjectId: 7002,
			newTarget: null);

		Assert.Equal(PlayerTargetChangePacketPlanStatus.PacketsCreated, plan.Status);
		Assert.True(plan.ShouldUpdatePlayerTargetObjectId);
		Assert.Equal(7002, plan.PreviousTargetObjectId);
		Assert.Equal(0, plan.NewTargetObjectId);
		Assert.NotNull(plan.OwnerPacket);
		Assert.NotNull(plan.SightedPlayersPacket);
		AssertTargetSelectedPayload(plan.OwnerPacket, TargetSelectedSnapshot.Empty);
		AssertTargetUpdatePayload(plan.SightedPlayersPacket, playerObjectId: 1001, targetObjectId: 0);
	}

	[Fact]
	public void CreatePlan_DoesNotCreatePacketsWhenTargetIdIsUnchangedLikeVisibleObjectGuard()
	{
		var plan = PlayerTargetChangePacketPlanService.CreatePlan(
			playerObjectId: 1001,
			currentTargetObjectId: 7002,
			TargetSelectedSnapshot.VisibleObject(7002));

		Assert.Equal(PlayerTargetChangePacketPlanStatus.NoChange, plan.Status);
		Assert.False(plan.ShouldUpdatePlayerTargetObjectId);
		Assert.False(plan.ShouldSendOwnerPacket);
		Assert.False(plan.ShouldBroadcastToSightedPlayers);
		Assert.Null(plan.OwnerPacket);
		Assert.Null(plan.SightedPlayersPacket);
		Assert.Contains("VisibleObject.setTarget", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_CreatesPlanFromPlayerCurrentTargetObjectId()
	{
		var player = new Player
		{
			ObjectId = 1001,
			TargetObjectId = 7001,
		};

		var plan = PlayerTargetChangePacketPlanService.CreatePlan(player, TargetSelectedSnapshot.VisibleObject(7002));

		Assert.Equal(PlayerTargetChangePacketPlanStatus.PacketsCreated, plan.Status);
		Assert.Equal(7001, plan.PreviousTargetObjectId);
		Assert.Equal(7002, plan.NewTargetObjectId);
		AssertTargetUpdatePayload(plan.SightedPlayersPacket!, playerObjectId: 1001, targetObjectId: 7002);
	}

	[Fact]
	public void CreatePlan_BlocksPacketsWhenPlayerOwnerIsUnavailable()
	{
		var plan = PlayerTargetChangePacketPlanService.CreatePlan(
			playerObjectId: 0,
			currentTargetObjectId: 0,
			TargetSelectedSnapshot.VisibleObject(7002));

		Assert.Equal(PlayerTargetChangePacketPlanStatus.BlockedInvalidPlayer, plan.Status);
		Assert.False(plan.ShouldUpdatePlayerTargetObjectId);
		Assert.False(plan.ShouldSendOwnerPacket);
		Assert.False(plan.ShouldBroadcastToSightedPlayers);
		Assert.Null(plan.OwnerPacket);
		Assert.Null(plan.SightedPlayersPacket);
	}

	private static void AssertTargetSelectedPayload(SmTargetSelected packet, TargetSelectedSnapshot expected)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmTargetSelected.PacketOpCode, packet.OpCode);
		Assert.Equal(expected.TargetObjectId, reader.ReadD());
		Assert.Equal(expected.Level, reader.ReadH());
		Assert.Equal(expected.MaxHp, reader.ReadD());
		Assert.Equal(expected.CurrentHp, reader.ReadD());
		Assert.Equal(expected.MaxMp, reader.ReadD());
		Assert.Equal(expected.CurrentMp, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertTargetUpdatePayload(SmTargetUpdate packet, int playerObjectId, int targetObjectId)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmTargetUpdate.PacketOpCode, packet.OpCode);
		Assert.Equal(playerObjectId, reader.ReadD());
		Assert.Equal(targetObjectId, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
