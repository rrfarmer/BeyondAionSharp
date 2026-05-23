using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public static class PlayerKiskDialogService
{
	public static PlayerKiskDialogResult RequestDialog(
		Player player,
		int targetObjectId,
		GameWorld? world,
		PlayerKiskRegistry registry,
		Func<Player, int, bool>? isKnownNpc = null)
	{
		// Java parity: data/handlers/ai/KiskAI.handleDialogStart after NpcController.onDialogRequest range/interact checks.
		var kisk = registry.GetKiskState(targetObjectId);
		if (kisk == null)
			return PlayerKiskDialogResult.NotHandled(PlayerKiskDialogStatus.NotKisk);

		var npcDialog = NpcDialogRequestService.RequestDialog(player, targetObjectId, world, isKnownNpc);
		if (npcDialog.ResponsePacket != null)
			return PlayerKiskDialogResult.WithPacket(PlayerKiskDialogStatus.TooFar, npcDialog.ResponsePacket);
		if (!npcDialog.Handled || npcDialog.Status != NpcDialogRequestStatus.DialogStarted)
			return PlayerKiskDialogResult.CreateHandled(MapNpcDialogStatus(npcDialog.Status));

		var authorization = PlayerKiskAuthorizationService.ValidateBind(player, kisk);
		switch (authorization.Status)
		{
			case PlayerKiskBindAuthorizationStatus.AlreadyRegistered:
				return PlayerKiskDialogResult.WithPacket(
					PlayerKiskDialogStatus.AlreadyRegistered,
					SmSystemMessage.BindstoneAlreadyRegistered());
			case PlayerKiskBindAuthorizationStatus.Full:
				return PlayerKiskDialogResult.WithPacket(
					PlayerKiskDialogStatus.Full,
					SmSystemMessage.CannotRegisterBindstoneFull());
			case PlayerKiskBindAuthorizationStatus.NoAuthority:
				return PlayerKiskDialogResult.WithPacket(
					PlayerKiskDialogStatus.NoAuthority,
					SmSystemMessage.CannotRegisterBindstoneHaveNoAuthority());
		}

		if (player.PendingKiskBindRequest != null)
			return PlayerKiskDialogResult.CreateHandled(PlayerKiskDialogStatus.PendingRequest);

		player.PendingKiskBindRequest = new PendingKiskBindRequest(kisk.ObjectId, SmQuestionWindow.RegisterBindstone);
		return PlayerKiskDialogResult.WithPacket(
			PlayerKiskDialogStatus.QuestionRequested,
			new SmQuestionWindow(SmQuestionWindow.RegisterBindstone, kisk.ObjectId, rangeOrCooldownSeconds: 5));
	}

	private static PlayerKiskDialogStatus MapNpcDialogStatus(NpcDialogRequestStatus status)
	{
		return status switch
		{
			NpcDialogRequestStatus.TooFar => PlayerKiskDialogStatus.TooFar,
			NpcDialogRequestStatus.UnknownTarget => PlayerKiskDialogStatus.UnknownTarget,
			NpcDialogRequestStatus.NotInteractable => PlayerKiskDialogStatus.NotInteractable,
			_ => PlayerKiskDialogStatus.UnknownTarget,
		};
	}
}

public sealed record PlayerKiskDialogResult(
	bool Handled,
	PlayerKiskDialogStatus Status,
	GameServerPacket? ResponsePacket)
{
	public static PlayerKiskDialogResult NotHandled(PlayerKiskDialogStatus status)
	{
		return new PlayerKiskDialogResult(false, status, null);
	}

	public static PlayerKiskDialogResult CreateHandled(PlayerKiskDialogStatus status)
	{
		return new PlayerKiskDialogResult(true, status, null);
	}

	public static PlayerKiskDialogResult WithPacket(PlayerKiskDialogStatus status, GameServerPacket packet)
	{
		return new PlayerKiskDialogResult(true, status, packet);
	}
}

public enum PlayerKiskDialogStatus
{
	NotKisk,
	QuestionRequested,
	PendingRequest,
	AlreadyRegistered,
	Full,
	NoAuthority,
	TooFar,
	UnknownTarget,
	NotInteractable,
}
