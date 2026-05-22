using Aion.GameServer.Model.GameObjects;
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
		Assert.False(player.IsAbnormalSet(PlayerAbnormalState.CantMoveState));

		player.AbnormalState = PlayerAbnormalState.None;

		Assert.True(player.IsAbnormalSet(PlayerAbnormalState.None));
		Assert.True(player.IsInAnyAbnormalState(PlayerAbnormalState.None));
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
		player.SetCreatureState(PlayerCreatureState.Flying, enabled: true);
		Assert.False(player.CanStartRideSprint());

		player.SetCreatureState(PlayerCreatureState.Flying, enabled: false);
		player.RideInfo = player.RideInfo with { SprintSpeed = 0 };
		Assert.False(player.RideInfo.CanSprint());
		Assert.False(player.CanStartRideSprint());
	}

	[Fact]
	public void Player_RideMountAndDismountMatchJavaPlayerActions()
	{
		var player = new Player
		{
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
			CreatureState = PlayerCreatureState.Flying,
			FlightPathType = PlayerFlightPathType.Windstream,
		};

		windstreamPlayer.CompleteFlyTeleport();

		Assert.False(windstreamPlayer.IsInState(PlayerCreatureState.Flying));
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

		Assert.True(player.IsInState(PlayerCreatureState.Flying));
		Assert.True(player.IsInState(PlayerCreatureState.FloatingCorpse));
		Assert.True(player.IsFpReduceActive);
		Assert.False(player.IsFpRestoreActive);

		player.EndFlying();

		Assert.False(player.IsInState(PlayerCreatureState.Flying));
		Assert.False(player.IsInState(PlayerCreatureState.Gliding));
		Assert.False(player.IsInState(PlayerCreatureState.FloatingCorpse));
		Assert.False(player.IsFpReduceActive);
		Assert.True(player.IsFpRestoreActive);
	}

	[Fact]
	public void Player_StopGlidingMatchesJavaFpTaskAndBroadcastDecision()
	{
		var walkingGlider = new Player
		{
			CreatureState = PlayerCreatureState.Gliding,
			IsFpReduceActive = true,
		};

		Assert.True(walkingGlider.StopGliding());
		Assert.False(walkingGlider.IsInState(PlayerCreatureState.Gliding));
		Assert.False(walkingGlider.IsFpReduceActive);
		Assert.True(walkingGlider.IsFpRestoreActive);

		var flyingGlider = new Player
		{
			CreatureState = PlayerCreatureState.Flying | PlayerCreatureState.Gliding,
			IsFpRestoreActive = true,
		};

		Assert.False(flyingGlider.StopGliding());
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
