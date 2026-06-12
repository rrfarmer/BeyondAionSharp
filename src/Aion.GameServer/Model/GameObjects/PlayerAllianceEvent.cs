namespace Aion.GameServer.Model.GameObjects;

public enum PlayerAllianceEvent
{
	// Java parity: model/team/common/legacy/PlayerAllianceEvent.
	LEAVE = 0,
	BANNED = 0,
	MOVEMENT = 1,
	DISCONNECTED = 3,
	JOIN = 5,
	MEMBER_GROUP_CHANGE = 5,
	ENTER_OFFLINE = 7,
	UPDATE_EFFECTS = 65,
	RECONNECT = 13,
	ENTER = 13,
	UPDATE = 13,
	APPOINT_VICE_CAPTAIN = 13,
	DEMOTE_VICE_CAPTAIN = 13,
	APPOINT_CAPTAIN = 13,
}
