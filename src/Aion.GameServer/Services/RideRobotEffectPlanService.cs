using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum RideRobotEffectPlanStatus
{
	StartPlanned,
	EndPlanned,
	BlockedInvalidPlayer,
	BlockedMissingWeaponRobot,
}

public sealed record RideRobotEffectPlan(
	RideRobotEffectPlanStatus Status,
	int PlayerObjectId,
	int? WeaponSkinRobotId,
	int RobotIdToSet,
	bool ShouldSetPlayerRobotId,
	bool ShouldBroadcastRobotPacket,
	RideRobotPacketPlan? PacketPlan,
	bool ShouldAddUnequipObserver,
	string? ObserverTypeName,
	string? ObserverEquipmentTypeName,
	bool ShouldEndRideRobotConditionEffects,
	string JavaSource)
{
	public bool IsLive => false;
	public SmRideRobot? Packet => PacketPlan?.Packet;
}

public static class RideRobotEffectPlanService
{
	public static RideRobotEffectPlan CreateStartPlan(int playerObjectId, int weaponSkinRobotId)
	{
		// Java parity breadcrumb: RideRobotEffect.startEffect sets Player.robotId from
		// the main-hand weapon skin robot id, broadcasts SM_RIDE_ROBOT, then installs an
		// UNEQUIP observer that ends the effect when a weapon is unequipped.
		if (playerObjectId <= 0)
			return Blocked(RideRobotEffectPlanStatus.BlockedInvalidPlayer, playerObjectId, weaponSkinRobotId, "RideRobotEffect.startEffect requires a live Player");

		if (weaponSkinRobotId <= 0)
			return Blocked(RideRobotEffectPlanStatus.BlockedMissingWeaponRobot, playerObjectId, weaponSkinRobotId, "RideRobotEffect.startEffect requires a weapon skin robot id");

		var packetPlan = RideRobotPacketPlanService.CreateBroadcastReceivePlan(new RideRobotSnapshot(playerObjectId, weaponSkinRobotId));
		return new RideRobotEffectPlan(
			RideRobotEffectPlanStatus.StartPlanned,
			playerObjectId,
			weaponSkinRobotId,
			RobotIdToSet: weaponSkinRobotId,
			ShouldSetPlayerRobotId: true,
			ShouldBroadcastRobotPacket: packetPlan.Status == RideRobotPacketPlanStatus.PacketCreated,
			packetPlan,
			ShouldAddUnequipObserver: true,
			ObserverTypeName: "UNEQUIP",
			ObserverEquipmentTypeName: "WEAPON",
			ShouldEndRideRobotConditionEffects: false,
			"RideRobotEffect.startEffect");
	}

	public static RideRobotEffectPlan CreateEndPlan(int playerObjectId)
	{
		// Java parity breadcrumb: RideRobotEffect.endEffect resets Player.robotId to 0,
		// broadcasts SM_RIDE_ROBOT, then ends all abnormal effects with RideRobotCondition.
		if (playerObjectId <= 0)
			return Blocked(RideRobotEffectPlanStatus.BlockedInvalidPlayer, playerObjectId, weaponSkinRobotId: null, "RideRobotEffect.endEffect requires a live Player");

		var packetPlan = RideRobotPacketPlanService.CreateBroadcastReceivePlan(new RideRobotSnapshot(playerObjectId, RobotId: 0));
		return new RideRobotEffectPlan(
			RideRobotEffectPlanStatus.EndPlanned,
			playerObjectId,
			WeaponSkinRobotId: null,
			RobotIdToSet: 0,
			ShouldSetPlayerRobotId: true,
			ShouldBroadcastRobotPacket: packetPlan.Status == RideRobotPacketPlanStatus.PacketCreated,
			packetPlan,
			ShouldAddUnequipObserver: false,
			ObserverTypeName: null,
			ObserverEquipmentTypeName: null,
			ShouldEndRideRobotConditionEffects: true,
			"RideRobotEffect.endEffect");
	}

	private static RideRobotEffectPlan Blocked(
		RideRobotEffectPlanStatus status,
		int playerObjectId,
		int? weaponSkinRobotId,
		string javaSource)
	{
		return new RideRobotEffectPlan(
			status,
			playerObjectId,
			weaponSkinRobotId,
			RobotIdToSet: 0,
			ShouldSetPlayerRobotId: false,
			ShouldBroadcastRobotPacket: false,
			PacketPlan: null,
			ShouldAddUnequipObserver: false,
			ObserverTypeName: null,
			ObserverEquipmentTypeName: null,
			ShouldEndRideRobotConditionEffects: false,
			javaSource);
	}
}
