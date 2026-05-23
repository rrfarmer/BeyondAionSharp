namespace Aion.GameServer.Model.GameObjects;

public enum PlayerGroupEvent
{
	// Java parity: model/team/common/legacy/GroupEvent.LEAVE.
	Leave = 0,

	// Java parity: model/team/common/legacy/GroupEvent.MOVEMENT.
	Movement = 1,

	// Java parity: model/team/common/legacy/GroupEvent.DISCONNECTED.
	Disconnected = 3,

	// Java parity: model/team/common/legacy/GroupEvent.JOIN.
	Join = 5,

	// Java parity: model/team/common/legacy/GroupEvent.ENTER_OFFLINE.
	EnterOffline = 7,

	// Java parity: model/team/common/legacy/GroupEvent.ENTER and UPDATE share id 13.
	Enter = 13,

	// Java parity: model/team/common/legacy/GroupEvent.UPDATE and ENTER share id 13.
	Update = 13,

	// Java parity: model/team/common/legacy/GroupEvent.UPDATE_EFFECTS.
	UpdateEffects = 65,
}
