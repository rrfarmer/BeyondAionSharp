using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Services;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Tests;

public sealed class PlayerStateTests
{
	[Fact]
	public void Player_AddsAndRemovesJavaItemCooldown()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var player = new Player { ObjectId = 1, Name = "CooldownTester" };

		player.AddItemCooldown(delayId: 21, useDelayMillis: 5000, now);

		var cooldown = Assert.Single(player.ItemCooldowns);
		Assert.Equal(21, cooldown.Key);
		Assert.Equal(105_000, cooldown.Value.ReuseTimeMillis);
		Assert.Equal(5, cooldown.Value.UseDelaySeconds);

		player.RemoveItemCooldown(21);

		Assert.Empty(player.ItemCooldowns);
	}

	[Fact]
	public void EmotionLearnService_MatchesJavaDuplicateAndExpirationRules()
	{
		var now = DateTimeOffset.FromUnixTimeSeconds(1_000);
		var player = new Player
		{
			Emotions = [new PlayerEmotion(64, 0)],
		};

		Assert.Equal(EmotionLearnFailure.InvalidItem, EmotionLearnService.ValidateNewEmotion(player, 0, 0, now).Failure);
		Assert.Equal(EmotionLearnFailure.AlreadyKnown, EmotionLearnService.ValidateNewEmotion(player, 64, 0, now).Failure);

		var permanent = EmotionLearnService.ValidateNewEmotion(player, 65, 0, now);
		Assert.True(permanent.Succeeded);
		Assert.Equal(new PlayerEmotion(65, 0), permanent.Emotion);

		var temporary = EmotionLearnService.ValidateNewEmotion(player, 66, 5, now);
		Assert.True(temporary.Succeeded);
		Assert.Equal(new PlayerEmotion(66, 1_300), temporary.Emotion);
		Assert.Equal(300, temporary.Emotion!.SecondsUntilExpiration(now));
	}

	[Fact]
	public void TitleAddService_MatchesJavaDuplicateRaceAndExpirationRules()
	{
		var now = DateTimeOffset.FromUnixTimeSeconds(1_000);
		var titles = new TitleTemplateTable(
			[
				new TitleTemplateSummary(269, 412994, string.Empty, "PC_ALL", Array.Empty<ItemStatModifier>()),
				new TitleTemplateSummary(270, 412995, string.Empty, "ASMODIANS", Array.Empty<ItemStatModifier>()),
			]);
		var player = new Player
		{
			Race = "ELYOS",
			Titles = [new PlayerTitle(1, 0)],
		};

		Assert.Equal(TitleAddFailure.InvalidItem, TitleAddService.ValidateCanAct(player, 0).Failure);
		Assert.Equal(TitleAddFailure.AlreadyKnown, TitleAddService.ValidateCanAct(player, 1).Failure);

		var permanent = TitleAddService.CreateTitle(player, 269, 0, hasMinutes: false, titles, now);
		Assert.True(permanent.Succeeded);
		Assert.Equal(new PlayerTitle(269, 0), permanent.Title);

		var temporary = TitleAddService.CreateTitle(player, 269, 5, hasMinutes: true, titles, now);
		Assert.True(temporary.Succeeded);
		Assert.Equal(new PlayerTitle(269, 1_300), temporary.Title);
		Assert.Equal(300, temporary.Title!.SecondsUntilExpiration(now));

		Assert.Equal(TitleAddFailure.InvalidRace, TitleAddService.CreateTitle(player, 270, 0, false, titles, now).Failure);
		Assert.Equal(TitleAddFailure.InvalidTitle, TitleAddService.CreateTitle(player, 999, 0, false, titles, now).Failure);
	}

	[Fact]
	public async Task SkillLearnService_MatchesJavaSkillBookGuardsAndNormalLearnMessage()
	{
		using var temp = TempDirectory.Create();
		var manager = await DataManager.LoadAsync(
			FindRepoRoot(),
			cacheDirectory: temp.Path,
			validateWhenCacheChanges: false);
		var staticData = manager.StaticData;
		var sourceTemplate = staticData.ItemTemplates.GetItemTemplate(169500916);
		Assert.NotNull(sourceTemplate);
		Assert.Equal(new ItemSkillLearnActionInfo(1, 10, "RANGER"), sourceTemplate.SkillLearnAction);

		var player = new Player
		{
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Exp = staticData.PlayerExperienceTable.GetStartExpForLevel(10),
		};

		var plan = SkillLearnService.CreateSkillBookPlan(player, sourceTemplate, staticData);

		Assert.True(plan.Succeeded);
		var packet = Assert.Single(plan.Packets);
		Assert.Equal(1, packet.Skill.SkillId);
		Assert.Equal(1, packet.Skill.SkillLevel);
		Assert.True(packet.IsNew);
		Assert.Equal(1300050, packet.MessageId);
		Assert.Contains(plan.PersistedSkills, skill => skill.SkillId == 1 && skill.SkillLevel == 1);

		player.Skills = [new PlayerSkill { SkillId = 1, SkillLevel = 1 }];
		Assert.Equal(SkillLearnFailure.AlreadyKnown, SkillLearnService.CreateSkillBookPlan(player, sourceTemplate, staticData).Failure);

		var wrongClassPlayer = new Player
		{
			Race = "ELYOS",
			PlayerClass = "ASSASSIN",
			Exp = staticData.PlayerExperienceTable.GetStartExpForLevel(10),
		};
		Assert.Equal(SkillLearnFailure.InvalidClass, SkillLearnService.CreateSkillBookPlan(wrongClassPlayer, sourceTemplate, staticData).Failure);

		var wrongRacePlayer = new Player
		{
			Race = "ASMODIANS",
			PlayerClass = "RANGER",
			Exp = staticData.PlayerExperienceTable.GetStartExpForLevel(10),
		};
		Assert.Equal(SkillLearnFailure.InvalidRace, SkillLearnService.CreateSkillBookPlan(wrongRacePlayer, sourceTemplate, staticData).Failure);
	}

	[Fact]
	public void InventoryExpansionService_MatchesJavaTicketLevelAndQuestGuards()
	{
		var cubeTicket = new ItemExpandInventoryActionInfo(1, "CUBE");
		var cubePlayer = new Player
		{
			NpcExpands = 1,
			QuestExpands = 1,
			ItemExpands = 0,
			WarehouseBonusExpands = 2,
		};

		var cubePlan = InventoryExpansionService.CreatePlan(cubePlayer, cubeTicket, cubeExpansionLimit: 11);

		Assert.True(cubePlan.Succeeded);
		Assert.Equal(InventoryExpansionStorage.Cube, cubePlan.Storage);
		Assert.Equal(1, cubePlan.NewItemExpands);
		Assert.Equal(2, cubePlan.NewWarehouseBonusExpands);

		cubePlayer.ItemExpands = 1;
		Assert.Equal(
			InventoryExpansionFailure.CubeCannotExpand,
			InventoryExpansionService.CreatePlan(cubePlayer, cubeTicket, cubeExpansionLimit: 11).Failure);

		var warehouseTicket = new ItemExpandInventoryActionInfo(1, "WAREHOUSE");
		var warehousePlayer = new Player
		{
			WarehouseNpcExpands = 1,
			WarehouseBonusExpands = 1,
			Quests = [new PlayerQuestState(1987, "COMPLETE", 0, 0, 0)],
		};

		var warehousePlan = InventoryExpansionService.CreatePlan(warehousePlayer, warehouseTicket, cubeExpansionLimit: 11);

		Assert.True(warehousePlan.Succeeded);
		Assert.Equal(InventoryExpansionStorage.Warehouse, warehousePlan.Storage);
		Assert.Equal(2, warehousePlan.NewWarehouseBonusExpands);

		warehousePlayer.Quests = [];
		Assert.Equal(
			InventoryExpansionFailure.WarehouseCannotExpand,
			InventoryExpansionService.CreatePlan(warehousePlayer, warehouseTicket, cubeExpansionLimit: 11).Failure);
	}

	[Fact]
	public void DyeService_MatchesJavaTargetAndExpirationRules()
	{
		var now = DateTimeOffset.FromUnixTimeSeconds(1_000);
		var targetItem = new InventoryItem { ObjectId = 1, ItemId = 110900040, Color = 0x112233 };
		var dyeableTemplate = new ItemTemplateSummary(
			110900040,
			"Dress",
			0,
			1 << 15,
			1,
			"CL_TORSO",
			"NORMAL",
			"COMMON",
			"PC_ALL",
			1,
			0,
			1);
		var nonDyeableTemplate = dyeableTemplate with { Mask = 0 };

		Assert.Equal(
			DyeFailure.InvalidTarget,
			DyeService.CreateItemDyePlan(null, dyeableTemplate, new ItemDyeActionInfo(0xc22626, 0, false), now).Failure);
		Assert.Equal(
			DyeFailure.NotDyeable,
			DyeService.CreateItemDyePlan(targetItem, nonDyeableTemplate, new ItemDyeActionInfo(0xc22626, 0, false), now).Failure);

		var permanent = DyeService.CreateItemDyePlan(targetItem, dyeableTemplate, new ItemDyeActionInfo(0xc22626, 0, false), now);
		Assert.True(permanent.Succeeded);
		Assert.Equal(0xc22626, permanent.Color);
		Assert.Equal(0, permanent.ColorExpires);

		var temporaryRemoval = DyeService.CreateItemDyePlan(targetItem, dyeableTemplate, new ItemDyeActionInfo(null, 5, true), now);
		Assert.True(temporaryRemoval.Succeeded);
		Assert.Null(temporaryRemoval.Color);
		Assert.Equal(1_300, temporaryRemoval.ColorExpires);
	}

	[Fact]
	public void CosmeticItemService_MatchesJavaGuardsAndAppearanceMutation()
	{
		var player = new Player
		{
			Race = "ELYOS",
			Gender = "MALE",
			Appearance = new CharacterAppearance
			{
				Face = 3,
				Hair = 4,
				SkinRgb = 10,
				HairRgb = 11,
				EyeRgb = 12,
				LipRgb = 13,
				Voice = 2,
				Height = 1.2f,
			},
		};

		Assert.Equal(CosmeticItemFailure.MissingTemplate, CosmeticItemService.CreatePlan(player, null).Failure);
		Assert.Equal(
			CosmeticItemFailure.InvalidRace,
			CosmeticItemService.CreatePlan(player, new CosmeticItemSummary("hair_type", "wrong_race", 1, "ASMODIANS", "MALE", null)).Failure);
		Assert.Equal(
			CosmeticItemFailure.InvalidGender,
			CosmeticItemService.CreatePlan(player, new CosmeticItemSummary("hair_type", "wrong_gender", 1, "ELYOS", "FEMALE", null)).Failure);

		player.IsInRideMode = true;
		Assert.Equal(
			CosmeticItemFailure.Ride,
			CosmeticItemService.CreatePlan(player, new CosmeticItemSummary("hair_type", "ride", 1, "ELYOS", "MALE", null)).Failure);
		player.IsInRideMode = false;

		var hairPlan = CosmeticItemService.CreatePlan(player, new CosmeticItemSummary("hair_type", "hair", 21, "ELYOS", "MALE", null));

		Assert.True(hairPlan.Succeeded);
		Assert.Equal(21, hairPlan.Appearance?.Hair);
		Assert.Equal(3, hairPlan.Appearance?.Face);
		Assert.Equal(10, hairPlan.Appearance?.SkinRgb);

		var presetPlan = CosmeticItemService.CreatePlan(
			player,
			new CosmeticItemSummary(
				"preset_name",
				"preset",
				0,
				"ELYOS",
				"ALL",
				new CosmeticPresetSummary(1.05f, 6, 7, 100, 101, 102, 103)));

		Assert.True(presetPlan.Succeeded);
		Assert.Equal(6, presetPlan.Appearance?.Hair);
		Assert.Equal(7, presetPlan.Appearance?.Face);
		Assert.Equal(100, presetPlan.Appearance?.HairRgb);
		Assert.Equal(101, presetPlan.Appearance?.LipRgb);
		Assert.Equal(102, presetPlan.Appearance?.EyeRgb);
		Assert.Equal(102, presetPlan.Appearance?.SkinRgb);
		Assert.Equal(1.05f, presetPlan.Appearance?.Height);
		Assert.Equal(CosmeticItemFailure.UnsupportedType, CosmeticItemService.CreatePlan(
			player,
			new CosmeticItemSummary("unknown", "unknown", 0, "ELYOS", "MALE", null)).Failure);
	}

	[Fact]
	public void MotionLearnService_MatchesJavaActiveReplacementAndExpirationRules()
	{
		var now = DateTimeOffset.FromUnixTimeSeconds(1_000);
		var player = new Player
		{
			Motions =
			[
				new PlayerMotion(1, 0, true),
				new PlayerMotion(6, 0, true),
			],
		};
		var action = new ItemAnimationActionInfo(5, 6, 7, 8, null, 5);

		var plan = MotionLearnService.CreatePlan(player, action, now);

		Assert.True(plan.Succeeded);
		Assert.Equal([5, 6, 7, 8], plan.AddedMotions.Select(motion => motion.Id));
		Assert.Equal(1_300, plan.AddedMotions[0].ExpireTimeSeconds);
		Assert.Equal([1], plan.DeactivatedMotionIds);
		Assert.Contains(plan.Motions, motion => motion.Id == 1 && !motion.IsActive);
		Assert.Contains(plan.Motions, motion => motion.Id == 5 && motion.IsActive);
		Assert.Contains(plan.Motions, motion => motion.Id == 6 && motion.IsActive);
		Assert.Contains(plan.Motions, motion => motion.Id == 7 && motion.IsActive);
		Assert.Contains(plan.Motions, motion => motion.Id == 8 && motion.IsActive);

		var permanent = MotionLearnService.CreatePlan(player, new ItemAnimationActionInfo(1, null, null, null, null, 0), now);
		Assert.True(permanent.Succeeded);
		Assert.Equal(0, Assert.Single(permanent.AddedMotions).ExpireTimeSeconds);
	}

	[Fact]
	public void Player_CreatureStateMatchesJavaBitAndExactMultibitSemantics()
	{
		var player = new Player();

		player.SetCreatureState(PlayerCreatureState.WalkMode, enabled: true);
		player.SetCreatureState(PlayerCreatureState.Powershard, enabled: true);

		Assert.True(player.IsInState(PlayerCreatureState.WalkMode));
		Assert.True(player.IsInState(PlayerCreatureState.Powershard));
		Assert.Equal(2, (int)PlayerCreatureState.Flying);
		Assert.Equal(8, (int)PlayerCreatureState.FloatingCorpse);
		Assert.Equal(64, (int)PlayerCreatureState.WalkMode);
		Assert.Equal(128, (int)PlayerCreatureState.Powershard);
		Assert.Equal(512, (int)PlayerCreatureState.Gliding);

		player.ReplaceCreatureState(PlayerCreatureState.Chair);

		Assert.True(player.IsInState(PlayerCreatureState.Chair));
		Assert.False(player.IsInState(PlayerCreatureState.PrivateShop));

		player.ReplaceCreatureState(PlayerCreatureState.PrivateShop);

		Assert.True(player.IsInState(PlayerCreatureState.PrivateShop));
		Assert.False(player.IsInState(PlayerCreatureState.Chair));
	}

	[Fact]
	public void Player_FlyStateMatchesJavaBitAndCompoundSemantics()
	{
		var player = new Player();

		Assert.Equal(1, (int)PlayerFlyState.Flying);
		Assert.Equal(2, (int)PlayerFlyState.Gliding);
		Assert.True(player.IsInFlyState(PlayerFlyState.None));
		Assert.False(player.IsFlying());

		player.SetFlyState(PlayerFlyState.Flying);

		Assert.True(player.IsFlying());
		Assert.True(player.IsInFlyingState());
		Assert.False(player.IsInGlidingState());

		player.SetFlyState(PlayerFlyState.Gliding);

		Assert.True(player.IsFlying());
		Assert.True(player.IsInFlyingState());
		Assert.True(player.IsInGlidingState());
		Assert.Equal(PlayerFlyState.Flying | PlayerFlyState.Gliding, player.FlyState);

		player.UnsetFlyState(PlayerFlyState.Flying);

		Assert.True(player.IsFlying());
		Assert.False(player.IsInFlyingState());
		Assert.True(player.IsInGlidingState());

		player.UnsetFlyState(PlayerFlyState.Gliding);

		Assert.False(player.IsFlying());
		Assert.True(player.IsInFlyState(PlayerFlyState.None));
	}

	[Fact]
	public void Player_AbnormalStateMatchesJavaBitAndCompoundSemantics()
	{
		var player = new Player
		{
			AbnormalState = PlayerAbnormalState.Root | PlayerAbnormalState.Fear | PlayerAbnormalState.Confuse,
		};

		Assert.Equal(16, (int)PlayerAbnormalState.Root);
		Assert.Equal(512, (int)PlayerAbnormalState.Fear);
		Assert.Equal(2048, (int)PlayerAbnormalState.Confuse);
		Assert.True(player.IsAbnormalSet(PlayerAbnormalState.Root));
		Assert.True(player.IsInAnyAbnormalState(PlayerAbnormalState.CantMoveState));
		Assert.True(player.IsUnderFear());
		Assert.True(player.IsConfused());
		Assert.False(player.CanPerformMove());
		Assert.False(player.IsAbnormalSet(PlayerAbnormalState.CantMoveState));

		player.AbnormalState = PlayerAbnormalState.None;

		Assert.True(player.IsAbnormalSet(PlayerAbnormalState.None));
		Assert.True(player.IsInAnyAbnormalState(PlayerAbnormalState.None));
		Assert.True(player.CanPerformMove());

		player.TransformBansMovement = true;

		Assert.False(player.CanPerformMove());
	}

	[Fact]
	public void Player_VisualStateMatchesJavaProtectionAndHideSemantics()
	{
		var player = new Player();

		Assert.Equal(0, PlayerVisualStates.Visible);
		Assert.Equal(1, PlayerVisualStates.Hide1);
		Assert.Equal(2, PlayerVisualStates.Hide2);
		Assert.Equal(3, PlayerVisualStates.Hide3);
		Assert.Equal(5, PlayerVisualStates.Hide5);
		Assert.Equal(10, PlayerVisualStates.Hide10);
		Assert.Equal(13, PlayerVisualStates.Hide13);
		Assert.Equal(20, PlayerVisualStates.Hide20);
		Assert.Equal(64, PlayerVisualStates.Blinking);
		Assert.False(player.IsProtectionActive());
		Assert.False(player.IsInAnyHide());

		player.SetVisualState(PlayerVisualStates.Blinking);

		Assert.True(player.IsProtectionActive());
		Assert.False(player.IsInAnyHide());
		Assert.True(player.StopProtectionActive());
		Assert.Equal(PlayerVisualStates.Visible, player.VisualState);
		Assert.False(player.StopProtectionActive());

		player.SetVisualState(PlayerVisualStates.Hide1);
		player.SetVisualState(PlayerVisualStates.Blinking);
		player.AbnormalState = PlayerAbnormalState.Hide | PlayerAbnormalState.Root;

		Assert.True(player.IsInAnyHide());
		Assert.True(player.RemoveHideEffects());
		Assert.Equal(PlayerVisualStates.Blinking, player.VisualState);
		Assert.True(player.IsAbnormalSet(PlayerAbnormalState.Root));
		Assert.False(player.IsAbnormalSet(PlayerAbnormalState.Hide));
	}

	[Fact]
	public void Player_StanceStateMatchesJavaObserverPresence()
	{
		var player = new Player();

		Assert.False(player.IsUnderStance());

		player.StanceSkillId = 1234;

		Assert.True(player.IsUnderStance());

		player.StanceSkillId = 0;

		Assert.False(player.IsUnderStance());
	}

	[Fact]
	public void Player_RideSprintMatchesJavaGuardAndFpTaskIntent()
	{
		var player = new Player
		{
			LifeStats = new PlayerLifeStats(100, 100, 50),
			IsInRideMode = true,
			RideInfo = new PlayerRideInfo(NpcId: 9001, StartFp: 30, CostFp: 1, SprintSpeed: 12.0f, FlySpeed: 0, MoveSpeed: 9.0f),
		};

		Assert.True(player.RideInfo.CanSprint());
		Assert.True(player.CanStartRideSprint());

		player.StartRideSprint();

		Assert.True(player.IsInSprintMode);
		Assert.True(player.IsFpReduceActive);
		Assert.False(player.IsFpRestoreActive);
		Assert.True(player.CanEndRideSprint());

		player.EndRideSprint();

		Assert.False(player.IsInSprintMode);
		Assert.False(player.IsFpReduceActive);
		Assert.True(player.IsFpRestoreActive);

		player.LifeStats = new PlayerLifeStats(100, 100, 29);
		Assert.False(player.CanStartRideSprint());

		player.LifeStats = new PlayerLifeStats(100, 100, 50);
		player.SetFlyState(PlayerFlyState.Flying);
		Assert.False(player.CanStartRideSprint());

		player.UnsetFlyState(PlayerFlyState.Flying);
		player.RideInfo = player.RideInfo with { SprintSpeed = 0 };
		Assert.False(player.RideInfo.CanSprint());
		Assert.False(player.CanStartRideSprint());
	}

	[Fact]
	public void Player_RideMountAndDismountMatchJavaPlayerActions()
	{
		var player = new Player
		{
			FlyState = PlayerFlyState.Flying,
			CreatureState = PlayerCreatureState.Active | PlayerCreatureState.Flying,
			LifeStats = new PlayerLifeStats(100, 100, 50),
		};
		var rideInfo = new PlayerRideInfo(NpcId: 2000000, StartFp: 10, CostFp: 10, SprintSpeed: 15.0f, FlySpeed: 16.0f, MoveSpeed: 12.0f);

		Assert.True(player.CanStartRide());

		player.MountRide(rideInfo);

		Assert.True(player.IsInRideMode);
		Assert.Same(rideInfo, player.RideInfo);
		Assert.False(player.IsInState(PlayerCreatureState.Active));
		Assert.True(player.IsInState(PlayerCreatureState.Resting));
		Assert.True(player.IsInState(PlayerCreatureState.FloatingCorpse));

		player.StartRideSprint();
		Assert.True(player.IsInSprintMode);

		Assert.True(player.DismountRide());

		Assert.False(player.IsInRideMode);
		Assert.Null(player.RideInfo);
		Assert.False(player.IsInSprintMode);
		Assert.False(player.IsInState(PlayerCreatureState.Resting));
		Assert.False(player.IsInState(PlayerCreatureState.FloatingCorpse));
		Assert.True(player.IsInState(PlayerCreatureState.Active));
		Assert.False(player.IsFpRestoreActive);
		Assert.False(player.DismountRide());

		player.AbnormalState = PlayerAbnormalState.Root;
		Assert.False(player.CanStartRide());
	}

	[Fact]
	public void Player_CompleteFlyTeleportMatchesJavaWindstreamAndTransporterState()
	{
		var windstreamPlayer = new Player
		{
			FlyState = PlayerFlyState.Flying,
			CreatureState = PlayerCreatureState.Flying,
			FlightPathType = PlayerFlightPathType.Windstream,
		};

		windstreamPlayer.CompleteFlyTeleport();

		Assert.False(windstreamPlayer.IsInState(PlayerCreatureState.Flying));
		Assert.False(windstreamPlayer.IsInFlyingState());
		Assert.True(windstreamPlayer.IsInGlidingState());
		Assert.True(windstreamPlayer.IsInState(PlayerCreatureState.Active));
		Assert.True(windstreamPlayer.IsInState(PlayerCreatureState.Gliding));
		Assert.True(windstreamPlayer.IsFpReduceActive);
		Assert.Null(windstreamPlayer.FlightPathType);

		var transporterPlayer = new Player
		{
			CreatureState = PlayerCreatureState.Flying,
			FlightPathType = PlayerFlightPathType.FlightTransporter,
		};

		transporterPlayer.CompleteFlyTeleport();

		Assert.False(transporterPlayer.IsInState(PlayerCreatureState.Flying));
		Assert.True(transporterPlayer.IsInState(PlayerCreatureState.Active));
		Assert.False(transporterPlayer.IsInState(PlayerCreatureState.Gliding));
		Assert.False(transporterPlayer.IsFpReduceActive);
		Assert.Null(transporterPlayer.FlightPathType);
	}

	[Fact]
	public void Player_StartAndEndFlyingMatchJavaFpTaskIntent()
	{
		var player = new Player
		{
			IsInRideMode = true,
			CreatureState = PlayerCreatureState.Active,
		};

		player.StartFlying();

		Assert.True(player.IsInFlyingState());
		Assert.True(player.IsInState(PlayerCreatureState.Flying));
		Assert.True(player.IsInState(PlayerCreatureState.FloatingCorpse));
		Assert.True(player.IsFpReduceActive);
		Assert.False(player.IsFpRestoreActive);

		player.EndFlying();

		Assert.False(player.IsFlying());
		Assert.False(player.IsInState(PlayerCreatureState.Flying));
		Assert.False(player.IsInState(PlayerCreatureState.Gliding));
		Assert.False(player.IsInState(PlayerCreatureState.FloatingCorpse));
		Assert.False(player.IsFpReduceActive);
		Assert.True(player.IsFpRestoreActive);
	}

	[Fact]
	public void PlayerFlightActionService_StartFlyingMatchesJavaGuardAndCooldownSlice()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var startingClass = new Player { PlayerClass = "WARRIOR" };
		var noFly = new Player { PlayerClass = "RANGER", AbnormalState = PlayerAbnormalState.NoFly };
		var transformed = new Player { PlayerClass = "RANGER", TransformForbidsFlight = true };
		var privateShop = new Player { PlayerClass = "RANGER", CreatureState = PlayerCreatureState.PrivateShop };
		var onCooldown = new Player { PlayerClass = "RANGER", FlyReuseTimeMillis = now.ToUnixTimeMilliseconds() + 1 };
		var flying = new Player { PlayerClass = "RANGER" };

		var notDaeva = PlayerFlightActionService.StartFlying(startingClass, now);
		var noFlyResult = PlayerFlightActionService.StartFlying(noFly, now);
		var transformedResult = PlayerFlightActionService.StartFlying(transformed, now);
		var privateShopResult = PlayerFlightActionService.StartFlying(privateShop, now);
		var cooldownResult = PlayerFlightActionService.StartFlying(onCooldown, now);
		var success = PlayerFlightActionService.StartFlying(flying, now);

		Assert.Equal(PlayerFlightActionStatus.NotDaeva, notDaeva.Status);
		Assert.NotNull(notDaeva.SystemMessage);
		Assert.False(startingClass.IsFlying());
		Assert.Equal(PlayerFlightActionStatus.NoFlyAbnormal, noFlyResult.Status);
		Assert.NotNull(noFlyResult.SystemMessage);
		Assert.False(noFly.IsFlying());
		Assert.Equal(PlayerFlightActionStatus.TransformForbidden, transformedResult.Status);
		Assert.NotNull(transformedResult.SystemMessage);
		Assert.False(transformed.IsFlying());
		Assert.Equal(PlayerFlightActionStatus.PrivateStore, privateShopResult.Status);
		Assert.Null(privateShopResult.SystemMessage);
		Assert.False(privateShop.IsFlying());
		Assert.Equal(PlayerFlightActionStatus.Cooldown, cooldownResult.Status);
		Assert.Null(cooldownResult.SystemMessage);
		Assert.False(onCooldown.IsFlying());

		Assert.True(success.Succeeded);
		Assert.True(flying.IsInFlyingState());
		Assert.True(flying.IsInState(PlayerCreatureState.Flying));
		Assert.True(flying.IsFpReduceActive);
		Assert.Equal(now.ToUnixTimeMilliseconds() + 9_900, flying.FlyReuseTimeMillis);

		var ignoredCooldown = new Player { PlayerClass = "RANGER", FlyReuseTimeMillis = now.ToUnixTimeMilliseconds() + 1 };
		var ignoredResult = PlayerFlightActionService.StartFlying(ignoredCooldown, now, ignoreFlightCooldown: true);

		Assert.True(ignoredResult.Succeeded);
		Assert.True(ignoredCooldown.IsInFlyingState());
		Assert.Equal(now.ToUnixTimeMilliseconds() + 1, ignoredCooldown.FlyReuseTimeMillis);
	}

	[Fact]
	public void PlayerFlightActionService_StartGlidingMatchesJavaGuardAndCooldownSlice()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(200_000);
		var startingClass = new Player { PlayerClass = "WARRIOR" };
		var transformed = new Player { PlayerClass = "RANGER", TransformForbidsFlight = true };
		var movementBlocked = new Player { PlayerClass = "RANGER", AbnormalState = PlayerAbnormalState.Root };
		var transformedMovementBlocked = new Player { PlayerClass = "RANGER", TransformBansMovement = true };
		var onCooldown = new Player { PlayerClass = "RANGER", FlyReuseTimeMillis = now.ToUnixTimeMilliseconds() + 1 };
		var walkingGlider = new Player { PlayerClass = "RANGER" };
		var flyingGlider = new Player
		{
			PlayerClass = "RANGER",
			FlyState = PlayerFlyState.Flying,
			CreatureState = PlayerCreatureState.Flying,
			FlyReuseTimeMillis = now.ToUnixTimeMilliseconds() + 50_000,
		};
		var alreadyGliding = new Player { PlayerClass = "RANGER", FlyState = PlayerFlyState.Gliding };

		var notDaeva = PlayerFlightActionService.StartGliding(startingClass, now);
		var transformedResult = PlayerFlightActionService.StartGliding(transformed, now);
		var movementBlockedResult = PlayerFlightActionService.StartGliding(movementBlocked, now);
		var transformedMovementBlockedResult = PlayerFlightActionService.StartGliding(transformedMovementBlocked, now);
		var cooldownResult = PlayerFlightActionService.StartGliding(onCooldown, now);
		var walkingSuccess = PlayerFlightActionService.StartGliding(walkingGlider, now);
		var flyingSuccess = PlayerFlightActionService.StartGliding(flyingGlider, now);
		var alreadyGlidingResult = PlayerFlightActionService.StartGliding(alreadyGliding, now);

		Assert.Equal(PlayerFlightActionStatus.NotDaeva, notDaeva.Status);
		Assert.NotNull(notDaeva.SystemMessage);
		Assert.False(startingClass.IsInGlidingState());
		Assert.Equal(PlayerFlightActionStatus.TransformForbidden, transformedResult.Status);
		Assert.NotNull(transformedResult.SystemMessage);
		Assert.False(transformed.IsInGlidingState());
		Assert.Equal(PlayerFlightActionStatus.CannotMove, movementBlockedResult.Status);
		Assert.Null(movementBlockedResult.SystemMessage);
		Assert.False(movementBlocked.IsInGlidingState());
		Assert.Equal(PlayerFlightActionStatus.CannotMove, transformedMovementBlockedResult.Status);
		Assert.Null(transformedMovementBlockedResult.SystemMessage);
		Assert.False(transformedMovementBlocked.IsInGlidingState());
		Assert.Equal(PlayerFlightActionStatus.Cooldown, cooldownResult.Status);
		Assert.Null(cooldownResult.SystemMessage);
		Assert.False(onCooldown.IsInGlidingState());

		Assert.True(walkingSuccess.Succeeded);
		Assert.True(walkingGlider.IsInGlidingState());
		Assert.True(walkingGlider.IsInState(PlayerCreatureState.Gliding));
		Assert.True(walkingGlider.IsFpReduceActive);
		Assert.Equal(now.ToUnixTimeMilliseconds() + 10_000, walkingGlider.FlyReuseTimeMillis);

		Assert.True(flyingSuccess.Succeeded);
		Assert.True(flyingGlider.IsInFlyingState());
		Assert.True(flyingGlider.IsInGlidingState());
		Assert.True(flyingGlider.IsInState(PlayerCreatureState.Flying));
		Assert.True(flyingGlider.IsInState(PlayerCreatureState.Gliding));
		Assert.True(flyingGlider.IsFpReduceActive);
		Assert.Equal(now.ToUnixTimeMilliseconds() + 50_000, flyingGlider.FlyReuseTimeMillis);

		Assert.Equal(PlayerFlightActionStatus.AlreadyGliding, alreadyGlidingResult.Status);
		Assert.False(alreadyGlidingResult.Succeeded);
		Assert.Null(alreadyGlidingResult.SystemMessage);
	}

	[Fact]
	public void Player_StopGlidingMatchesJavaFpTaskAndBroadcastDecision()
	{
		var walkingGlider = new Player
		{
			FlyState = PlayerFlyState.Gliding,
			CreatureState = PlayerCreatureState.Gliding,
			IsFpReduceActive = true,
		};

		Assert.True(walkingGlider.StopGliding());
		Assert.False(walkingGlider.IsInGlidingState());
		Assert.False(walkingGlider.IsInState(PlayerCreatureState.Gliding));
		Assert.False(walkingGlider.IsFpReduceActive);
		Assert.True(walkingGlider.IsFpRestoreActive);

		var flyingGlider = new Player
		{
			FlyState = PlayerFlyState.Flying | PlayerFlyState.Gliding,
			CreatureState = PlayerCreatureState.Flying | PlayerCreatureState.Gliding,
			IsFpRestoreActive = true,
		};

		Assert.False(flyingGlider.StopGliding());
		Assert.True(flyingGlider.IsInFlyingState());
		Assert.False(flyingGlider.IsInGlidingState());
		Assert.True(flyingGlider.IsInState(PlayerCreatureState.Flying));
		Assert.False(flyingGlider.IsInState(PlayerCreatureState.Gliding));
		Assert.True(flyingGlider.IsFpReduceActive);
		Assert.False(flyingGlider.IsFpRestoreActive);
	}

	private static string FindRepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (Directory.Exists(Path.Combine(directory.FullName, "game-server")))
				return directory.FullName;

			directory = directory.Parent;
		}

		throw new InvalidOperationException("Could not find repository root.");
	}

	private sealed class TempDirectory : IDisposable
	{
		public TempDirectory()
		{
			Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Path);
		}

		public string Path { get; }

		public static TempDirectory Create()
		{
			return new TempDirectory();
		}

		public void Dispose()
		{
			if (Directory.Exists(Path))
				Directory.Delete(Path, recursive: true);
		}
	}
}
