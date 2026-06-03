using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class NpcDialogSideEffectServiceTests
{
	[Fact]
	public void ApplyShowDialogSideEffects_StopsProtectionBeforeTradingGuard()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var player = new Player
		{
			IsTrading = true,
			VisualState = PlayerVisualStates.Blinking | PlayerVisualStates.Hide1,
			AbnormalState = PlayerAbnormalState.Hide,
		};
		world.TryAddObject(5001, CreateNpc(canTalkInvisible: false));

		var result = NpcDialogSideEffectService.ApplyShowDialogSideEffects(player, 5001, world);

		Assert.True(result.ProtectionStopped);
		Assert.False(result.HideEffectsRemoved);
		Assert.True(result.PlayerStateChanged);
		Assert.Equal(PlayerVisualStates.Hide1, player.VisualState);
		Assert.True(player.IsAbnormalSet(PlayerAbnormalState.Hide));
	}

	[Fact]
	public void ApplyShowDialogSideEffects_RemovesHideForKnownNpcThatCannotTalkInvisible()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var player = new Player
		{
			VisualState = PlayerVisualStates.Blinking | PlayerVisualStates.Hide1,
			AbnormalState = PlayerAbnormalState.Hide | PlayerAbnormalState.Root,
		};
		world.TryAddObject(5001, CreateNpc(canTalkInvisible: false));

		var result = NpcDialogSideEffectService.ApplyShowDialogSideEffects(player, 5001, world);

		Assert.True(result.ProtectionStopped);
		Assert.True(result.HideEffectsRemoved);
		Assert.True(result.PlayerStateChanged);
		Assert.Equal(PlayerVisualStates.Visible, player.VisualState);
		Assert.True(player.IsAbnormalSet(PlayerAbnormalState.Root));
		Assert.False(player.IsAbnormalSet(PlayerAbnormalState.Hide));
	}

	[Fact]
	public void ApplyShowDialogSideEffects_KeepsHideForNpcThatCanTalkInvisible()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var player = new Player
		{
			VisualState = PlayerVisualStates.Hide2,
			AbnormalState = PlayerAbnormalState.Hide,
		};
		world.TryAddObject(5001, CreateNpc(canTalkInvisible: true));

		var result = NpcDialogSideEffectService.ApplyShowDialogSideEffects(player, 5001, world);

		Assert.False(result.ProtectionStopped);
		Assert.False(result.HideEffectsRemoved);
		Assert.False(result.PlayerStateChanged);
		Assert.Equal(PlayerVisualStates.Hide2, player.VisualState);
		Assert.True(player.IsAbnormalSet(PlayerAbnormalState.Hide));
	}

	[Fact]
	public void ApplyShowDialogSideEffects_KeepsHideForNpcOutsideKnownList()
	{
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var player = new Player
		{
			VisualState = PlayerVisualStates.Hide2,
			AbnormalState = PlayerAbnormalState.Hide,
		};
		world.TryAddObject(5001, CreateNpc(canTalkInvisible: false));

		var result = NpcDialogSideEffectService.ApplyShowDialogSideEffects(
			player,
			5001,
			world,
			isKnownNpc: (_, _) => false);

		Assert.False(result.ProtectionStopped);
		Assert.False(result.HideEffectsRemoved);
		Assert.Equal(PlayerVisualStates.Hide2, player.VisualState);
		Assert.True(player.IsAbnormalSet(PlayerAbnormalState.Hide));
	}

	[Fact]
	public void CloseDialogPlan_PlansMailboxCloseAndAiEventForNpcTargetWithOpenMailbox()
	{
		var planner = new NpcDialogCloseSideEffectPlanService();
		var player = new Player { MailboxState = PlayerMailboxState.Regular };

		var plan = planner.CreatePlan(player, targetObjectId: 5001, isNpcTarget: true);

		Assert.Equal(5001, plan.TargetObjectId);
		Assert.True(plan.IsNpcTarget);
		Assert.True(plan.WouldFireDialogFinishAiEvent);
		Assert.True(plan.WouldCloseMailbox);
		Assert.False(plan.WouldReleaseLegionWarehouseLock);
		Assert.False(plan.ShouldMutateLiveAiState);
		Assert.False(plan.ShouldMutateLiveMailboxState);
		Assert.False(plan.ShouldMutateLiveLegionWarehouse);
		Assert.Contains("DialogService.onCloseDialog", plan.JavaSource);
	}

	[Fact]
	public void CloseDialogPlan_SkipsMailboxCloseWhenAlreadyClosedAndSkipsAiEventForNonNpcTarget()
	{
		var planner = new NpcDialogCloseSideEffectPlanService();
		var playerClosedMailbox = new Player { MailboxState = PlayerMailboxState.Closed };
		var playerWithMailbox = new Player { MailboxState = PlayerMailboxState.Express };

		var noNpc = planner.CreatePlan(playerWithMailbox, targetObjectId: 0, isNpcTarget: false);
		var closedMailbox = planner.CreatePlan(playerClosedMailbox, targetObjectId: 5001, isNpcTarget: true);

		// Non-NPC target: no AI event, but mailbox still closes
		Assert.False(noNpc.IsNpcTarget);
		Assert.False(noNpc.WouldFireDialogFinishAiEvent);
		Assert.True(noNpc.WouldCloseMailbox);
		Assert.False(noNpc.ShouldMutateLiveAiState);
		Assert.False(noNpc.ShouldMutateLiveMailboxState);
		// Already-closed mailbox: mailbox does not close
		Assert.True(closedMailbox.IsNpcTarget);
		Assert.True(closedMailbox.WouldFireDialogFinishAiEvent);
		Assert.False(closedMailbox.WouldCloseMailbox);
	}

	private static WorldNpc CreateNpc(bool canTalkInvisible)
	{
		var template = new NpcTemplateSummary(
			TemplateId: 3001,
			Name: "Dialog NPC",
			NameId: 0,
			Level: 1,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "NONE",
			Tribe: "GENERAL",
			Type: "NPC",
			CanTalkInvisible: canTalkInvisible);

		return new WorldNpc(
			ObjectId: 5001,
			TemplateId: template.TemplateId,
			Template: template,
			Position: new WorldPosition(210010000, 0, 0, 0, 0));
	}
}
