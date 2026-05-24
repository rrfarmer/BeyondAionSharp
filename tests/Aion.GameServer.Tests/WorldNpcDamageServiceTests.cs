using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcDamageServiceTests
{
	[Fact]
	public async Task ApplyDamageAsync_ReducesSpawnedNpcHpViaLifeStats()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out var lifeStats, out var threadPoolManager, out _, out var combatStates, out _, out _, out var registry);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203090, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());

			var result = await damageService.ApplyDamageAsync(npc, CreatePlayer(), damage: 25);

			Assert.Equal(WorldNpcDamageStatus.Damaged, result.Status);
			Assert.Equal(25, result.Damage);
			Assert.True(result.NotifyAttack);
			Assert.NotNull(result.LifeStats);
			Assert.NotNull(result.AttackStatusPacket);
			Assert.Equal(25, result.AttackStatusPacket.Value);
			Assert.Equal(75, result.AttackStatusPacket.HpOrMpPercentage);
			Assert.Equal(SmAttackStatusType.Regular, result.AttackStatusPacket.Type);
			Assert.Equal(1, result.AttackStatusBroadcastCount);
			var broadcast = Assert.Single(registry.Broadcasts);
			Assert.Same(result.AttackStatusPacket, broadcast.Packet);
			Assert.True(broadcast.IncludeSourcePlayer);
			Assert.NotNull(result.CombatState);
			Assert.Equal(1, result.CombatState.AttackedCount);
			var aggro = Assert.Single(result.CombatState.HateEntries);
			Assert.Equal(1001, aggro.AttackerObjectId);
			Assert.Equal(25, aggro.Damage);
			Assert.Equal(25, aggro.Hate);
			Assert.True(aggro.NotifyAttack);
			Assert.Equal(WorldNpcDamageHopType.Damage, aggro.HopType);
			Assert.NotNull(result.AttackedObserverNotification);
			Assert.Equal(npc.ObjectId, result.AttackedObserverNotification.NpcObjectId);
			Assert.Equal(1001, result.AttackedObserverNotification.AttackerObjectId);
			Assert.Equal(0, result.AttackedObserverNotification.SkillId);
			Assert.NotNull(result.CastingInterrupt);
			Assert.Equal(WorldNpcCastingInterruptStatus.NoCastingSkill, result.CastingInterrupt.Status);
			Assert.NotNull(result.SupportAiRequests);
			Assert.Empty(result.SupportAiRequests);
			Assert.Equal(WorldNpcLifeStatsDamageStatus.Reduced, result.LifeStats.Status);
			Assert.Equal(new WorldNpcLifeStats(100, 0, 75, 0), result.LifeStats.Current);
			Assert.True(lifeStats.TryGetStats(npc.ObjectId, out var stored));
			Assert.Equal(75, stored!.CurrentHp);
			Assert.True(combatStates.TryGetState(npc.ObjectId, out var storedCombat));
			Assert.Equal(result.CombatState.NpcObjectId, storedCombat!.NpcObjectId);
			Assert.Equal(result.CombatState.AttackedCount, storedCombat.AttackedCount);
			Assert.Equal(result.CombatState.HateEntries, storedCombat.HateEntries);
			Assert.False(spawnService.HasRespawnTask(npc.ObjectId));
			Assert.False(spawnService.HasDecayTask(npc.ObjectId));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageAsync_TriggersDeathWorkflowForLethalDamage()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out _, out var threadPoolManager, out var aiStates, out _, out _, out _, out var registry);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203091, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());

			var result = await damageService.ApplyDamageAsync(
				npc,
				CreatePlayer(),
				damage: 150,
				new WorldNpcDamageOptions(
					NotifyAttack: true,
					DeathOptions: WorldNpcDeathDropOptions.Default with { RewardLoot = false }));

			Assert.Equal(WorldNpcDamageStatus.Died, result.Status);
			Assert.NotNull(result.LifeStats);
			Assert.NotNull(result.AttackStatusPacket);
			Assert.Equal(100, result.AttackStatusPacket.Value);
			Assert.Equal(0, result.AttackStatusPacket.HpOrMpPercentage);
			Assert.Equal(1, result.AttackStatusBroadcastCount);
			Assert.Single(registry.Broadcasts);
			Assert.Equal(WorldNpcLifeStatsDamageStatus.Died, result.LifeStats.Status);
			Assert.Equal(new WorldNpcLifeStats(100, 0, 0, 0), result.LifeStats.Current);
			Assert.NotNull(result.LifeStats.DeathResult);
			Assert.True(result.LifeStats.DeathResult.RespawnScheduled);
			Assert.True(result.LifeStats.DeathResult.DecayScheduled);
			Assert.True(result.LifeStats.DeathResult.AiMarkedDied);
			Assert.True(spawnService.HasRespawnTask(npc.ObjectId));
			Assert.True(spawnService.HasDecayTask(npc.ObjectId));
			Assert.True(aiStates.TryGetState(npc.ObjectId, out var state));
			Assert.Equal(WorldNpcAiState.Died, state!.State);

			var duplicate = await damageService.ApplyDamageAsync(
				npc,
				CreatePlayer(),
				damage: 1,
				new WorldNpcDamageOptions(
					NotifyAttack: true,
					DeathOptions: WorldNpcDeathDropOptions.Default with { RewardLoot = false }));

			Assert.Equal(WorldNpcDamageStatus.AlreadyDead, duplicate.Status);
			Assert.NotNull(duplicate.LifeStats);
			Assert.Equal(WorldNpcLifeStatsDamageStatus.AlreadyDead, duplicate.LifeStats.Status);
			Assert.Null(duplicate.LifeStats.DeathResult);
			Assert.Equal(1, spawnService.PendingDecayCount);
			Assert.NotNull(spawnService.CancelDecay(npc.ObjectId));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageAsync_CancelsCastingSkillBeforeDamageWorkflow()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out _, out var threadPoolManager, out _, out _, out _, out var castingInterrupts, out _);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203099, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());
			castingInterrupts.SetCastingSkill(
				npc.ObjectId,
				new WorldNpcCastingSkill(
					3001,
					WorldNpcSkillMethod.Item,
					CancelRate: 0));

			var result = await damageService.ApplyDamageAsync(npc, CreatePlayer(), damage: 10);

			Assert.NotNull(result.CastingInterrupt);
			Assert.Equal(WorldNpcCastingInterruptStatus.ItemSkillCanceled, result.CastingInterrupt.Status);
			Assert.True(result.CastingInterrupt.Canceled);
			Assert.Equal(3001, result.CastingInterrupt.Skill?.SkillId);
			Assert.False(castingInterrupts.TryGetCastingSkill(npc.ObjectId, out _));
			Assert.NotNull(result.AttackedObserverNotification);
			Assert.Equal(WorldNpcDamageStatus.Damaged, result.Status);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageEffectAsync_MapsRegularDamageEffectToNpcDamageOptions()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203100, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());

			var result = await skillDamageService.ApplyDamageEffectAsync(new WorldNpcSkillDamageRequest(
				Target: npc,
				Effector: CreatePlayer(),
				Damage: 20,
				SkillId: 5001));

			Assert.Equal(WorldNpcSkillDamageKind.RegularDamageEffect, result.Kind);
			Assert.Equal(WorldNpcDamageStatus.Damaged, result.DamageResult.Status);
			Assert.NotNull(result.DamageResult.AttackStatusPacket);
			Assert.Equal(SmAttackStatusType.Regular, result.DamageResult.AttackStatusPacket.Type);
			Assert.Equal(SmAttackStatusLog.Regular, result.DamageResult.AttackStatusPacket.Log);
			Assert.Equal(5001, result.DamageResult.AttackStatusPacket.SkillId);
			Assert.NotNull(result.AttackObserverNotification);
			Assert.Equal(1001, result.AttackObserverNotification.EffectorObjectId);
			Assert.Equal(npc.ObjectId, result.AttackObserverNotification.TargetObjectId);
			Assert.Equal(5001, result.AttackObserverNotification.SkillId);
			Assert.NotNull(result.CalculationResult);
			Assert.Equal(20, result.CalculationResult.InputDamage);
			Assert.Equal(20, result.CalculationResult.FinalDamage);
			Assert.True(result.CalculationResult.ShouldApplyAttackerMovementModifier);
			Assert.False(result.CalculationResult.IgnoreShield);
			Assert.True(result.CalculationResult.SendResult);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageEffectAsync_AppliesEquipmentObserverBurnsForOrdinaryAttack()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var idianSavedObjects = new List<int>();
		var chargeSavedObjects = new List<int>();
		var skillDamageService = new WorldNpcSkillDamageService(
			damageService,
			itemTemplates: CreateObserverBurnItemTemplates(),
			saveIdianPolishBurnAsync: (_, plan, _) =>
			{
				idianSavedObjects.AddRange(plan.Burns.Select(burn => burn.ItemUpdate.ObjectId));
				return Task.FromResult(true);
			},
			saveItemChargeBurnAsync: (_, plan, _) =>
			{
				chargeSavedObjects.AddRange(plan.Burns.Select(burn => burn.ItemUpdate.ObjectId));
				return Task.FromResult(true);
			});
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203108, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());
			var effector = CreatePlayerWithObserverBurnItem(charge: 100_050, polishCharge: 350_000);

			var result = await skillDamageService.ApplyDamageEffectAsync(new WorldNpcSkillDamageRequest(
				Target: npc,
				Effector: effector,
				Damage: 20,
				SkillId: 0));

			Assert.NotNull(result.AttackObserverNotification);
			Assert.Null(result.DotAttackedObserverNotification);
			Assert.NotNull(result.EquipmentObserverBurns);
			Assert.True(result.EquipmentObserverBurns.Changed);
			Assert.True(result.EquipmentObserverBurns.Persisted);
			Assert.Equal([10], idianSavedObjects);
			Assert.Equal([10], chargeSavedObjects);
			var item = Assert.Single(effector.InventoryItems);
			Assert.Equal(250_000, item.IdianStone?.PolishCharge);
			Assert.Equal(99_850, item.Charge);
			Assert.Collection(
				result.EquipmentObserverBurns.Packets,
				packet => AssertPolishChargePacket(packet, objectId: 10, polishCharge: 250_000),
				packet => AssertChargePacket(packet, objectId: 10, charge: 99_850));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageEffectAsync_UsesStagedSkillResultCalculation()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService, new WorldNpcSkillResultCalculationService());
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203107, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());

			var result = await skillDamageService.ApplyDamageEffectAsync(new WorldNpcSkillDamageRequest(
				Target: npc,
				Effector: CreatePlayer(),
				Damage: 20,
				SkillId: 5008,
				Options: new WorldNpcSkillDamageOptions(
					ResultCalculation: new WorldNpcSkillResultCalculationOptions(
						RandomDamageType: 1,
						RandomRoll: 13,
						CannotMiss: true))));

			Assert.Equal(WorldNpcDamageStatus.Damaged, result.DamageResult.Status);
			Assert.Equal(30, result.DamageResult.Damage);
			Assert.NotNull(result.CalculationResult);
			Assert.Equal(WorldNpcSkillResultCalculationStatus.Calculated, result.CalculationResult.Status);
			Assert.Equal(20, result.CalculationResult.InputDamage);
			Assert.Equal(30, result.CalculationResult.FinalDamage);
			Assert.Equal(1.5f, result.CalculationResult.RandomDamageMultiplier);
			Assert.True(result.CalculationResult.CannotMiss);
			Assert.False(result.CalculationResult.CanDodgeOrResist);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageEffectAsync_MapsProvokedDamageEffectToDamageTypeWithoutAttackObserver()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203101, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());

			var result = await skillDamageService.ApplyDamageEffectAsync(new WorldNpcSkillDamageRequest(
				Target: npc,
				Effector: CreatePlayer(),
				Damage: 20,
				SkillId: 5002,
				Kind: WorldNpcSkillDamageKind.ProvokedDamageEffect));

			Assert.Equal(WorldNpcSkillDamageKind.ProvokedDamageEffect, result.Kind);
			Assert.Equal(WorldNpcDamageStatus.Damaged, result.DamageResult.Status);
			Assert.NotNull(result.DamageResult.AttackStatusPacket);
			Assert.Equal(SmAttackStatusType.Damage, result.DamageResult.AttackStatusPacket.Type);
			Assert.Equal(SmAttackStatusLog.ProcAttackInstant, result.DamageResult.AttackStatusPacket.Log);
			Assert.Equal(5002, result.DamageResult.AttackStatusPacket.SkillId);
			Assert.Null(result.AttackObserverNotification);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageEffectAsync_MapsPeriodicSpellAttackToDotObserver()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203102, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());

			var result = await skillDamageService.ApplyDamageEffectAsync(new WorldNpcSkillDamageRequest(
				Target: npc,
				Effector: CreatePlayer(),
				Damage: 15,
				SkillId: 5003,
				Kind: WorldNpcSkillDamageKind.PeriodicSpellAttack));

			Assert.Equal(WorldNpcSkillDamageKind.PeriodicSpellAttack, result.Kind);
			Assert.Equal(WorldNpcDamageStatus.Damaged, result.DamageResult.Status);
			Assert.False(result.DamageResult.NotifyAttack);
			Assert.NotNull(result.DamageResult.AttackStatusPacket);
			Assert.Equal(SmAttackStatusType.Damage, result.DamageResult.AttackStatusPacket.Type);
			Assert.Equal(SmAttackStatusLog.SpellAttack, result.DamageResult.AttackStatusPacket.Log);
			Assert.Equal(5003, result.DamageResult.AttackStatusPacket.SkillId);
			Assert.Null(result.AttackObserverNotification);
			Assert.NotNull(result.DotAttackedObserverNotification);
			Assert.Equal(1001, result.DotAttackedObserverNotification.EffectorObjectId);
			Assert.Equal(npc.ObjectId, result.DotAttackedObserverNotification.TargetObjectId);
			Assert.Equal(5003, result.DotAttackedObserverNotification.SkillId);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageEffectAsync_AppliesEquipmentObserverBurnsForDotAttack()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(
			damageService,
			itemTemplates: CreateObserverBurnItemTemplates());
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203109, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());
			var effector = CreatePlayerWithObserverBurnItem(charge: 100_050, polishCharge: 350_000);

			var result = await skillDamageService.ApplyDamageEffectAsync(new WorldNpcSkillDamageRequest(
				Target: npc,
				Effector: effector,
				Damage: 15,
				SkillId: 5003,
				Kind: WorldNpcSkillDamageKind.PeriodicSpellAttack));

			Assert.Null(result.AttackObserverNotification);
			Assert.NotNull(result.DotAttackedObserverNotification);
			Assert.NotNull(result.EquipmentObserverBurns);
			Assert.True(result.EquipmentObserverBurns.Changed);
			var item = Assert.Single(effector.InventoryItems);
			Assert.Equal(290_000, item.IdianStone?.PolishCharge);
			Assert.Equal(99_950, item.Charge);
			Assert.Collection(
				result.EquipmentObserverBurns.Packets,
				packet => AssertPolishChargePacket(packet, objectId: 10, polishCharge: 290_000),
				packet => AssertChargePacket(packet, objectId: 10, charge: 99_950));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageEffectAsync_MapsSpellAttackDrainToAttackObserverAndDrainAmounts()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203103, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());

			var result = await skillDamageService.ApplyDamageEffectAsync(new WorldNpcSkillDamageRequest(
				Target: npc,
				Effector: CreatePlayer(),
				Damage: 20,
				SkillId: 5004,
				Kind: WorldNpcSkillDamageKind.SpellAttackDrain,
				Options: new WorldNpcSkillDamageOptions(
					HpDrainPercent: 50,
					MpDrainPercent: 25)));

			Assert.Equal(WorldNpcSkillDamageKind.SpellAttackDrain, result.Kind);
			Assert.Equal(WorldNpcDamageStatus.Damaged, result.DamageResult.Status);
			Assert.True(result.DamageResult.NotifyAttack);
			Assert.NotNull(result.DamageResult.AttackStatusPacket);
			Assert.Equal(SmAttackStatusType.Damage, result.DamageResult.AttackStatusPacket.Type);
			Assert.Equal(SmAttackStatusLog.SpellAttackDrain, result.DamageResult.AttackStatusPacket.Log);
			Assert.Equal(5004, result.DamageResult.AttackStatusPacket.SkillId);
			Assert.NotNull(result.AttackObserverNotification);
			Assert.Null(result.DotAttackedObserverNotification);
			Assert.NotNull(result.DrainResult);
			Assert.Equal(10, result.DrainResult.HpAmount);
			Assert.Equal(5, result.DrainResult.MpAmount);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageEffectAsync_MapsDelayedSpellAttackToDelayDamageAndAttackObserver()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203104, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());

			var result = await skillDamageService.ApplyDamageEffectAsync(new WorldNpcSkillDamageRequest(
				Target: npc,
				Effector: CreatePlayer(),
				Damage: 20,
				SkillId: 5005,
				Kind: WorldNpcSkillDamageKind.DelayedSpellAttackInstant,
				Options: new WorldNpcSkillDamageOptions(Delay: TimeSpan.FromMilliseconds(750))));

			Assert.Equal(WorldNpcSkillDamageKind.DelayedSpellAttackInstant, result.Kind);
			Assert.Equal(WorldNpcDamageStatus.Damaged, result.DamageResult.Status);
			Assert.NotNull(result.DamageResult.AttackStatusPacket);
			Assert.Equal(SmAttackStatusType.DelayDamage, result.DamageResult.AttackStatusPacket.Type);
			Assert.Equal(SmAttackStatusLog.DelayedSpellAttackInstant, result.DamageResult.AttackStatusPacket.Log);
			Assert.NotNull(result.AttackObserverNotification);
			Assert.Null(result.DotAttackedObserverNotification);
			Assert.NotNull(result.DelayResult);
			Assert.Equal(TimeSpan.FromMilliseconds(750), result.DelayResult.Delay);
			Assert.NotNull(result.CalculationResult);
			Assert.True(result.CalculationResult.IgnoreShield);
			Assert.False(result.CalculationResult.SendResult);
			Assert.False(result.CalculationResult.AttackResult.ShieldChecked);
			Assert.False(result.CalculationResult.EffectReserved.Send);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageEffectAsync_MapsProcAttackInstantWithoutAttackObserver()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203105, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());

			var result = await skillDamageService.ApplyDamageEffectAsync(new WorldNpcSkillDamageRequest(
				Target: npc,
				Effector: CreatePlayer(),
				Damage: 20,
				SkillId: 5006,
				Kind: WorldNpcSkillDamageKind.ProcAttackInstant));

			Assert.Equal(WorldNpcSkillDamageKind.ProcAttackInstant, result.Kind);
			Assert.Equal(WorldNpcDamageStatus.Damaged, result.DamageResult.Status);
			Assert.NotNull(result.DamageResult.AttackStatusPacket);
			Assert.Equal(SmAttackStatusType.Damage, result.DamageResult.AttackStatusPacket.Type);
			Assert.Equal(SmAttackStatusLog.ProcAttackInstant, result.DamageResult.AttackStatusPacket.Log);
			Assert.Null(result.AttackObserverNotification);
			Assert.Null(result.DotAttackedObserverNotification);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageEffectAsync_MapsBleedPeriodicToDotObserver()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203106, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());

			var result = await skillDamageService.ApplyDamageEffectAsync(new WorldNpcSkillDamageRequest(
				Target: npc,
				Effector: CreatePlayer(),
				Damage: 15,
				SkillId: 5007,
				Kind: WorldNpcSkillDamageKind.BleedPeriodic));

			Assert.Equal(WorldNpcSkillDamageKind.BleedPeriodic, result.Kind);
			Assert.Equal(WorldNpcDamageStatus.Damaged, result.DamageResult.Status);
			Assert.False(result.DamageResult.NotifyAttack);
			Assert.NotNull(result.DamageResult.AttackStatusPacket);
			Assert.Equal(SmAttackStatusType.Damage, result.DamageResult.AttackStatusPacket.Type);
			Assert.Equal(SmAttackStatusLog.Bleed, result.DamageResult.AttackStatusPacket.Log);
			Assert.Null(result.AttackObserverNotification);
			Assert.NotNull(result.DotAttackedObserverNotification);
			Assert.Equal(5007, result.DotAttackedObserverNotification.SkillId);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task StartOverTimeEffect_BleedReservesDamageAndSchedulesAbnormal()
	{
		var damageService = CreateDamageService(out _, out _, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			var result = skillDamageService.StartOverTimeEffect(new WorldNpcSkillOverTimeEffectStartRequest(
				WorldNpcSkillOverTimeEffectKind.Bleed,
				BaseValue: 30,
				SkillId: 7001,
				Position: 2,
				CheckTime: TimeSpan.FromSeconds(1),
				MagicalOverTime: new WorldNpcSkillMagicalOverTimeOptions(
					MagicalSkillDamage: 40,
					BaseMagicalDamageMultiplier: 1.5f,
					PvpPveMultiplier: 1f)));

			Assert.Equal(WorldNpcSkillOverTimeEffectCallerStatus.Applied, result.Status);
			Assert.False(result.UseMagicBoost);
			Assert.Equal(PlayerAbnormalState.Bleed, result.AbnormalState);
			Assert.True(result.AppliesAbnormalState);
			Assert.True(result.ReserveDamageOnStart);
			Assert.True(result.SchedulesPeriodicTask);
			Assert.Equal(TimeSpan.FromMilliseconds(1300), result.InitialDelay);
			Assert.NotNull(result.Reserved);
			Assert.Equal(2, result.Reserved.Position);
			Assert.Equal(60, result.Reserved.Value);
			Assert.Equal(WorldNpcEffectResourceType.Hp, result.Reserved.Type);
			Assert.True(result.Reserved.IsDamage);
			Assert.False(result.Reserved.Send);
			Assert.NotNull(result.CalculationResult);
			Assert.True(result.CalculationResult.Applied);
			Assert.Equal(60, result.CalculationResult.FinalDamage);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task StartOverTimeEffect_SpellAttackUsesShugoVenomMagicBoostExclusion()
	{
		var damageService = CreateDamageService(out _, out _, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			var result = skillDamageService.StartOverTimeEffect(new WorldNpcSkillOverTimeEffectStartRequest(
				WorldNpcSkillOverTimeEffectKind.SpellAttack,
				BaseValue: 7,
				SkillId: 21110,
				Position: 1,
				CheckTime: TimeSpan.Zero,
				MagicalOverTime: new WorldNpcSkillMagicalOverTimeOptions(EffectorIsTrap: true)));

			Assert.Equal(WorldNpcSkillOverTimeEffectCallerStatus.Applied, result.Status);
			Assert.False(result.UseMagicBoost);
			Assert.Equal(PlayerAbnormalState.None, result.AbnormalState);
			Assert.False(result.AppliesAbnormalState);
			Assert.False(result.SchedulesPeriodicTask);
			Assert.Null(result.InitialDelay);
			Assert.NotNull(result.Reserved);
			Assert.Equal(7, result.Reserved.Value);
			Assert.NotNull(result.CalculationResult);
			Assert.False(result.CalculationResult.UseMagicBoost);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyOverTimePeriodicActionAsync_PoisonUsesReservedDamageAndDotObserver()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203108, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());

			var result = await skillDamageService.ApplyOverTimePeriodicActionAsync(new WorldNpcSkillOverTimePeriodicActionRequest(
				Target: npc,
				Effector: CreatePlayer(),
				SkillId: 7002,
				Kind: WorldNpcSkillOverTimeEffectKind.Poison,
				ReservedDamage: 15));

			Assert.True(result.Applied);
			Assert.Equal(WorldNpcSkillDamageKind.PoisonPeriodic, result.DamageKind);
			Assert.Equal(15, result.Damage);
			Assert.True(result.UsedReservedDamage);
			Assert.False(result.RecalculatedDamage);
			Assert.Null(result.CalculationResult);
			Assert.NotNull(result.DamageResult);
			Assert.Equal(WorldNpcSkillDamageKind.PoisonPeriodic, result.DamageResult.Kind);
			Assert.Equal(WorldNpcDamageStatus.Damaged, result.DamageResult.DamageResult.Status);
			Assert.False(result.DamageResult.DamageResult.NotifyAttack);
			Assert.NotNull(result.DamageResult.DamageResult.AttackStatusPacket);
			Assert.Equal(SmAttackStatusLog.Poison, result.DamageResult.DamageResult.AttackStatusPacket.Log);
			Assert.Null(result.DamageResult.AttackObserverNotification);
			Assert.NotNull(result.DamageResult.DotAttackedObserverNotification);
			Assert.Equal(7002, result.DamageResult.DotAttackedObserverNotification.SkillId);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyOverTimePeriodicActionAsync_SpellAttackDrainRecalculatesDamageAndDrain()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203109, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());

			var result = await skillDamageService.ApplyOverTimePeriodicActionAsync(new WorldNpcSkillOverTimePeriodicActionRequest(
				Target: npc,
				Effector: CreatePlayer(),
				SkillId: 7003,
				Kind: WorldNpcSkillOverTimeEffectKind.SpellAttackDrain,
				BaseValue: 20,
				MagicalOverTime: new WorldNpcSkillMagicalOverTimeOptions(EffectorIsTrap: true),
				DamageOptions: new WorldNpcSkillDamageOptions(
					HpDrainPercent: 50,
					MpDrainPercent: 25)));

			Assert.True(result.Applied);
			Assert.Equal(WorldNpcSkillDamageKind.SpellAttackDrain, result.DamageKind);
			Assert.Equal(20, result.Damage);
			Assert.False(result.UsedReservedDamage);
			Assert.True(result.RecalculatedDamage);
			Assert.NotNull(result.CalculationResult);
			Assert.True(result.CalculationResult.UseMagicBoost);
			Assert.Equal(20, result.CalculationResult.FinalDamage);
			Assert.NotNull(result.DamageResult);
			Assert.Equal(WorldNpcSkillDamageKind.SpellAttackDrain, result.DamageResult.Kind);
			Assert.NotNull(result.DamageResult.DamageResult.AttackStatusPacket);
			Assert.Equal(SmAttackStatusLog.SpellAttackDrain, result.DamageResult.DamageResult.AttackStatusPacket.Log);
			Assert.NotNull(result.DamageResult.AttackObserverNotification);
			Assert.Null(result.DamageResult.DotAttackedObserverNotification);
			Assert.NotNull(result.DamageResult.DrainResult);
			Assert.Equal(10, result.DamageResult.DrainResult.HpAmount);
			Assert.Equal(5, result.DamageResult.DrainResult.MpAmount);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyOverTimePeriodicActionAsync_RecordsMissingPeriodicInputs()
	{
		var damageService = CreateDamageService(out _, out _, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			var missingReserved = await skillDamageService.ApplyOverTimePeriodicActionAsync(new WorldNpcSkillOverTimePeriodicActionRequest(
				Target: null,
				Effector: null,
				SkillId: 7004,
				Kind: WorldNpcSkillOverTimeEffectKind.Bleed));

			Assert.False(missingReserved.Applied);
			Assert.Equal(WorldNpcSkillOverTimeEffectCallerStatus.MissingReservedDamage, missingReserved.Status);
			Assert.Null(missingReserved.DamageResult);

			var missingBaseValue = await skillDamageService.ApplyOverTimePeriodicActionAsync(new WorldNpcSkillOverTimePeriodicActionRequest(
				Target: null,
				Effector: null,
				SkillId: 7005,
				Kind: WorldNpcSkillOverTimeEffectKind.SpellAttackDrain));

			Assert.False(missingBaseValue.Applied);
			Assert.Equal(WorldNpcSkillOverTimeEffectCallerStatus.MissingBaseValue, missingBaseValue.Status);
			Assert.Null(missingBaseValue.DamageResult);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task StartResourceOverTimeEffect_MpAttackSchedulesWithoutReservation()
	{
		var damageService = CreateDamageService(out _, out _, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			var result = skillDamageService.StartResourceOverTimeEffect(new WorldNpcSkillResourceOverTimeStartRequest(
				WorldNpcSkillResourceOverTimeEffectKind.MpAttack,
				Value: 25,
				SkillId: 7101,
				Position: 0,
				CheckTime: TimeSpan.FromMilliseconds(500)));

			Assert.Equal(WorldNpcSkillResourceOverTimeStatus.Applied, result.Status);
			Assert.Equal(WorldNpcEffectResourceType.Mp, result.ResourceType);
			Assert.True(result.IsDamage);
			Assert.False(result.ReserveValueOnStart);
			Assert.Null(result.Reserved);
			Assert.True(result.SchedulesPeriodicTask);
			Assert.Equal(TimeSpan.FromMilliseconds(800), result.InitialDelay);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyResourceOverTimePeriodicAction_MpAttackAppliesPercentDamagePacket()
	{
		var damageService = CreateDamageService(out _, out _, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			var result = skillDamageService.ApplyResourceOverTimePeriodicAction(new WorldNpcSkillResourceOverTimePeriodicActionRequest(
				WorldNpcSkillResourceOverTimeEffectKind.MpAttack,
				Value: 10,
				SkillId: 7102,
				CurrentResource: 200,
				MaxResource: 250,
				Percent: true,
				TargetIsPlayer: false));

			Assert.True(result.Applied);
			Assert.True(result.IsDamage);
			Assert.Equal(WorldNpcEffectResourceType.Mp, result.ResourceType);
			Assert.Equal(25, result.FinalValue);
			Assert.True(result.PercentApplied);
			Assert.Equal(SmAttackStatusType.DamageMp, result.PacketType);
			Assert.Equal(SmAttackStatusLog.MpAttack, result.PacketLog);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyResourceOverTimePeriodicAction_FpAttackRequiresPlayer()
	{
		var damageService = CreateDamageService(out _, out _, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			var skipped = skillDamageService.ApplyResourceOverTimePeriodicAction(new WorldNpcSkillResourceOverTimePeriodicActionRequest(
				WorldNpcSkillResourceOverTimeEffectKind.FpAttack,
				Value: 8,
				SkillId: 7103,
				CurrentResource: 100,
				MaxResource: 200,
				TargetIsPlayer: false));

			Assert.False(skipped.Applied);
			Assert.Equal(WorldNpcSkillResourceOverTimeStatus.TargetNotPlayer, skipped.Status);

			var applied = skillDamageService.ApplyResourceOverTimePeriodicAction(new WorldNpcSkillResourceOverTimePeriodicActionRequest(
				WorldNpcSkillResourceOverTimeEffectKind.FpAttack,
				Value: 8,
				SkillId: 7103,
				CurrentResource: 100,
				MaxResource: 200,
				TargetIsPlayer: true));

			Assert.True(applied.Applied);
			Assert.Equal(8, applied.FinalValue);
			Assert.Equal(WorldNpcEffectResourceType.Fp, applied.ResourceType);
			Assert.Equal(SmAttackStatusType.FpDamage, applied.PacketType);
			Assert.Equal(SmAttackStatusLog.FpAttack, applied.PacketLog);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task StartResourceOverTimeEffect_HpHealReservesNonDamageResource()
	{
		var damageService = CreateDamageService(out _, out _, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			var result = skillDamageService.StartResourceOverTimeEffect(new WorldNpcSkillResourceOverTimeStartRequest(
				WorldNpcSkillResourceOverTimeEffectKind.HpHeal,
				Value: 45,
				SkillId: 7104,
				Position: 3,
				CheckTime: TimeSpan.FromSeconds(2)));

			Assert.Equal(WorldNpcSkillResourceOverTimeStatus.Applied, result.Status);
			Assert.Equal(WorldNpcEffectResourceType.Hp, result.ResourceType);
			Assert.False(result.IsDamage);
			Assert.True(result.ReserveValueOnStart);
			Assert.NotNull(result.Reserved);
			Assert.Equal(3, result.Reserved.Position);
			Assert.Equal(45, result.Reserved.Value);
			Assert.Equal(WorldNpcEffectResourceType.Hp, result.Reserved.Type);
			Assert.False(result.Reserved.IsDamage);
			Assert.Equal(-45, result.Reserved.ValueToSend);
			Assert.Equal(TimeSpan.FromMilliseconds(2300), result.InitialDelay);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyResourceOverTimePeriodicAction_HpHealAppliesDeboostAndCaps()
	{
		var damageService = CreateDamageService(out _, out _, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			var result = skillDamageService.ApplyResourceOverTimePeriodicAction(new WorldNpcSkillResourceOverTimePeriodicActionRequest(
				WorldNpcSkillResourceOverTimeEffectKind.HpHeal,
				Value: 50,
				SkillId: 7105,
				CurrentResource: 80,
				MaxResource: 100,
				HasItemTemplate: false,
				HealSkillDeboostedValue: 30));

			Assert.True(result.Applied);
			Assert.False(result.IsDamage);
			Assert.Equal(WorldNpcEffectResourceType.Hp, result.ResourceType);
			Assert.True(result.HealSkillDeboostApplied);
			Assert.Equal(30, result.ValueBeforeCap);
			Assert.Equal(20, result.FinalValue);
			Assert.Equal(SmAttackStatusType.Hp, result.PacketType);
			Assert.Equal(SmAttackStatusLog.Heal, result.PacketLog);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyResourceOverTimePeriodicAction_FpAndDpHealRequirePlayerAndSkipFullResource()
	{
		var damageService = CreateDamageService(out _, out _, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			var fpSkipped = skillDamageService.ApplyResourceOverTimePeriodicAction(new WorldNpcSkillResourceOverTimePeriodicActionRequest(
				WorldNpcSkillResourceOverTimeEffectKind.FpHeal,
				Value: 10,
				SkillId: 7106,
				CurrentResource: 20,
				MaxResource: 100,
				TargetIsPlayer: false));

			Assert.False(fpSkipped.Applied);
			Assert.Equal(WorldNpcSkillResourceOverTimeStatus.TargetNotPlayer, fpSkipped.Status);

			var dpApplied = skillDamageService.ApplyResourceOverTimePeriodicAction(new WorldNpcSkillResourceOverTimePeriodicActionRequest(
				WorldNpcSkillResourceOverTimeEffectKind.DpHeal,
				Value: 30,
				SkillId: 7107,
				CurrentResource: 50,
				MaxResource: 100,
				TargetIsPlayer: true));

			Assert.True(dpApplied.Applied);
			Assert.Equal(WorldNpcEffectResourceType.Dp, dpApplied.ResourceType);
			Assert.Null(dpApplied.PacketType);
			Assert.Null(dpApplied.PacketLog);
			Assert.Equal(30, dpApplied.FinalValue);

			var fullMp = skillDamageService.ApplyResourceOverTimePeriodicAction(new WorldNpcSkillResourceOverTimePeriodicActionRequest(
				WorldNpcSkillResourceOverTimeEffectKind.MpHeal,
				Value: 30,
				SkillId: 7108,
				CurrentResource: 100,
				MaxResource: 100));

			Assert.False(fullMp.Applied);
			Assert.Equal(WorldNpcSkillResourceOverTimeStatus.NoResourceChange, fullMp.Status);
			Assert.Equal(0, fullMp.FinalValue);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task CalculateInstantResourceEffect_MpAttackInstantReservesPercentMp()
	{
		var damageService = CreateDamageService(out _, out _, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			var result = skillDamageService.CalculateInstantResourceEffect(new WorldNpcSkillInstantResourceEffectRequest(
				WorldNpcSkillInstantResourceEffectKind.MpAttackInstant,
				Value: 20,
				SkillId: 7201,
				Position: 4,
				MaxResource: 250,
				Percent: true));

			Assert.True(result.Applied);
			Assert.Equal(50, result.FinalValue);
			Assert.Equal(WorldNpcEffectResourceType.Mp, result.ResourceType);
			Assert.True(result.ReserveValueOnCalculate);
			Assert.NotNull(result.Reserved);
			Assert.Equal(4, result.Reserved.Position);
			Assert.Equal(50, result.Reserved.Value);
			Assert.Equal(WorldNpcEffectResourceType.Mp, result.Reserved.Type);
			Assert.True(result.Reserved.IsDamage);
			Assert.True(result.Reserved.Send);
			Assert.Equal(SmAttackStatusType.DamageMp, result.PacketType);
			Assert.Equal(SmAttackStatusLog.MpAttack, result.PacketLog);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task CalculateInstantResourceEffect_FpAttackInstantRequiresPlayer()
	{
		var damageService = CreateDamageService(out _, out _, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			var skipped = skillDamageService.CalculateInstantResourceEffect(new WorldNpcSkillInstantResourceEffectRequest(
				WorldNpcSkillInstantResourceEffectKind.FpAttackInstant,
				Value: 12,
				SkillId: 7202,
				Position: 1,
				MaxResource: 100,
				TargetIsPlayer: false));

			Assert.False(skipped.Applied);
			Assert.Equal(WorldNpcSkillInstantEffectStatus.TargetNotPlayer, skipped.Status);
			Assert.Null(skipped.Reserved);

			var applied = skillDamageService.CalculateInstantResourceEffect(new WorldNpcSkillInstantResourceEffectRequest(
				WorldNpcSkillInstantResourceEffectKind.FpAttackInstant,
				Value: 12,
				SkillId: 7202,
				Position: 1,
				MaxResource: 100,
				TargetIsPlayer: true));

			Assert.True(applied.Applied);
			Assert.Equal(12, applied.FinalValue);
			Assert.Equal(WorldNpcEffectResourceType.Fp, applied.ResourceType);
			Assert.Equal(SmAttackStatusType.FpDamage, applied.PacketType);
			Assert.Equal(SmAttackStatusLog.FpAttack, applied.PacketLog);
			Assert.NotNull(applied.Reserved);
			Assert.Equal(WorldNpcEffectResourceType.Fp, applied.Reserved.Type);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task CalculateInstantResourceEffect_DelayedFpAttackRecordsDelayAndEnemyGate()
	{
		var damageService = CreateDamageService(out _, out _, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			var skipped = skillDamageService.CalculateInstantResourceEffect(new WorldNpcSkillInstantResourceEffectRequest(
				WorldNpcSkillInstantResourceEffectKind.DelayedFpAttackInstant,
				Value: 10,
				SkillId: 7203,
				Position: 0,
				MaxResource: 300,
				Percent: true,
				TargetIsPlayer: true,
				IsEnemy: false,
				Delay: TimeSpan.FromMilliseconds(750)));

			Assert.False(skipped.Applied);
			Assert.Equal(WorldNpcSkillInstantEffectStatus.NotEnemy, skipped.Status);
			Assert.True(skipped.SchedulesDelayedAction);
			Assert.Equal(TimeSpan.FromMilliseconds(750), skipped.Delay);
			Assert.Null(skipped.Reserved);

			var applied = skillDamageService.CalculateInstantResourceEffect(new WorldNpcSkillInstantResourceEffectRequest(
				WorldNpcSkillInstantResourceEffectKind.DelayedFpAttackInstant,
				Value: 10,
				SkillId: 7203,
				Position: 0,
				MaxResource: 300,
				Percent: true,
				TargetIsPlayer: true,
				IsEnemy: true,
				Delay: TimeSpan.FromMilliseconds(750)));

			Assert.True(applied.Applied);
			Assert.Equal(30, applied.FinalValue);
			Assert.True(applied.SchedulesDelayedAction);
			Assert.Equal(TimeSpan.FromMilliseconds(750), applied.Delay);
			Assert.False(applied.ReserveValueOnCalculate);
			Assert.Null(applied.Reserved);
			Assert.Equal(SmAttackStatusType.FpDamage, applied.PacketType);
			Assert.Equal(SmAttackStatusLog.FpAttack, applied.PacketLog);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task CalculateInstantDrainEffect_RecordsSkillAndSpellDrainMetadata()
	{
		var damageService = CreateDamageService(out _, out _, out _, out var threadPoolManager, out _, out _, out _, out _, out _);
		var skillDamageService = new WorldNpcSkillDamageService(damageService);
		try
		{
			var skillDrain = skillDamageService.CalculateInstantDrainEffect(new WorldNpcSkillInstantDrainEffectRequest(
				WorldNpcSkillInstantDrainEffectKind.SkillAttackDrainInstant,
				ReservedDamage: 80,
				HpPercent: 50,
				MpPercent: 25));

			Assert.True(skillDrain.Applied);
			Assert.Equal(40, skillDrain.HpAmount);
			Assert.Equal(20, skillDrain.MpAmount);
			Assert.Equal(TimeSpan.FromSeconds(1), skillDrain.Delay);
			Assert.Equal(SmAttackStatusType.AbsorbedHp, skillDrain.HpPacketType);
			Assert.Equal(SmAttackStatusType.Mp, skillDrain.MpPacketType);
			Assert.Equal(SmAttackStatusLog.SkillAttackDrainInstant, skillDrain.PacketLog);

			var spellDrain = skillDamageService.CalculateInstantDrainEffect(new WorldNpcSkillInstantDrainEffectRequest(
				WorldNpcSkillInstantDrainEffectKind.SpellAttackDrainInstant,
				ReservedDamage: 80,
				HpPercent: 50,
				MpPercent: 25));

			Assert.True(spellDrain.Applied);
			Assert.Equal(40, spellDrain.HpAmount);
			Assert.Equal(20, spellDrain.MpAmount);
			Assert.Equal(SmAttackStatusType.Hp, spellDrain.HpPacketType);
			Assert.Equal(SmAttackStatusType.AbsorbedMp, spellDrain.MpPacketType);
			Assert.Equal(SmAttackStatusLog.SpellAttackDrainInstant, spellDrain.PacketLog);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageAsync_RecordsAttackedObserverAndSupportAiEvents()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out _, out var threadPoolManager, out _, out _, out var combatEvents, out _, out _);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203095, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());
			AddWorldNpc(world, objectId: 9001, templateId: 203096, position: npc.Position with { X = npc.Position.X + 20 });
			AddWorldNpc(world, objectId: 9002, templateId: 203097, position: npc.Position with { X = npc.Position.X + 200 });
			AddWorldNpc(world, objectId: 9003, templateId: 203098, position: npc.Position with { WorldId = 220010000 });

			var result = await damageService.ApplyDamageAsync(
				npc,
				CreatePlayer(),
				damage: 10,
				new WorldNpcDamageOptions(
					NotifyAttack: true,
					SkillId: 1234));

			Assert.Equal(WorldNpcDamageStatus.Damaged, result.Status);
			Assert.NotNull(result.AttackedObserverNotification);
			Assert.Equal(npc.ObjectId, result.AttackedObserverNotification.NpcObjectId);
			Assert.Equal(1001, result.AttackedObserverNotification.AttackerObjectId);
			Assert.Equal(1234, result.AttackedObserverNotification.SkillId);
			var supportRequest = Assert.Single(result.SupportAiRequests!);
			Assert.Equal(9001, supportRequest.SupportNpcObjectId);
			Assert.Equal(npc.ObjectId, supportRequest.AttackedNpcObjectId);
			Assert.Equal(WorldNpcAiEventType.CreatureNeedsSupport, supportRequest.EventType);
			Assert.True(result.AttackedObserverNotification.Sequence < supportRequest.Sequence);
			Assert.True(combatEvents.TryGetState(npc.ObjectId, out var eventState));
			Assert.Equal(result.AttackedObserverNotification, Assert.Single(eventState!.AttackedObserverNotifications));
			Assert.Equal(supportRequest, Assert.Single(eventState.SupportAiRequests));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageAsync_ReturnsNotSpawnedBeforeCreatingStats()
	{
		var damageService = CreateDamageService(out _, out _, out var lifeStats, out var threadPoolManager, out _, out var combatStates, out var combatEvents, out var castingInterrupts, out _);
		try
		{
			var npc = CreateWorldNpc(objectId: 77, maxHp: 100);

			var result = await damageService.ApplyDamageAsync(npc, CreatePlayer(), damage: 10);

			Assert.Equal(WorldNpcDamageStatus.NotSpawned, result.Status);
			Assert.Null(result.LifeStats);
			Assert.False(lifeStats.TryGetStats(npc.ObjectId, out _));
			Assert.False(combatStates.TryGetState(npc.ObjectId, out _));
			Assert.False(combatEvents.TryGetState(npc.ObjectId, out _));
			Assert.False(castingInterrupts.TryGetCastingSkill(npc.ObjectId, out _));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageAsync_ReturnsMissingAttackerWithoutReducingHp()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out var lifeStats, out var threadPoolManager, out _, out var combatStates, out var combatEvents, out var castingInterrupts, out var registry);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203092, maxHp: 100);
			var npc = Assert.Single(world.GetNpcs());

			var result = await damageService.ApplyDamageAsync(npc, attacker: null, damage: 10);

			Assert.Equal(WorldNpcDamageStatus.MissingAttacker, result.Status);
			Assert.Null(result.LifeStats);
			Assert.Null(result.AttackStatusPacket);
			Assert.Empty(registry.Broadcasts);
			Assert.True(lifeStats.TryGetStats(npc.ObjectId, out var stored));
			Assert.Equal(100, stored!.CurrentHp);
			Assert.False(combatStates.TryGetState(npc.ObjectId, out _));
			Assert.False(combatEvents.TryGetState(npc.ObjectId, out _));
			Assert.False(castingInterrupts.TryGetCastingSkill(npc.ObjectId, out _));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	[Fact]
	public async Task ApplyDamageAsync_ReturnsMissingLifeStatsWhenMaxHpUnavailable()
	{
		var damageService = CreateDamageService(out var spawnService, out var world, out var lifeStats, out var threadPoolManager, out _, out var combatStates, out var combatEvents, out var castingInterrupts, out _);
		try
		{
			SpawnNpc(spawnService, world, npcTemplateId: 203093, maxHp: 0);
			var npc = Assert.Single(world.GetNpcs());

			var result = await damageService.ApplyDamageAsync(npc, CreatePlayer(), damage: 10);

			Assert.Equal(WorldNpcDamageStatus.MissingLifeStats, result.Status);
			Assert.Null(result.LifeStats);
			Assert.False(lifeStats.TryGetStats(npc.ObjectId, out _));
			Assert.False(combatStates.TryGetState(npc.ObjectId, out _));
			Assert.False(combatEvents.TryGetState(npc.ObjectId, out _));
			Assert.False(castingInterrupts.TryGetCastingSkill(npc.ObjectId, out _));
		}
		finally
		{
			await threadPoolManager.ShutdownAsync();
		}
	}

	private static WorldNpcDamageService CreateDamageService(
		out WorldNpcSpawnService spawnService,
		out GameWorld world,
		out WorldNpcLifeStatsService lifeStats,
		out ThreadPoolManager threadPoolManager,
		out WorldNpcAiStateService aiStates,
		out WorldNpcCombatStateService combatStates,
		out WorldNpcCombatEventService combatEvents,
		out WorldNpcCastingInterruptService castingInterrupts,
		out CapturingConnectionRegistry registry)
	{
		world = new GameWorld(NullLogger<GameWorld>.Instance);
		var dropRegistration = new WorldNpcDropRegistrationService();
		threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		aiStates = new WorldNpcAiStateService();
		var stagedCombatStates = new WorldNpcCombatStateService();
		var stagedCombatEvents = new WorldNpcCombatEventService();
		var stagedCastingInterrupts = new WorldNpcCastingInterruptService();
		combatStates = stagedCombatStates;
		combatEvents = stagedCombatEvents;
		castingInterrupts = stagedCastingInterrupts;
		var staticPlaceables = new StaticPlaceableStateService();
		registry = new CapturingConnectionRegistry();
		WorldNpcLifeStatsService? stagedLifeStats = null;
		spawnService = new WorldNpcSpawnService(
			new GameServerRuntimeContext(),
			world,
			new IDFactory(),
			gameTimeService: null,
			threadPoolManager,
			connectionRegistry: null,
			staticPlaceables,
			walkerSpawnPlans: null,
			walkerPlacementApplication: null,
			NullLogger<WorldNpcSpawnService>.Instance,
			dropRegistrationLookup: dropRegistration,
			npcAiStates: aiStates,
			npcLifeStatsInitialize: npc => stagedLifeStats!.Initialize(npc, npc.Template.MaxHp),
			npcLifeStatsClear: objectId =>
			{
				stagedLifeStats!.Clear(objectId);
				stagedCombatStates.Clear(objectId);
				stagedCombatEvents.Clear(objectId);
				stagedCastingInterrupts.Clear(objectId);
			});
		var lootService = new WorldNpcLootService(dropRegistration, spawnService, threadPoolManager);
		var broadcastService = new WorldNpcLootBroadcastService(lootService, registry);
		var dropWorkflow = new WorldNpcDropRegistrationWorkflowService(
			new WorldNpcCustomDropService(new CustomNpcDropTable([])),
			dropRegistration,
			broadcastService);
		var deathWorkflow = new WorldNpcDeathDropWorkflowService(spawnService, dropWorkflow, aiStates);
		stagedLifeStats = new WorldNpcLifeStatsService(deathWorkflow);
		lifeStats = stagedLifeStats;
		return new WorldNpcDamageService(world, lifeStats, registry, combatStates, combatEvents, castingInterrupts);
	}

	private static void SpawnNpc(WorldNpcSpawnService spawnService, GameWorld world, int npcTemplateId, int maxHp)
	{
		var spawns = new NpcSpawnTable([CreateSpawn(210010000, npcTemplateId, respawnSeconds: 30)]);
		var templates = new NpcTemplateTable([CreateTemplate(npcTemplateId, maxHp)]);
		spawnService.SpawnWorldNpcs(spawns, templates, [210010000]);
		Assert.Single(world.GetNpcs());
	}

	private static WorldNpc CreateWorldNpc(int objectId, int maxHp)
	{
		return new WorldNpc(
			objectId,
			203094,
			CreateTemplate(203094, maxHp),
				new WorldPosition(210010000, 1, 2, 3, 0));
	}

	private static WorldNpc AddWorldNpc(GameWorld world, int objectId, int templateId, WorldPosition position)
	{
		var npc = new WorldNpc(
			objectId,
			templateId,
			CreateTemplate(templateId, maxHp: 100),
			position);
		Assert.True(world.TryAddObject(objectId, npc));
		return npc;
	}

	private static Player CreatePlayer()
	{
		return new Player { ObjectId = 1001, Race = "ELYOS", Level = 10 };
	}

	private static Player CreatePlayerWithObserverBurnItem(int charge, int polishCharge)
	{
		return new Player
		{
			ObjectId = 1001,
			Race = "ELYOS",
			Level = 10,
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 10,
					ItemId = 100,
					Count = 1,
					Location = 0,
					IsEquipped = true,
					Slot = 1,
					Charge = charge,
					IdianStone = new PlayerIdianStone(600, 1, polishCharge),
				},
			],
		};
	}

	private static ItemTemplateTable CreateObserverBurnItemTemplates()
	{
		return new ItemTemplateTable(
		[
			new ItemTemplateSummary(
				100,
				"item_100",
				0,
				1,
				1,
				"SWORD",
				"NORMAL",
				"COMMON",
				"PC_ALL",
				1,
				0,
				3,
				Improvement: new ItemImprovement(ChargeWay: 1, Level: 2, BurnAttack: 200, BurnDefend: 100, Price1: 1000, Price2: 2000),
				IdianInfo: new ItemIdianInfo(BurnAttack: 100_000, BurnDefend: 60_000)),
			new ItemTemplateSummary(
				600,
				"idian_600",
				0,
				1,
				1,
				"NONE",
				"NORMAL",
				"COMMON",
				"PC_ALL",
				1,
				0,
				0,
				PolishSetId: 12),
		]);
	}

	private static void AssertPolishChargePacket(GameServerPacket packet, int objectId, int polishCharge)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(objectId, reader.ReadD());
		Assert.Equal(string.Empty, reader.ReadS());
		Assert.Equal(5, reader.ReadH());
		Assert.Equal(0x11, (int)reader.ReadC());
		Assert.Equal(polishCharge, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertChargePacket(GameServerPacket packet, int objectId, int charge)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		Assert.Equal(objectId, reader.ReadD());
		Assert.Equal(string.Empty, reader.ReadS());
		Assert.Equal(5, reader.ReadH());
		Assert.Equal(0x0f, (int)reader.ReadC());
		Assert.Equal(charge, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static NpcSpawnSummary CreateSpawn(
		int mapId,
		int npcId,
		int respawnSeconds)
	{
		return new NpcSpawnSummary(
			mapId,
			npcId,
			X: 1,
			Y: 2,
			Z: 3,
			Heading: 0,
			RespawnSeconds: respawnSeconds,
			PoolSize: 0,
			DifficultId: 0,
			Handler: string.Empty,
			StaticId: 0,
			RandomWalkRange: 0,
			WalkerId: string.Empty,
			WalkerIndex: 0,
			Anchor: string.Empty,
			State: 0,
			AiName: string.Empty,
			Custom: false,
			GroupTemporarySchedule: null,
			SpotTemporarySchedule: null);
	}

	private static NpcTemplateSummary CreateTemplate(int templateId, int maxHp)
	{
		return new NpcTemplateSummary(
			templateId,
			$"npc-{templateId}",
			NameId: templateId,
			Level: 10,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "ELYOS",
			Tribe: "GENERAL",
			Type: "GENERAL",
			MaxHp: maxHp);
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<BroadcastRecord> Broadcasts { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = null;
			return false;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			return Task.FromResult(false);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null)
		{
			Broadcasts.Add(new BroadcastRecord(sourcePosition, sourceObjectId, packet, includeSourcePlayer));
			return Task.FromResult(1);
		}

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates)
		{
			return Task.FromResult(0);
		}

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail)
		{
			return Task.FromResult(false);
		}

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah)
		{
			return Task.FromResult(false);
		}
	}

	private sealed record BroadcastRecord(
		WorldPosition SourcePosition,
		int SourceObjectId,
		GameServerPacket Packet,
		bool IncludeSourcePlayer);
}
