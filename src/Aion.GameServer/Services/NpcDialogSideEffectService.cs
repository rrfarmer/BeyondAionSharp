using Aion.GameServer.Model.GameObjects;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public static class NpcDialogSideEffectService
{
	public static NpcDialogSideEffectResult ApplyShowDialogSideEffects(
		Player player,
		int targetObjectId,
		GameWorld? world,
		Func<Player, int, bool>? isKnownNpc = null)
	{
		// Java parity: network/aion/clientpackets/CM_SHOW_DIALOG.runImpl stops blinking before the trading guard.
		var protectionStopped = player.StopProtectionActive();
		var hideEffectsRemoved = false;

		if (!player.IsTrading
			&& (isKnownNpc?.Invoke(player, targetObjectId) ?? true)
			&& world?.TryGetObject(targetObjectId, out var target) == true
			&& target is IWorldNpcObject npc
			&& !npc.Template.CanTalkInvisible
			&& player.IsInAnyHide())
		{
			// Java parity: TalkInfo.canTalkInvisible false triggers EffectController.removeHideEffects.
			hideEffectsRemoved = player.RemoveHideEffects();
		}

		return new NpcDialogSideEffectResult(protectionStopped, hideEffectsRemoved);
	}
}

public sealed record NpcDialogSideEffectResult(
	bool ProtectionStopped,
	bool HideEffectsRemoved)
{
	public bool PlayerStateChanged => ProtectionStopped || HideEffectsRemoved;
}
