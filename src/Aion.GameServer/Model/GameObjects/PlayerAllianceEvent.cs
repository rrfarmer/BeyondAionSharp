namespace Aion.GameServer.Model.GameObjects;

// Java parity: model/team/common/legacy/PlayerAllianceEvent.
// Java keeps each constant a distinct identity (ordinal, declaration order) with a separate repeatable
// id field used on the wire. The C# enum therefore uses sequential ordinal values (so switch labels stay
// distinct) and exposes the wire id via GetId(). Declaration order matches Java for ordinal parity.
public enum PlayerAllianceEvent
{
	LEAVE,
	BANNED,
	MOVEMENT,
	DISCONNECTED,
	JOIN,
	ENTER_OFFLINE,
	UPDATE_EFFECTS,
	RECONNECT,
	ENTER,
	UPDATE,
	MEMBER_GROUP_CHANGE,
	APPOINT_VICE_CAPTAIN,
	DEMOTE_VICE_CAPTAIN,
	APPOINT_CAPTAIN,
}

public static class PlayerAllianceEventExtensions
{
	// Java parity: PlayerAllianceEvent.getId() - the wire id (repeats across constants).
	public static int GetId(this PlayerAllianceEvent e) => e switch
	{
		PlayerAllianceEvent.LEAVE => 0,
		PlayerAllianceEvent.BANNED => 0,
		PlayerAllianceEvent.MOVEMENT => 1,
		PlayerAllianceEvent.DISCONNECTED => 3,
		PlayerAllianceEvent.JOIN => 5,
		PlayerAllianceEvent.ENTER_OFFLINE => 7,
		PlayerAllianceEvent.UPDATE_EFFECTS => 65,
		PlayerAllianceEvent.RECONNECT => 13,
		PlayerAllianceEvent.ENTER => 13,
		PlayerAllianceEvent.UPDATE => 13,
		PlayerAllianceEvent.MEMBER_GROUP_CHANGE => 5,
		PlayerAllianceEvent.APPOINT_VICE_CAPTAIN => 13,
		PlayerAllianceEvent.DEMOTE_VICE_CAPTAIN => 13,
		PlayerAllianceEvent.APPOINT_CAPTAIN => 13,
		_ => 0,
	};
}
