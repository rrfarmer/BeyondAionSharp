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
	public void InventoryItemExtensions_ComposeTemplateMaskWithInstanceSoulboundLikeJavaItem()
	{
		// Java parity: Item.isTradeable/isStorableInAccWarehouse/isStorableInLegWarehouse/isLegionTradeable
		// each combine template mask bit AND !item.isSoulBound() (runtime instance state).
		var tradeableTemplate = CreateMinimalItemTemplate(mask: (1 << 1)); // TRADEABLE
		var awhTemplate = CreateMinimalItemTemplate(mask: (1 << 4));       // STORABLE_IN_AWH
		var lwhTemplate = CreateMinimalItemTemplate(mask: (1 << 5));       // STORABLE_IN_LWH
		var legionTemplate = CreateMinimalItemTemplate(mask: (1 << 18));   // LEGION_TRADEABLE
		var notSoulBound = CreateInventoryItem(isSoulBound: false);
		var soulBound = CreateInventoryItem(isSoulBound: true);

		// Not soul-bound instance: template mask determines outcome
		Assert.True(notSoulBound.IsTradeable(tradeableTemplate));
		Assert.True(notSoulBound.IsStorableInAccountWarehouse(awhTemplate));
		Assert.True(notSoulBound.IsStorableInLegionWarehouse(lwhTemplate));
		Assert.True(notSoulBound.IsLegionTradeable(legionTemplate));
		// Soul-bound instance: always false regardless of template mask
		Assert.False(soulBound.IsTradeable(tradeableTemplate));
		Assert.False(soulBound.IsStorableInAccountWarehouse(awhTemplate));
		Assert.False(soulBound.IsStorableInLegionWarehouse(lwhTemplate));
		Assert.False(soulBound.IsLegionTradeable(legionTemplate));
		// Template mask false: always false regardless of instance soulbound
		var noMaskTemplate = CreateMinimalItemTemplate(mask: 0);
		Assert.False(notSoulBound.IsTradeable(noMaskTemplate));
		Assert.False(notSoulBound.IsStorableInAccountWarehouse(noMaskTemplate));
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

		// IsTradeable = template mask bit only (no soulbound guard at template level).
		// Java: ItemTemplate.isTradeable() = mask bit; Item.isTradeable() adds !isSoulBound() at runtime.
		Assert.True(allMask.IsTradeable);      // 1 << 1 — template is tradeable regardless of soul-bound template bit
		Assert.True(allMask.IsBreakable);      // 1 << 6
		Assert.True(allMask.IsSoulBound);      // 1 << 7
		Assert.True(allMask.IsNoEnchant);      // 1 << 9
		Assert.True(allMask.IsRemodelable);    // 1 << 12
		Assert.True(allMask.CanPolish);        // 1 << 17
		Assert.True(allMask.CanApExtract);     // 1 << 16
		Assert.True(allMask.CanSocketGodstone); // 1 << 10
		Assert.True(allMask.IsItemDyePermitted); // 1 << 15
		// Simple mask bits
		Assert.True(allMask.IsSellable);                  // 1 << 2
		Assert.True(allMask.IsStorableInWarehouse);        // 1 << 3
		Assert.True(allMask.IsStorableInAccountWarehouse); // 1 << 4 — template mask bit only
		Assert.True(allMask.IsStorableInLegionWarehouse);  // 1 << 5 — template mask bit only
		Assert.True(allMask.IsRemovedOnLogout);            // 1 << 8
		Assert.True(allMask.CanCompositeWeapon);           // 1 << 11
		Assert.True(allMask.CanSplit);                     // 1 << 13
		Assert.True(allMask.IsDeletable);                  // 1 << 14
		Assert.True(allMask.IsLegionTradeable);            // 1 << 18 — template mask bit only
		// All template-level properties return true when bit is set, regardless of SOUL_BOUND bit.
		// Runtime soulbound checks use InventoryItem.IsSoulBound (not template mask).
		Assert.True(awhMask.IsStorableInAccountWarehouse);
		Assert.True(lwhMask.IsStorableInLegionWarehouse);
		Assert.True(awhSoulBound.IsStorableInAccountWarehouse); // template bit set; runtime soulbound not checked here
		// All false for zero mask
		Assert.False(noMask.IsBreakable);
		Assert.False(noMask.IsSellable);
		Assert.False(noMask.IsStorableInWarehouse);
		Assert.False(noMask.IsStorableInAccountWarehouse);
		Assert.False(noMask.IsStorableInLegionWarehouse);
		Assert.False(noMask.IsRemovedOnLogout);
		Assert.False(noMask.IsDeletable);
		Assert.False(noMask.IsLegionTradeable);
		// All template properties are mask-bit-only; soulbound template bit does NOT gate other properties.
		Assert.True(CreateMinimalItemTemplate(mask: (1<<2)|(1<<7)).IsSellable); // sellable + soulbound mask → still true
		Assert.True(CreateMinimalItemTemplate(mask: (1<<1)|(1<<7)).IsTradeable); // tradeable + soulbound mask → still true at template level
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

	private static InventoryItem CreateInventoryItem(bool isSoulBound)
	{
		return new InventoryItem
		{
			ObjectId = 9001,
			ItemId = 100001,
			Count = 1,
			IsSoulBound = isSoulBound,
		};
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
