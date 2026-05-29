using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SmShieldEffectPacketTests
{
	[Fact]
	public void SmShieldEffect_WritesCountAndLocationsInJavaOrder()
	{
		var packet = new SmShieldEffect(new[]
		{
			new ShieldEffectLocationSnapshot(LocationId: 3011, IsUnderShield: true),
			new ShieldEffectLocationSnapshot(LocationId: 3021, IsUnderShield: false),
		});

		AssertSmShieldEffectPayload(packet, new[]
		{
			new ShieldEffectLocationSnapshot(LocationId: 3011, IsUnderShield: true),
			new ShieldEffectLocationSnapshot(LocationId: 3021, IsUnderShield: false),
		});
	}

	[Fact]
	public void CreateSendToPlayerPlan_PreservesWorldLocationOrderLikeJavaCollectionIteration()
	{
		var locations = new[]
		{
			new ShieldEffectLocationSnapshot(LocationId: 3021, IsUnderShield: false),
			new ShieldEffectLocationSnapshot(LocationId: 3011, IsUnderShield: true),
		};

		var plan = ShieldEffectPacketPlanService.CreateSendToPlayerPlan(locations);

		Assert.Equal(ShieldEffectPacketPlanStatus.PacketCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldSendToPlayer);
		Assert.False(plan.ShouldBroadcastToMap);
		Assert.NotNull(plan.Packet);
		Assert.Contains("worldLocations.values", plan.JavaSource);
		AssertSmShieldEffectPayload(plan.Packet!, locations);
		Assert.Equal(locations, plan.Locations);
	}

	[Fact]
	public void CreateMapBroadcastPlan_CreatesSingleLocationBroadcastLikeShieldNpcAI()
	{
		var location = new ShieldEffectLocationSnapshot(LocationId: 1211, IsUnderShield: true);

		var plan = ShieldEffectPacketPlanService.CreateMapBroadcastPlan(location);

		Assert.Equal(ShieldEffectPacketPlanStatus.PacketCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.False(plan.ShouldSendToPlayer);
		Assert.True(plan.ShouldBroadcastToMap);
		Assert.NotNull(plan.Packet);
		Assert.Contains("broadcastToMap", plan.JavaSource);
		AssertSmShieldEffectPayload(plan.Packet!, new[] { location });
	}

	[Fact]
	public void CreateSendToPlayerPlan_AllowsEmptyLocationCollectionLikeJavaWriteImpl()
	{
		var plan = ShieldEffectPacketPlanService.CreateSendToPlayerPlan(Array.Empty<ShieldEffectLocationSnapshot>());

		Assert.Equal(ShieldEffectPacketPlanStatus.PacketCreated, plan.Status);
		Assert.True(plan.ShouldSendToPlayer);
		Assert.False(plan.ShouldBroadcastToMap);
		Assert.NotNull(plan.Packet);
		AssertSmShieldEffectPayload(plan.Packet!, Array.Empty<ShieldEffectLocationSnapshot>());
	}

	[Fact]
	public void CreateMapBroadcastPlan_BlocksInvalidLocationBeforePacketCreation()
	{
		var plan = ShieldEffectPacketPlanService.CreateMapBroadcastPlan(new ShieldEffectLocationSnapshot(LocationId: 0, IsUnderShield: true));

		Assert.Equal(ShieldEffectPacketPlanStatus.BlockedInvalidLocation, plan.Status);
		Assert.False(plan.ShouldSendToPlayer);
		Assert.False(plan.ShouldBroadcastToMap);
		Assert.Null(plan.Packet);
	}

	private static void AssertSmShieldEffectPayload(SmShieldEffect packet, IReadOnlyList<ShieldEffectLocationSnapshot> locations)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmShieldEffect.PacketOpCode, packet.OpCode);
		Assert.Equal(locations.Count, reader.ReadH());
		foreach (var location in locations)
		{
			Assert.Equal(location.LocationId, reader.ReadD());
			Assert.Equal(location.IsUnderShield ? 1 : 0, reader.ReadC());
		}

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
