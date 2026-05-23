namespace Aion.GameServer.Model.GameObjects;

public enum PlayerAllianceEvent
{
	// Java parity: model/team/common/legacy/PlayerAllianceEvent.
	Leave = 0,
	Banned = 0,
	Movement = 1,
	Disconnected = 3,
	Join = 5,
	MemberGroupChange = 5,
	EnterOffline = 7,
	UpdateEffects = 65,
	Reconnect = 13,
	Enter = 13,
	Update = 13,
	AppointViceCaptain = 13,
	DemoteViceCaptain = 13,
	AppointCaptain = 13,
}
