using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class PlayerAllianceMovementUpdatePlanner
{
	public PlayerAllianceMemberInfoUpdatePlan? CreateMovementUpdatePlan(
		int allianceId,
		IReadOnlyList<Player> members,
		Player player)
	{
		// Java parity: model/team/alliance/events/PlayerAllianceUpdateEvent handles MOVEMENT via Predicates.Players.allExcept(player).
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(allianceId, 0);

		var subject = members.FirstOrDefault(member => member.ObjectId == player.ObjectId);
		if (subject == null)
			return null;

		var packetPlan = PlayerAllianceMemberInfoPacketPlan.FromPlayer(allianceId, subject, PlayerAllianceEvent.Movement);
		var intents = members
			.Where(member => member.ObjectId != player.ObjectId)
			.Select(member => new PlayerAllianceMemberInfoIntent(
				member.ObjectId,
				player.ObjectId,
				PlayerAllianceEvent.Movement,
				packetPlan))
			.ToArray();

		return new PlayerAllianceMemberInfoUpdatePlan(
			allianceId,
			player.ObjectId,
			PlayerAllianceEvent.Movement,
			Slot: 0,
			intents);
	}
}
