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

// Java parity: services/player/PlayerMailboxState
public static class PlayerMailboxState
{
	public const byte Closed = 0x00;
	public const byte Regular = 0x01;
	public const byte Express = 0x02;
}

public sealed class NpcDialogCloseSideEffectPlanService
{
	public NpcDialogCloseSideEffectPlan CreatePlan(
		Player player,
		int targetObjectId,
		bool isNpcTarget,
		bool npcSupportsLegionWarehouse = false,
		bool playerIsLegionMember = false)
	{
		ArgumentNullException.ThrowIfNull(player);

		// Java parity: DialogService.onCloseDialog closes an open mailbox unconditionally.
		var wouldCloseMailbox = player.MailboxState != PlayerMailboxState.Closed;
		// Java parity: npc.getAi().onCreatureEvent(AIEventType.DIALOG_FINISH, player) fires when target is an NPC.
		var wouldFireDialogFinishAiEvent = isNpcTarget;
		// Java parity: OPEN_LEGION_WAREHOUSE unsetInUse when player is a legion member and NPC supports action.
		// C# does not yet model LegionWarehouse.unsetInUse or Player.isLegionMember(); this flag documents the intent.
		var wouldReleaseLegionWarehouseLock = isNpcTarget && npcSupportsLegionWarehouse && playerIsLegionMember;

		return new NpcDialogCloseSideEffectPlan(
			targetObjectId,
			isNpcTarget,
			wouldFireDialogFinishAiEvent,
			wouldCloseMailbox,
			wouldReleaseLegionWarehouseLock,
			JavaSource: "services/DialogService.onCloseDialog");
	}
}

public sealed record NpcDialogCloseSideEffectPlan(
	int TargetObjectId,
	bool IsNpcTarget,
	bool WouldFireDialogFinishAiEvent,
	bool WouldCloseMailbox,
	bool WouldReleaseLegionWarehouseLock,
	string JavaSource)
{
	public bool ShouldMutateLiveAiState => false;
	public bool ShouldMutateLiveMailboxState => false;
	public bool ShouldMutateLiveLegionWarehouse => false;
}
