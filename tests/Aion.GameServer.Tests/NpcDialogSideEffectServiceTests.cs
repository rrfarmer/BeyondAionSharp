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
	public void ItemTemplateSummary_ItemMaskPropertiesMatchJavaItemMaskConstants()
	{
		// Java parity: model/items/ItemMask constants; each bit maps to a boolean property.
		// Note: allMask has SOUL_BOUND set (1<<7), so AWH/LWH storable return false (soulbound guard).
		var allMask = CreateMinimalItemTemplate(mask: ~0); // all bits set (including SOUL_BOUND)
		var noMask = CreateMinimalItemTemplate(mask: 0);
		// AWH/LWH storable without soulbound
		var awhMask = CreateMinimalItemTemplate(mask: (1 << 4)); // STORABLE_IN_AWH only, not soulbound
		var lwhMask = CreateMinimalItemTemplate(mask: (1 << 5)); // STORABLE_IN_LWH only, not soulbound
		var awhSoulBound = CreateMinimalItemTemplate(mask: (1 << 4) | (1 << 7)); // AWH + SOUL_BOUND

		// Existing masks (unaffected by soulbound guard)
		Assert.True(allMask.IsTradeable);      // 1 << 1
		Assert.True(allMask.IsBreakable);      // 1 << 6
		Assert.True(allMask.IsSoulBound);      // 1 << 7
		Assert.True(allMask.IsNoEnchant);      // 1 << 9
		Assert.True(allMask.IsRemodelable);    // 1 << 12
		Assert.True(allMask.CanPolish);        // 1 << 17
		Assert.True(allMask.CanApExtract);     // 1 << 16
		Assert.True(allMask.CanSocketGodstone); // 1 << 10
		Assert.True(allMask.IsItemDyePermitted); // 1 << 15
		// Simple mask bits
		Assert.True(allMask.IsSellable);       // 1 << 2
		Assert.True(allMask.IsStorableInWarehouse); // 1 << 3 (no soulbound guard)
		Assert.True(allMask.IsRemovedOnLogout);     // 1 << 8
		Assert.True(allMask.CanCompositeWeapon);    // 1 << 11
		Assert.True(allMask.CanSplit);              // 1 << 13
		Assert.True(allMask.IsDeletable);           // 1 << 14
		Assert.True(allMask.IsLegionTradeable);     // 1 << 18
		// AWH/LWH storable: mask bit AND !soulBound (Java parity: Item.isStorableInAccWarehouse/isStorableInLegWarehouse)
		Assert.True(awhMask.IsStorableInAccountWarehouse);   // bit set, not soulbound → true
		Assert.True(lwhMask.IsStorableInLegionWarehouse);    // bit set, not soulbound → true
		Assert.False(awhSoulBound.IsStorableInAccountWarehouse); // bit set BUT soulbound → false
		Assert.False(allMask.IsStorableInAccountWarehouse);  // bit set BUT allMask has SOUL_BOUND → false
		Assert.False(allMask.IsStorableInLegionWarehouse);   // same
		// All false for zero mask
		Assert.False(noMask.IsBreakable);
		Assert.False(noMask.IsSellable);
		Assert.False(noMask.IsStorableInWarehouse);
		Assert.False(noMask.IsStorableInAccountWarehouse);
		Assert.False(noMask.IsStorableInLegionWarehouse);
		Assert.False(noMask.IsRemovedOnLogout);
		Assert.False(noMask.IsDeletable);
		Assert.False(noMask.IsLegionTradeable);
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

	private static ItemTemplateSummary CreateMinimalItemTemplate(int mask)
	{
		// Java parity: ItemMask bits are read from the parsed integer mask on the item template.
		return new ItemTemplateSummary(
			100001,
			"test_item",
			0,
			mask,
			1,
			"ETC",
			"ELSE",
			"COMMON",
			"PC_ALL",
			1,
			0,
			1);
	}
}
