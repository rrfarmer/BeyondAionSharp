using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public static class NpcDialogRequestService
{
	public static NpcDialogRequestResult RequestDialog(
		Player player,
		int targetObjectId,
		GameWorld? world,
		Func<Player, int, bool>? isKnownNpc = null)
	{
		// Java parity: controllers/NpcController.onDialogRequest after CM_SHOW_DIALOG known-list dispatch.
		if (world == null
			|| (isKnownNpc?.Invoke(player, targetObjectId) == false)
			|| !world.TryGetObject(targetObjectId, out var target)
			|| target is not IWorldNpcObject npc)
		{
			return NpcDialogRequestResult.NotHandled(NpcDialogRequestStatus.UnknownTarget);
		}

		if (!npc.Template.CanInteract)
			return NpcDialogRequestResult.NotHandled(NpcDialogRequestStatus.NotInteractable);

		if (!IsInTalkRange(player, npc))
		{
			var packet = npc.Template.IsDialogNpc
				? SmSystemMessage.DialogTooFarToTalk()
				: SmSystemMessage.WarehouseTooFarFromNpc();
			return NpcDialogRequestResult.WithPacket(NpcDialogRequestStatus.TooFar, packet);
		}

		return NpcDialogRequestResult.CreateHandled(NpcDialogRequestStatus.DialogStarted);
	}

	private static bool IsInTalkRange(Player player, IWorldNpcObject npc)
	{
		// Java parity: utils/PositionUtil.isInTalkRange(Creature, Npc) uses talkDistance + 1 and object radii.
		if (player.Position.WorldId != npc.Position.WorldId || player.Position.InstanceId != npc.Position.InstanceId)
			return false;

		var range = npc.Template.TalkDistance + 1 + npc.Template.BoundRadius;
		var deltaX = player.Position.X - npc.Position.X;
		var deltaY = player.Position.Y - npc.Position.Y;
		var deltaZ = player.Position.Z - npc.Position.Z;
		return deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ < range * range;
	}
}

public sealed record NpcDialogRequestResult(
	bool Handled,
	NpcDialogRequestStatus Status,
	GameServerPacket? ResponsePacket)
{
	public static NpcDialogRequestResult CreateHandled(NpcDialogRequestStatus status)
	{
		return new NpcDialogRequestResult(true, status, null);
	}

	public static NpcDialogRequestResult WithPacket(NpcDialogRequestStatus status, GameServerPacket packet)
	{
		return new NpcDialogRequestResult(true, status, packet);
	}

	public static NpcDialogRequestResult NotHandled(NpcDialogRequestStatus status)
	{
		return new NpcDialogRequestResult(false, status, null);
	}
}

public enum NpcDialogRequestStatus
{
	DialogStarted,
	TooFar,
	UnknownTarget,
	NotInteractable,
}
