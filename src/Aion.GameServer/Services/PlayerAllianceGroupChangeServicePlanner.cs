using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerAllianceGroupChangeServicePlanner(PlayerAllianceRuntime runtime)
{
	public PlayerAllianceGroupChangeServicePlan CreateChangeMemberGroupPlan(
		Player caller,
		int firstMemberObjectId,
		int secondMemberObjectId,
		int targetAllianceGroupId)
	{
		// Java parity: model/team/alliance/PlayerAllianceService.changeMemberGroup validates alliance membership and captain rights before dispatching ChangeMemberGroupEvent.
		var alliance = runtime.Resolve(caller);
		if (alliance == null)
		{
			return new PlayerAllianceGroupChangeServicePlan(
				AllianceId: 0,
				caller.ObjectId,
				PlayerAllianceGroupChangeServicePlanStatus.NotAllianceMember,
				GroupChangePlan: null,
				new PlayerAllianceSystemMessageIntent(caller.ObjectId, SmSystemMessage.ForceYouAreNotForceMember()));
		}

		if (!runtime.IsLeader(alliance.AllianceId, caller) && !runtime.IsViceCaptain(alliance.AllianceId, caller.ObjectId))
		{
			return new PlayerAllianceGroupChangeServicePlan(
				alliance.AllianceId,
				caller.ObjectId,
				PlayerAllianceGroupChangeServicePlanStatus.NotAuthorized,
				GroupChangePlan: null,
				new PlayerAllianceSystemMessageIntent(caller.ObjectId, SmSystemMessage.ForceRightNotHave()));
		}

		var groupChangePlan = runtime.ChangeMemberGroup(
			alliance.AllianceId,
			firstMemberObjectId,
			secondMemberObjectId,
			targetAllianceGroupId);

		return new PlayerAllianceGroupChangeServicePlan(
			alliance.AllianceId,
			caller.ObjectId,
			groupChangePlan == null
				? PlayerAllianceGroupChangeServicePlanStatus.EventSkipped
				: PlayerAllianceGroupChangeServicePlanStatus.Dispatched,
			groupChangePlan,
			SystemMessageIntent: null);
	}
}

public enum PlayerAllianceGroupChangeServicePlanStatus
{
	Dispatched,
	EventSkipped,
	NotAllianceMember,
	NotAuthorized,
}

public sealed record PlayerAllianceGroupChangeServicePlan(
	int AllianceId,
	int CallerObjectId,
	PlayerAllianceGroupChangeServicePlanStatus Status,
	PlayerAllianceMemberGroupChangePlan? GroupChangePlan,
	PlayerAllianceSystemMessageIntent? SystemMessageIntent);
