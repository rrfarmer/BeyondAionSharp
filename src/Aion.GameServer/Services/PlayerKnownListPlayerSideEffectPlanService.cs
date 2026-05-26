using Aion.GameServer.Model;

namespace Aion.GameServer.Services;

public enum PlayerKnownListPlayerSideEffectTransition
{
	See,
	NotSee,
}

public enum PlayerKnownListPlayerSideEffectStatus
{
	Planned,
	SkippedViewerNotSpawned,
}

public enum PlayerKnownListPlayerSideEffectKind
{
	SmPlayerInfo,
	SmMotion,
	SmEmotionRide,
	SmPlayerStance,
	SmAbnormalEffect,
	SmDelete,
}

public enum PlayerKnownListPlayerSideEffectCSharpSupport
{
	Available,
	Partial,
	Missing,
}

public sealed record PlayerKnownListPlayerSeeSideEffectContext(
	int ViewerPlayerObjectId,
	int SeenPlayerObjectId,
	bool ViewerAggroIconToSeen = false,
	bool SeenIsInRideMode = false,
	int? SeenRideNpcId = null,
	bool SeenIsUnderStance = false,
	bool SeenHasAbnormalEffects = false);

public sealed record PlayerKnownListPlayerNotSeeSideEffectContext(
	int ViewerPlayerObjectId,
	int LostPlayerObjectId,
	ObjectDeleteAnimation Animation = ObjectDeleteAnimation.FadeOut,
	bool ViewerIsSpawned = true);

public sealed record PlayerKnownListPlayerSideEffectDescriptor(
	PlayerKnownListPlayerSideEffectKind Kind,
	string JavaPacketName,
	string? CSharpPacketTypeName,
	PlayerKnownListPlayerSideEffectCSharpSupport CSharpSupport,
	int ViewerPlayerObjectId,
	int SubjectPlayerObjectId,
	string JavaSource,
	bool AggroIcon = false,
	int? RideNpcId = null,
	int? StanceState = null,
	ObjectDeleteAnimation? DeleteAnimation = null,
	string Notes = "");

public sealed record PlayerKnownListPlayerSideEffectPlan(
	PlayerKnownListPlayerSideEffectTransition Transition,
	PlayerKnownListPlayerSideEffectStatus Status,
	int ViewerPlayerObjectId,
	int SubjectPlayerObjectId,
	IReadOnlyList<PlayerKnownListPlayerSideEffectDescriptor> Descriptors,
	bool ExecutesLivePackets,
	bool IsJavaControllerParity,
	bool IsLive,
	string JavaSource);

public sealed class PlayerKnownListPlayerSideEffectPlanService
{
	public PlayerKnownListPlayerSideEffectPlan PlanSee(PlayerKnownListPlayerSeeSideEffectContext context)
	{
		// Java parity breadcrumb: PlayerController.see(Player) delegates to
		// sendPlayerInfoPackets(Player) after CreatureController.see.
		var aggroIcon = context.ViewerPlayerObjectId != context.SeenPlayerObjectId && context.ViewerAggroIconToSeen;
		var descriptors = new List<PlayerKnownListPlayerSideEffectDescriptor>
		{
			new(
				PlayerKnownListPlayerSideEffectKind.SmPlayerInfo,
				"SM_PLAYER_INFO",
				"SmPlayerInfo",
				PlayerKnownListPlayerSideEffectCSharpSupport.Partial,
				context.ViewerPlayerObjectId,
				context.SeenPlayerObjectId,
				"com.aionemu.gameserver.controllers.PlayerController.sendPlayerInfoPackets -> new SM_PLAYER_INFO(player, !player.equals(getOwner()) && getOwner().isAggroIconTo(player))",
				AggroIcon: aggroIcon,
				Notes: "C# SmPlayerInfo can carry Java's enemy/aggro creature-type flag; viewer-sensitive race projection still needs verification."),
			new(
				PlayerKnownListPlayerSideEffectKind.SmMotion,
				"SM_MOTION",
				"SmMotion",
				PlayerKnownListPlayerSideEffectCSharpSupport.Available,
				context.ViewerPlayerObjectId,
				context.SeenPlayerObjectId,
				"com.aionemu.gameserver.controllers.PlayerController.sendPlayerInfoPackets -> new SM_MOTION(player.getObjectId(), player.getMotions().getActiveMotions())"),
		};

		if (context.SeenIsInRideMode)
		{
			descriptors.Add(new PlayerKnownListPlayerSideEffectDescriptor(
				PlayerKnownListPlayerSideEffectKind.SmEmotionRide,
				"SM_EMOTION",
				"SmEmotion",
				PlayerKnownListPlayerSideEffectCSharpSupport.Available,
				context.ViewerPlayerObjectId,
				context.SeenPlayerObjectId,
				"com.aionemu.gameserver.controllers.PlayerController.sendPlayerInfoPackets -> new SM_EMOTION(player, EmotionType.RIDE, 0, player.ride.getNpcId())",
				RideNpcId: context.SeenRideNpcId,
				Notes: context.SeenRideNpcId is null
					? "Ride mode was supplied without a ride NPC id; Java reads player.ride.getNpcId()."
					: ""));
		}

		if (context.SeenIsUnderStance)
		{
			descriptors.Add(new PlayerKnownListPlayerSideEffectDescriptor(
				PlayerKnownListPlayerSideEffectKind.SmPlayerStance,
				"SM_PLAYER_STANCE",
				"SmPlayerStance",
				PlayerKnownListPlayerSideEffectCSharpSupport.Available,
				context.ViewerPlayerObjectId,
				context.SeenPlayerObjectId,
				"com.aionemu.gameserver.controllers.PlayerController.sendPlayerInfoPackets -> new SM_PLAYER_STANCE(player, 1)",
				StanceState: 1,
				Notes: "C# packet serializer exists; descriptor remains non-live and does not instantiate or send packets."));
		}

		if (context.SeenHasAbnormalEffects)
		{
			descriptors.Add(new PlayerKnownListPlayerSideEffectDescriptor(
				PlayerKnownListPlayerSideEffectKind.SmAbnormalEffect,
				"SM_ABNORMAL_EFFECT",
				"SmAbnormalEffect",
				PlayerKnownListPlayerSideEffectCSharpSupport.Partial,
				context.ViewerPlayerObjectId,
				context.SeenPlayerObjectId,
				"com.aionemu.gameserver.controllers.PlayerController.see -> if (!creature.getEffectController().isEmpty()) new SM_ABNORMAL_EFFECT(creature)",
				Notes: "C# packet serializer exists for supplied effect facts; descriptor does not yet hydrate live EffectController data or send packets."));
		}

		return CreatePlan(
			PlayerKnownListPlayerSideEffectTransition.See,
			PlayerKnownListPlayerSideEffectStatus.Planned,
			context.ViewerPlayerObjectId,
			context.SeenPlayerObjectId,
			descriptors,
			"Descriptor planner for com.aionemu.gameserver.controllers.PlayerController.see(Player); does not send packets.");
	}

	public PlayerKnownListPlayerSideEffectPlan PlanNotSee(PlayerKnownListPlayerNotSeeSideEffectContext context)
	{
		// Java parity breadcrumb: PlayerController.notSee(Player) calls super.notSee,
		// then skips deletion packets while the owner is teleporting/unspawned.
		if (!context.ViewerIsSpawned)
		{
			return CreatePlan(
				PlayerKnownListPlayerSideEffectTransition.NotSee,
				PlayerKnownListPlayerSideEffectStatus.SkippedViewerNotSpawned,
				context.ViewerPlayerObjectId,
				context.LostPlayerObjectId,
				[],
				"Descriptor planner for com.aionemu.gameserver.controllers.PlayerController.notSee(Player); viewer unspawned branch sends no delete packet.");
		}

		return CreatePlan(
			PlayerKnownListPlayerSideEffectTransition.NotSee,
			PlayerKnownListPlayerSideEffectStatus.Planned,
			context.ViewerPlayerObjectId,
			context.LostPlayerObjectId,
			[
				new PlayerKnownListPlayerSideEffectDescriptor(
					PlayerKnownListPlayerSideEffectKind.SmDelete,
					"SM_DELETE",
					"SmDelete",
					PlayerKnownListPlayerSideEffectCSharpSupport.Available,
					context.ViewerPlayerObjectId,
					context.LostPlayerObjectId,
					"com.aionemu.gameserver.controllers.PlayerController.notSee -> fallback new SM_DELETE(object, animation)",
					DeleteAnimation: context.Animation),
			],
			"Descriptor planner for com.aionemu.gameserver.controllers.PlayerController.notSee(Player); does not send packets.");
	}

	private static PlayerKnownListPlayerSideEffectPlan CreatePlan(
		PlayerKnownListPlayerSideEffectTransition transition,
		PlayerKnownListPlayerSideEffectStatus status,
		int viewerPlayerObjectId,
		int subjectPlayerObjectId,
		IReadOnlyList<PlayerKnownListPlayerSideEffectDescriptor> descriptors,
		string javaSource) =>
		new(
			transition,
			status,
			viewerPlayerObjectId,
			subjectPlayerObjectId,
			descriptors,
			ExecutesLivePackets: false,
			IsJavaControllerParity: false,
			IsLive: false,
			javaSource);
}
