using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;
using Aion.Commons.Network;

namespace Aion.GameServer.Tests;

public class PlayerSummonSkillExecutionServiceTests
{
	[Fact]
	public void EvaluateMercenaryNpcSkillConditionReadiness_ProjectsSimpleJavaConditionBranches()
	{
		var service = new PlayerSummonSkillExecutionService();
		var playerTarget = new PlayerSummonKnownObjectNpcSkillConditionTarget(
			PlayerSummonKnownObjectNpcSkillConditionTargetKind.Player,
			PlayerAbnormalState.Stun | PlayerAbnormalState.Poison,
			IsFlying: true,
			IsPhysicalClass: false,
			IsInRange: true);
		var npcTarget = new PlayerSummonKnownObjectNpcSkillConditionTarget(
			PlayerSummonKnownObjectNpcSkillConditionTargetKind.Npc,
			PlayerAbnormalState.Sleep,
			IsInRange: false);
		var gateTarget = new PlayerSummonKnownObjectNpcSkillConditionTarget(
			PlayerSummonKnownObjectNpcSkillConditionTargetKind.Gate);

		var ownerDead = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.None,
			playerTarget,
			ownerIsDead: true);
		var none = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.None,
			null);
		var missingTarget = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsStunned,
			null);
		var stunned = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsStunned,
			playerTarget);
		var sleepingNpc = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsSleeping,
			npcTarget);
		var bleeding = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsBleeding,
			playerTarget);
		var flying = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsFlying,
			playerTarget);
		var player = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsPlayer,
			playerTarget);
		var npc = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsNpc,
			npcTarget);
		var gate = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsGate,
			gateTarget);
		var magicalClass = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsMagicalClass,
			playerTarget);
		var physicalClass = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsPhysicalClass,
			playerTarget);
		var inRange = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsInRange,
			playerTarget);
		var outOfRange = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsInRange,
			npcTarget);
		var unsupported = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.HelpFriend,
			playerTarget);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.OwnerNotReady, ownerDead.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready, none.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.MissingTarget, missingTarget.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready, stunned.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready, sleepingNpc.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.NotReady, bleeding.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready, flying.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready, player.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready, npc.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready, gate.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready, magicalClass.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.NotReady, physicalClass.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Ready, inRange.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.NotReady, outOfRange.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.Unsupported, unsupported.Status);
	}

	[Fact]
	public void EvaluateMercenaryNpcSkillEntryReadiness_ProjectsJavaHpTimeCooldownAndConjunctions()
	{
		var service = new PlayerSummonSkillExecutionService();
		var defaultTiming = new PlayerSummonKnownObjectNpcSkillEntryTiming();
		var gatedTiming = new PlayerSummonKnownObjectNpcSkillEntryTiming(
			MinHpPercentage: 25,
			MaxHpPercentage: 75,
			MinTimeMilliseconds: 1_000,
			MaxTimeMilliseconds: 5_000);

		var defaultReady = service.EvaluateMercenaryNpcSkillEntryReadiness(
			defaultTiming,
			hpPercentage: 1,
			elapsedFightTimeMilliseconds: 0,
			currentTimeMilliseconds: 10_000);
		var onCooldown = service.EvaluateMercenaryNpcSkillEntryReadiness(
			defaultTiming with { CooldownMilliseconds = 5_000, LastTimeUsedMilliseconds = 8_000 },
			hpPercentage: 50,
			elapsedFightTimeMilliseconds: 2_000,
			currentTimeMilliseconds: 10_000);
		var chanceNotReady = service.EvaluateMercenaryNpcSkillEntryReadiness(
			defaultTiming,
			hpPercentage: 50,
			elapsedFightTimeMilliseconds: 2_000,
			currentTimeMilliseconds: 10_000,
			chanceReady: false);
		var andReady = service.EvaluateMercenaryNpcSkillEntryReadiness(
			gatedTiming,
			hpPercentage: 50,
			elapsedFightTimeMilliseconds: 2_000,
			currentTimeMilliseconds: 10_000);
		var andHpOutOfRange = service.EvaluateMercenaryNpcSkillEntryReadiness(
			gatedTiming,
			hpPercentage: 90,
			elapsedFightTimeMilliseconds: 2_000,
			currentTimeMilliseconds: 10_000);
		var minOnlyTimeReady = service.EvaluateMercenaryNpcSkillEntryReadiness(
			gatedTiming with { MaxTimeMilliseconds = 0 },
			hpPercentage: 50,
			elapsedFightTimeMilliseconds: 7_000,
			currentTimeMilliseconds: 10_000);
		var orReady = service.EvaluateMercenaryNpcSkillEntryReadiness(
			gatedTiming with { ConjunctionType = PlayerSummonKnownObjectNpcSkillConjunction.Or },
			hpPercentage: 90,
			elapsedFightTimeMilliseconds: 2_000,
			currentTimeMilliseconds: 10_000);
		var xorReady = service.EvaluateMercenaryNpcSkillEntryReadiness(
			gatedTiming with { ConjunctionType = PlayerSummonKnownObjectNpcSkillConjunction.Xor },
			hpPercentage: 90,
			elapsedFightTimeMilliseconds: 2_000,
			currentTimeMilliseconds: 10_000);
		var xorNotReady = service.EvaluateMercenaryNpcSkillEntryReadiness(
			gatedTiming with { ConjunctionType = PlayerSummonKnownObjectNpcSkillConjunction.Xor },
			hpPercentage: 50,
			elapsedFightTimeMilliseconds: 2_000,
			currentTimeMilliseconds: 10_000);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.Ready, defaultReady.Status);
		Assert.True(defaultReady.HpReady);
		Assert.True(defaultReady.TimeReady);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.OnCooldown, onCooldown.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.ChanceNotReady, chanceNotReady.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.Ready, andReady.Status);
		Assert.True(andReady.HpReady);
		Assert.True(andReady.TimeReady);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.NotReady, andHpOutOfRange.Status);
		Assert.False(andHpOutOfRange.HpReady);
		Assert.True(andHpOutOfRange.TimeReady);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.Ready, minOnlyTimeReady.Status);
		Assert.True(minOnlyTimeReady.TimeReady);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.Ready, orReady.Status);
		Assert.False(orReady.HpReady);
		Assert.True(orReady.TimeReady);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.Ready, xorReady.Status);
		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.NotReady, xorNotReady.Status);
		Assert.True(xorNotReady.HpReady);
		Assert.True(xorNotReady.TimeReady);
	}

	[Fact]
	public void ApplyMercenaryTargetRangeDelay_ProjectsJavaTargetTooFarDelay()
	{
		var service = new PlayerSummonSkillExecutionService();
		var player = new Player
		{
			ObjectId = 1,
		};
		var knownObject = new PlayerSummonKnownObject(
			ObjectId: 8002,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary);
		player.SetSummonKnownObject(knownObject);

		var notRequired = service.EvaluateMercenaryTargetRange(
			knownObject,
			requiresCreatureTargetCheck: false,
			hasCreatureTarget: false);
		var noneTarget = service.EvaluateMercenaryTargetRange(
			knownObject,
			PlayerSummonKnownObjectSkillTargetMode.None,
			hasCreatureTarget: false);
		var mostHatedTarget = service.EvaluateMercenaryTargetRange(
			knownObject,
			PlayerSummonKnownObjectSkillTargetMode.MostHated,
			hasCreatureTarget: false);
		var selfTarget = service.EvaluateMercenaryTargetRange(
			knownObject,
			PlayerSummonKnownObjectSkillTargetMode.Self,
			hasCreatureTarget: false);
		var areaSkillReady = service.EvaluateMercenaryTargetRange(
			knownObject,
			PlayerSummonKnownObjectSkillTargetMode.CreatureTarget,
			hasCreatureTarget: true,
			isAreaTarget: true,
			isInRange: false);
		var missingCreatureTarget = service.EvaluateMercenaryTargetRange(
			knownObject,
			PlayerSummonKnownObjectSkillTargetMode.CreatureTarget,
			hasCreatureTarget: false);
		var deadTarget = service.EvaluateMercenaryTargetRange(
			knownObject,
			PlayerSummonKnownObjectSkillTargetMode.CreatureTarget,
			hasCreatureTarget: true,
			targetIsDead: true);
		var unseenTarget = service.EvaluateMercenaryTargetRange(
			knownObject,
			PlayerSummonKnownObjectSkillTargetMode.CreatureTarget,
			hasCreatureTarget: true,
			canSeeTarget: false);
		var outOfRange = service.EvaluateMercenaryTargetRange(
			knownObject,
			PlayerSummonKnownObjectSkillTargetMode.CreatureTarget,
			hasCreatureTarget: true,
			isInRange: false);

		var missingEvaluation = service.ApplyMercenaryTargetRangeDelay(player, mercenaryObjectId: 8002, null);
		var noDelay = service.ApplyMercenaryTargetRangeDelay(player, mercenaryObjectId: 8002, areaSkillReady);
		var missingKnownObject = service.ApplyMercenaryTargetRangeDelay(new Player { ObjectId = 1 }, mercenaryObjectId: 8002, outOfRange);
		var delayed = service.ApplyMercenaryTargetRangeDelay(player, mercenaryObjectId: 8002, outOfRange);

		Assert.Equal(PlayerSummonKnownObjectTargetRangeReadinessStatus.NotRequired, notRequired.Status);
		Assert.False(notRequired.ShouldSetNextSkillDelay);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeReadinessStatus.NotRequired, noneTarget.Status);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeReadinessStatus.NotRequired, mostHatedTarget.Status);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeReadinessStatus.NotRequired, selfTarget.Status);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeReadinessStatus.Ready, areaSkillReady.Status);
		Assert.False(areaSkillReady.ShouldSetNextSkillDelay);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeReadinessStatus.MissingCreatureTarget, missingCreatureTarget.Status);
		Assert.Equal(5_000, missingCreatureTarget.NextSkillDelayMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeReadinessStatus.TargetDead, deadTarget.Status);
		Assert.Equal(5_000, deadTarget.NextSkillDelayMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeReadinessStatus.CannotSeeTarget, unseenTarget.Status);
		Assert.Equal(5_000, unseenTarget.NextSkillDelayMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeReadinessStatus.TargetOutOfRange, outOfRange.Status);
		Assert.Equal(5_000, outOfRange.NextSkillDelayMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeDelayStatus.MissingRangeEvaluation, missingEvaluation.Status);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeDelayStatus.NotRequired, noDelay.Status);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeDelayStatus.MissingKnownObject, missingKnownObject.Status);
		Assert.Equal(PlayerSummonKnownObjectTargetRangeDelayStatus.Set, delayed.Status);
		Assert.Equal(5_000, delayed.StoredDelayMilliseconds);
		Assert.True(player.TryGetSummonKnownObject(8002, out var storedKnownObject));
		Assert.Equal(5_000, storedKnownObject.NextSkillDelayMilliseconds);
	}

	[Fact]
	public void EvaluateMercenarySkillReadiness_ProjectsJavaAbnormalAndTransformGates()
	{
		var service = new PlayerSummonSkillExecutionService();
		var knownObject = new PlayerSummonKnownObject(
			ObjectId: 8002,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary);
		var magicalSkill = CreateSkillTemplate(skillType: "MAGICAL");
		var physicalSkill = CreateSkillTemplate(skillType: "PHYSICAL");

		var timingNotReady = service.EvaluateMercenarySkillReadiness(knownObject, magicalSkill, entryTimingReady: false);
		var conditionNotReady = service.EvaluateMercenarySkillReadiness(knownObject, magicalSkill, entryConditionReady: false);
		var missingTemplate = service.EvaluateMercenarySkillReadiness(knownObject, null);
		var silencedMagical = service.EvaluateMercenarySkillReadiness(
			knownObject with { AbnormalState = PlayerAbnormalState.Silence },
			magicalSkill);
		var boundPhysical = service.EvaluateMercenarySkillReadiness(
			knownObject with { AbnormalState = PlayerAbnormalState.Bind },
			physicalSkill);
		var stunnedMagical = service.EvaluateMercenarySkillReadiness(
			knownObject with { AbnormalState = PlayerAbnormalState.Stun },
			magicalSkill);
		var transformedBan = service.EvaluateMercenarySkillReadiness(
			knownObject with { IsTransformed = true, TransformBansSkillUse = true },
			physicalSkill);
		var ready = service.EvaluateMercenarySkillReadiness(knownObject, magicalSkill);

		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.EntryTimingNotReady, timingNotReady.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.EntryConditionNotReady, conditionNotReady.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.MissingSkillTemplate, missingTemplate.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.BlockedBySilence, silencedMagical.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.BlockedByBind, boundPhysical.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.BlockedByCantAttackState, stunnedMagical.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.BlockedByTransformSkillBan, transformedBan.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.Ready, ready.Status);
		Assert.Same(magicalSkill, ready.SkillTemplate);
	}

	[Fact]
	public void EvaluateMercenarySkillReadiness_ConsumesTypedNpcSkillEntryTiming()
	{
		var service = new PlayerSummonSkillExecutionService();
		var knownObject = new PlayerSummonKnownObject(
			ObjectId: 8002,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary);
		var magicalSkill = CreateSkillTemplate(skillType: "MAGICAL");
		var timing = new PlayerSummonKnownObjectNpcSkillEntryTiming(
			MinHpPercentage: 25,
			MaxHpPercentage: 75,
			MinTimeMilliseconds: 1_000,
			MaxTimeMilliseconds: 5_000);
		var blockedTiming = service.EvaluateMercenaryNpcSkillEntryReadiness(
			timing,
			hpPercentage: 90,
			elapsedFightTimeMilliseconds: 2_000,
			currentTimeMilliseconds: 10_000);
		var readyTiming = service.EvaluateMercenaryNpcSkillEntryReadiness(
			timing,
			hpPercentage: 50,
			elapsedFightTimeMilliseconds: 2_000,
			currentTimeMilliseconds: 10_000);

		var blocked = service.EvaluateMercenarySkillReadiness(knownObject, magicalSkill, blockedTiming);
		var ready = service.EvaluateMercenarySkillReadiness(knownObject, magicalSkill, readyTiming);
		var conditionBlocked = service.EvaluateMercenarySkillReadiness(
			knownObject,
			magicalSkill,
			readyTiming,
			entryConditionReady: false);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillEntryReadinessStatus.NotReady, blockedTiming.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.EntryTimingNotReady, blocked.Status);
		Assert.Same(blockedTiming, blocked.EntryTimingReadiness);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.Ready, ready.Status);
		Assert.Same(readyTiming, ready.EntryTimingReadiness);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.EntryConditionNotReady, conditionBlocked.Status);
		Assert.Same(readyTiming, conditionBlocked.EntryTimingReadiness);
	}

	[Fact]
	public void EvaluateMercenarySkillReadiness_ConsumesTypedNpcSkillConditionReadiness()
	{
		var service = new PlayerSummonSkillExecutionService();
		var knownObject = new PlayerSummonKnownObject(
			ObjectId: 8002,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary);
		var magicalSkill = CreateSkillTemplate(skillType: "MAGICAL");
		var readyTiming = service.EvaluateMercenaryNpcSkillEntryReadiness(
			new PlayerSummonKnownObjectNpcSkillEntryTiming(),
			hpPercentage: 50,
			elapsedFightTimeMilliseconds: 0,
			currentTimeMilliseconds: 10_000);
		var stunnedTarget = new PlayerSummonKnownObjectNpcSkillConditionTarget(
			PlayerSummonKnownObjectNpcSkillConditionTargetKind.Player,
			PlayerAbnormalState.Stun);
		var readyCondition = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsStunned,
			stunnedTarget);
		var blockedCondition = service.EvaluateMercenaryNpcSkillConditionReadiness(
			PlayerSummonKnownObjectNpcSkillCondition.TargetIsBleeding,
			stunnedTarget);

		var blocked = service.EvaluateMercenarySkillReadiness(
			knownObject,
			magicalSkill,
			readyTiming,
			blockedCondition);
		var ready = service.EvaluateMercenarySkillReadiness(
			knownObject,
			magicalSkill,
			readyTiming,
			readyCondition);

		Assert.Equal(PlayerSummonKnownObjectNpcSkillConditionReadinessStatus.NotReady, blockedCondition.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.EntryConditionNotReady, blocked.Status);
		Assert.Same(readyTiming, blocked.EntryTimingReadiness);
		Assert.Same(blockedCondition, blocked.EntryConditionReadiness);
		Assert.Equal(PlayerSummonKnownObjectSkillReadinessStatus.Ready, ready.Status);
		Assert.Same(readyTiming, ready.EntryTimingReadiness);
		Assert.Same(readyCondition, ready.EntryConditionReadiness);
	}

	[Fact]
	public void PreviewMercenarySkillAttack_ProjectsSkillAttackManagerGates()
	{
		var service = new PlayerSummonSkillExecutionService();
		var knownObject = new PlayerSummonKnownObject(
			ObjectId: 8002,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary,
			LastSkillTimeMilliseconds: 10_000,
			NextSkillDelayMilliseconds: 5_000);

		var casting = service.PreviewMercenarySkillAttack(
			knownObject,
			fightStartingTimeMilliseconds: 10_000,
			initialSkillDelayMilliseconds: 1_000,
			currentTimeMilliseconds: 20_000,
			isCasting: true);
		var queuedInstant = service.PreviewMercenarySkillAttack(
			knownObject,
			fightStartingTimeMilliseconds: 10_000,
			initialSkillDelayMilliseconds: 20_000,
			currentTimeMilliseconds: 10_001,
			isCasting: false,
			hasReadyQueuedInstantSkill: true);
		var initialDelay = service.PreviewMercenarySkillAttack(
			knownObject,
			fightStartingTimeMilliseconds: 10_000,
			initialSkillDelayMilliseconds: 1_000,
			currentTimeMilliseconds: 11_000,
			isCasting: false);
		var nextSkillNotReady = service.PreviewMercenarySkillAttack(
			knownObject,
			fightStartingTimeMilliseconds: 10_000,
			initialSkillDelayMilliseconds: 1_000,
			currentTimeMilliseconds: 14_999,
			isCasting: false);
		var wouldEvaluate = service.PreviewMercenarySkillAttack(
			knownObject,
			fightStartingTimeMilliseconds: 10_000,
			initialSkillDelayMilliseconds: 1_000,
			currentTimeMilliseconds: 15_000,
			isCasting: false);
		var defaultDelayReady = service.PreviewMercenarySkillAttack(
			knownObject with { NextSkillDelayMilliseconds = null },
			fightStartingTimeMilliseconds: 10_000,
			initialSkillDelayMilliseconds: 1_000,
			currentTimeMilliseconds: 11_001,
			isCasting: false);

		Assert.Equal(PlayerSummonKnownObjectSkillAttackPreviewStatus.BlockedCasting, casting.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillAttackPreviewStatus.WouldUseQueuedInstantSkill, queuedInstant.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillAttackPreviewStatus.InitialDelayNotElapsed, initialDelay.Status);
		Assert.Equal(1_000, initialDelay.ElapsedFightTimeMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectSkillAttackPreviewStatus.NextSkillNotReady, nextSkillNotReady.Status);
		Assert.Equal(PlayerSummonKnownObjectNextSkillReadinessStatus.NotReady, nextSkillNotReady.Readiness?.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillAttackPreviewStatus.WouldEvaluateSkills, wouldEvaluate.Status);
		Assert.Equal(PlayerSummonKnownObjectNextSkillReadinessStatus.Ready, wouldEvaluate.Readiness?.Status);
		Assert.Equal(PlayerSummonKnownObjectSkillAttackPreviewStatus.WouldEvaluateSkills, defaultDelayReady.Status);
		Assert.Equal(0, defaultDelayReady.Readiness?.NextSkillDelayMilliseconds);
	}

	[Fact]
	public void SetMercenaryNextSkillDelay_StoresConcreteDelayAndRejectsRandomSentinel()
	{
		var service = new PlayerSummonSkillExecutionService();
		var player = new Player
		{
			ObjectId = 1,
		};
		player.SetSummonKnownObject(new PlayerSummonKnownObject(
			ObjectId: 8002,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary));

		var missingKnownObject = service.SetMercenaryNextSkillDelay(player, mercenaryObjectId: 9003, nextSkillDelayMilliseconds: 5_000);
		var randomDelay = service.SetMercenaryNextSkillDelay(player, mercenaryObjectId: 8002, nextSkillDelayMilliseconds: -1);
		var zeroDelay = service.SetMercenaryNextSkillDelay(player, mercenaryObjectId: 8002, nextSkillDelayMilliseconds: 0);
		var concreteDelay = service.SetMercenaryNextSkillDelay(player, mercenaryObjectId: 8002, nextSkillDelayMilliseconds: 5_000);

		Assert.Equal(PlayerSummonKnownObjectNextSkillDelayStatus.MissingKnownObject, missingKnownObject.Status);
		Assert.Null(missingKnownObject.StoredDelayMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectNextSkillDelayStatus.RandomDelayUnsupported, randomDelay.Status);
		Assert.Null(randomDelay.StoredDelayMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectNextSkillDelayStatus.Set, zeroDelay.Status);
		Assert.Equal(0, zeroDelay.StoredDelayMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectNextSkillDelayStatus.Set, concreteDelay.Status);
		Assert.Equal(5_000, concreteDelay.StoredDelayMilliseconds);
		Assert.True(player.TryGetSummonKnownObject(8002, out var knownObject));
		Assert.Equal(5_000, knownObject.NextSkillDelayMilliseconds);
	}

	[Fact]
	public void EvaluateMercenaryNextSkillReadiness_ProjectsJavaDelayCheck()
	{
		var service = new PlayerSummonSkillExecutionService();
		var knownObject = new PlayerSummonKnownObject(
			ObjectId: 8002,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary,
			LastSkillTimeMilliseconds: 10_000);
		var noDelay = service.EvaluateMercenaryNextSkillReadiness(
			knownObject,
			nextSkillDelayMilliseconds: 0,
			currentTimeMilliseconds: 10_001);
		var notReady = service.EvaluateMercenaryNextSkillReadiness(
			knownObject,
			nextSkillDelayMilliseconds: 5_000,
			currentTimeMilliseconds: 14_999);
		var ready = service.EvaluateMercenaryNextSkillReadiness(
			knownObject,
			nextSkillDelayMilliseconds: 5_000,
			currentTimeMilliseconds: 15_000);
		var defaultLastSkillTime = service.EvaluateMercenaryNextSkillReadiness(
			knownObject with { LastSkillTimeMilliseconds = null },
			nextSkillDelayMilliseconds: 5_000,
			currentTimeMilliseconds: 4_999);
		var randomDelay = service.EvaluateMercenaryNextSkillReadiness(
			knownObject,
			nextSkillDelayMilliseconds: -1,
			currentTimeMilliseconds: 15_000);

		Assert.Equal(PlayerSummonKnownObjectNextSkillReadinessStatus.Ready, noDelay.Status);
		Assert.Null(noDelay.ReadyAtMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectNextSkillReadinessStatus.NotReady, notReady.Status);
		Assert.Equal(15_000, notReady.ReadyAtMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectNextSkillReadinessStatus.Ready, ready.Status);
		Assert.Equal(15_000, ready.ReadyAtMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectNextSkillReadinessStatus.NotReady, defaultLastSkillTime.Status);
		Assert.Equal(5_000, defaultLastSkillTime.ReadyAtMilliseconds);
		Assert.Equal(PlayerSummonKnownObjectNextSkillReadinessStatus.RandomDelayUnsupported, randomDelay.Status);
	}

	[Fact]
	public void RenewMercenaryLastSkillTime_UpdatesRepresentedKnownObject()
	{
		var service = new PlayerSummonSkillExecutionService();
		var player = new Player
		{
			ObjectId = 1,
		};
		player.SetSummonKnownObject(new PlayerSummonKnownObject(
			ObjectId: 8002,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary));
		var mercenaryPlan = new PlayerSummonSkillInvocationPlan(
			PlayerSummonSkillInvocationActorKind.Mercenary,
			ActorObjectId: 8002,
			ActorTemplateId: 833288,
			SkillId: 22107,
			SkillLevel: 1,
			Target: new PlayerSummonCastSpellTarget(8002, PlayerSummonKnownObjectKind.Creature, IsActorSelfTarget: true),
			Hate: 0,
			ReleaseOnSuccess: false);
		var summonPlan = mercenaryPlan with
		{
			ActorKind = PlayerSummonSkillInvocationActorKind.Summon,
			ActorObjectId = 8001,
			Hate = 5,
			ReleaseOnSuccess = true,
		};
		var mercenaryExecution = PlayerSummonSkillInvocationExecutionResult.WouldUseSkill(
			mercenaryPlan,
			skillTemplateId: 22107,
			skillCooldownId: 0);
		var summonExecution = PlayerSummonSkillInvocationExecutionResult.WouldUseSkill(
			summonPlan,
			skillTemplateId: 22107,
			skillCooldownId: 0);

		var missingExecution = service.RenewMercenaryLastSkillTime(player, null, currentTimeMilliseconds: 12_000);
		var notRenewable = service.RenewMercenaryLastSkillTime(player, summonExecution, currentTimeMilliseconds: 12_000);
		var missingKnownObject = service.RenewMercenaryLastSkillTime(new Player { ObjectId = 1 }, mercenaryExecution, currentTimeMilliseconds: 12_000);
		var renewed = service.RenewMercenaryLastSkillTime(player, mercenaryExecution, currentTimeMilliseconds: 12_345);

		Assert.Equal(PlayerSummonKnownObjectLastSkillTimeRenewalStatus.MissingExecution, missingExecution.Status);
		Assert.Equal(PlayerSummonKnownObjectLastSkillTimeRenewalStatus.NotRenewable, notRenewable.Status);
		Assert.Equal(PlayerSummonKnownObjectLastSkillTimeRenewalStatus.MissingKnownObject, missingKnownObject.Status);
		Assert.Equal(PlayerSummonKnownObjectLastSkillTimeRenewalStatus.Renewed, renewed.Status);
		Assert.Equal(12_345, renewed.LastSkillTimeMilliseconds);
		Assert.True(player.TryGetSummonKnownObject(8002, out var knownObject));
		Assert.Equal(12_345, knownObject.LastSkillTimeMilliseconds);
	}

	[Fact]
	public void PreviewInvocationUse_GatesReleaseOnSuccessfulSkillUse()
	{
		var service = new PlayerSummonSkillExecutionService();
		var target = new PlayerSummonCastSpellTarget(7001, PlayerSummonKnownObjectKind.Creature, IsActorSelfTarget: false);
		var releasePlan = new PlayerSummonSkillInvocationPlan(
			PlayerSummonSkillInvocationActorKind.Summon,
			ActorObjectId: 8001,
			ActorTemplateId: 833288,
			SkillId: 22107,
			SkillLevel: 1,
			target,
			Hate: 5,
			ReleaseOnSuccess: true);
		var noReleasePlan = releasePlan with { ReleaseOnSuccess = false };
		var mercenaryPlan = releasePlan with
		{
			ActorKind = PlayerSummonSkillInvocationActorKind.Mercenary,
			ActorObjectId = 8002,
			Hate = 0,
			ReleaseOnSuccess = false,
		};

		var missingExecution = service.PreviewInvocationUse(null, skillUseSucceeded: true);
		var notReady = service.PreviewInvocationUse(
			PlayerSummonSkillInvocationExecutionResult.MissingSkillTemplate(releasePlan),
			skillUseSucceeded: true);
		var failedUse = service.PreviewInvocationUse(
			PlayerSummonSkillInvocationExecutionResult.WouldUseSkill(releasePlan, skillTemplateId: 22107, skillCooldownId: 0),
			skillUseSucceeded: false);
		var wouldRelease = service.PreviewInvocationUse(
			PlayerSummonSkillInvocationExecutionResult.WouldUseSkill(releasePlan, skillTemplateId: 22107, skillCooldownId: 0),
			skillUseSucceeded: true);
		var wouldNotRelease = service.PreviewInvocationUse(
			PlayerSummonSkillInvocationExecutionResult.WouldUseSkill(noReleasePlan, skillTemplateId: 22107, skillCooldownId: 0),
			skillUseSucceeded: true);
		var mercenaryUse = service.PreviewInvocationUse(
			PlayerSummonSkillInvocationExecutionResult.WouldUseSkill(mercenaryPlan, skillTemplateId: 22107, skillCooldownId: 0),
			skillUseSucceeded: true);

		Assert.Equal(PlayerSummonSkillInvocationUseStatus.MissingExecution, missingExecution.Status);
		Assert.Equal(PlayerSummonSkillInvocationUseStatus.NotReadyToUseSkill, notReady.Status);
		Assert.Equal(PlayerSummonSkillInvocationUseStatus.SkillUseFailed, failedUse.Status);
		Assert.False(failedUse.ShouldReleaseSummon);
		Assert.Equal(PlayerSummonSkillInvocationUseStatus.WouldReleaseSummon, wouldRelease.Status);
		Assert.True(wouldRelease.SkillUseSucceeded);
		Assert.True(wouldRelease.ShouldReleaseSummon);
		Assert.Equal(PlayerSummonSkillInvocationUseStatus.WouldCompleteWithoutRelease, wouldNotRelease.Status);
		Assert.True(wouldNotRelease.SkillUseSucceeded);
		Assert.False(wouldNotRelease.ShouldReleaseSummon);
		Assert.Equal(PlayerSummonSkillInvocationUseStatus.WouldCompleteWithoutRelease, mercenaryUse.Status);
		Assert.False(mercenaryUse.ShouldReleaseSummon);
	}

	[Fact]
	public async Task ValidateExecution_AllowsPetSkillBeforeRepresentedSkillEngineInvocation()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var player = new Player
		{
			HasPetSummon = true,
			PetSummonObjectId = 8001,
			PetSummonNpcId = 833288,
		};
		var order = new PlayerPetSkillOrder(22107, SkillLevel: 1, TargetObjectId: 7001, Hate: 5, Release: true);

		var service = new PlayerSummonSkillExecutionService();
		var result = service.ValidateExecution(
			player,
			order,
			dataManager.StaticData.PetSkills,
			new PlayerSummonCastSpellTarget(7001, PlayerSummonKnownObjectKind.Creature, IsActorSelfTarget: false));
		var invocationExecution = service.PlanInvocationExecution(result.InvocationPlan, dataManager.StaticData.SkillTemplates);

		Assert.Equal(PlayerSummonSkillExecutionStatus.WouldInvokeSkillEngine, result.Status);
		Assert.Equal(833288, result.PetSummonNpcId);
		Assert.Same(order, result.Order);
		Assert.Equal(7001, result.ResolvedTarget?.ObjectId);
		Assert.False(result.ResolvedTarget?.IsActorSelfTarget);
		Assert.Equal(PlayerSummonSkillInvocationActorKind.Summon, result.InvocationPlan?.ActorKind);
		Assert.Equal(8001, result.InvocationPlan?.ActorObjectId);
		Assert.Equal(833288, result.InvocationPlan?.ActorTemplateId);
		Assert.Equal(22107, result.InvocationPlan?.SkillId);
		Assert.Equal(1, result.InvocationPlan?.SkillLevel);
		Assert.Equal(7001, result.InvocationPlan?.Target?.ObjectId);
		Assert.Equal(5, result.InvocationPlan?.Hate);
		Assert.True(result.InvocationPlan?.ReleaseOnSuccess);
		Assert.Equal(PlayerSummonSkillInvocationExecutionStatus.WouldUseSkill, invocationExecution.Status);
		Assert.Equal(22107, invocationExecution.SkillTemplateId);
		Assert.Equal(result.InvocationPlan, invocationExecution.InvocationPlan);
		Assert.Equal(
			[
				PlayerSummonSkillInvocationExecutionAction.ResolveSkillTemplate,
				PlayerSummonSkillInvocationExecutionAction.SetHate,
				PlayerSummonSkillInvocationExecutionAction.UseSkill,
				PlayerSummonSkillInvocationExecutionAction.ReleaseOnSuccessfulUse,
			],
			invocationExecution.Actions);
		Assert.True(result.Order.Release);
		Assert.Equal(5, result.Order.Hate);
		Assert.Equal(
			[
				PlayerSummonSkillExecutionAction.GetSkill,
				PlayerSummonSkillExecutionAction.SetHate,
				PlayerSummonSkillExecutionAction.UseSkill,
				PlayerSummonSkillExecutionAction.ReleaseOnSuccess,
			],
			result.Actions);
	}

	[Fact]
	public async Task ValidateExecution_PlansNoReleaseWhenQueuedOrderDoesNotRelease()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var player = new Player
		{
			HasPetSummon = true,
			PetSummonObjectId = 8001,
			PetSummonNpcId = 833288,
		};

		var service = new PlayerSummonSkillExecutionService();
		var result = service.ValidateExecution(
			player,
			new PlayerPetSkillOrder(22107, SkillLevel: 1, TargetObjectId: 7001, Hate: 0, Release: false),
			dataManager.StaticData.PetSkills,
			new PlayerSummonCastSpellTarget(7001, PlayerSummonKnownObjectKind.Creature, IsActorSelfTarget: false));
		var invocationExecution = service.PlanInvocationExecution(result.InvocationPlan, dataManager.StaticData.SkillTemplates);
		var missingTemplate = service.PlanInvocationExecution(result.InvocationPlan, new SkillTemplateTable([]));

		Assert.Equal(PlayerSummonSkillExecutionStatus.WouldInvokeSkillEngine, result.Status);
		Assert.Equal(7001, result.ResolvedTarget?.ObjectId);
		Assert.Equal(PlayerSummonSkillInvocationActorKind.Summon, result.InvocationPlan?.ActorKind);
		Assert.Equal(8001, result.InvocationPlan?.ActorObjectId);
		Assert.Equal(1, result.InvocationPlan?.SkillLevel);
		Assert.False(result.InvocationPlan?.ReleaseOnSuccess);
		Assert.Equal(PlayerSummonSkillInvocationExecutionStatus.WouldUseSkill, invocationExecution.Status);
		Assert.Equal(
			[
				PlayerSummonSkillInvocationExecutionAction.ResolveSkillTemplate,
				PlayerSummonSkillInvocationExecutionAction.SetHate,
				PlayerSummonSkillInvocationExecutionAction.UseSkill,
			],
			invocationExecution.Actions);
		Assert.Equal(PlayerSummonSkillInvocationExecutionStatus.MissingSkillTemplate, missingTemplate.Status);
		Assert.Empty(missingTemplate.Actions);
		Assert.Equal(
			[
				PlayerSummonSkillExecutionAction.GetSkill,
				PlayerSummonSkillExecutionAction.SetHate,
				PlayerSummonSkillExecutionAction.UseSkill,
			],
			result.Actions);
	}

	[Fact]
	public async Task ValidateExecution_RejectsMissingSummonAndInvalidPetSkill()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var service = new PlayerSummonSkillExecutionService();
		var order = new PlayerPetSkillOrder(22107, SkillLevel: 1, TargetObjectId: 7001, Hate: 0, Release: false);

		var resolvedTarget = new PlayerSummonCastSpellTarget(7001, PlayerSummonKnownObjectKind.Creature, IsActorSelfTarget: false);
		var missingSummon = service.ValidateExecution(new Player(), order, dataManager.StaticData.PetSkills, resolvedTarget);
		var invalidSkill = service.ValidateExecution(
			new Player
			{
				HasPetSummon = true,
				PetSummonNpcId = 833288,
			},
			order with { SkillId = 9999 },
			dataManager.StaticData.PetSkills,
			resolvedTarget);
		var missingPlan = service.PlanInvocationExecution(invalidSkill.InvocationPlan, dataManager.StaticData.SkillTemplates);

		Assert.Equal(PlayerSummonSkillExecutionStatus.MissingSummon, missingSummon.Status);
		Assert.Equal(PlayerSummonSkillExecutionStatus.InvalidPetSkill, invalidSkill.Status);
		Assert.Equal(9999, invalidSkill.Order.SkillId);
		Assert.Equal(7001, missingSummon.ResolvedTarget?.ObjectId);
		Assert.Equal(7001, invalidSkill.ResolvedTarget?.ObjectId);
		Assert.Null(missingSummon.InvocationPlan);
		Assert.Null(invalidSkill.InvocationPlan);
		Assert.Equal(PlayerSummonSkillInvocationExecutionStatus.MissingPlan, missingPlan.Status);
		Assert.Empty(missingSummon.Actions);
		Assert.Empty(invalidSkill.Actions);
	}

	[Fact]
	public async Task ValidateMercenaryExecution_PlansControllerUseAndAuditsInvalidSkill()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var service = new PlayerSummonSkillExecutionService();
		var player = new Player
		{
			ObjectId = 1,
		};
		player.SetSummonKnownObject(new PlayerSummonKnownObject(
			ObjectId: 8002,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary));

		var valid = service.ValidateMercenaryExecution(
			player,
			CreateSummonCastSpell(summonObjectId: 8002, skillId: 22107, skillLevel: 1, targetObjectId: 8002),
			dataManager.StaticData.PetSkills,
			new PlayerSummonCastSpellTarget(8002, PlayerSummonKnownObjectKind.Creature, IsActorSelfTarget: true));
		var invocationExecution = service.PlanInvocationExecution(valid.InvocationPlan, dataManager.StaticData.SkillTemplates, player);
		var invalid = service.ValidateMercenaryExecution(
			player,
			CreateSummonCastSpell(summonObjectId: 8002, skillId: 9999, skillLevel: 1, targetObjectId: 8002),
			dataManager.StaticData.PetSkills,
			new PlayerSummonCastSpellTarget(8002, PlayerSummonKnownObjectKind.Creature, IsActorSelfTarget: true));

		Assert.Equal(PlayerMercenarySkillExecutionStatus.WouldInvokeController, valid.Status);
		Assert.Equal(8002, valid.ResolvedTarget?.ObjectId);
		Assert.True(valid.ResolvedTarget?.IsActorSelfTarget);
		Assert.Equal(PlayerSummonSkillInvocationActorKind.Mercenary, valid.InvocationPlan?.ActorKind);
		Assert.Equal(8002, valid.InvocationPlan?.ActorObjectId);
		Assert.Equal(833288, valid.InvocationPlan?.ActorTemplateId);
		Assert.Equal(22107, valid.InvocationPlan?.SkillId);
		Assert.Equal(1, valid.InvocationPlan?.SkillLevel);
		Assert.Equal(8002, valid.InvocationPlan?.Target?.ObjectId);
		Assert.Equal(0, valid.InvocationPlan?.Hate);
		Assert.False(valid.InvocationPlan?.ReleaseOnSuccess);
		Assert.Equal(PlayerSummonSkillInvocationExecutionStatus.WouldUseSkill, invocationExecution.Status);
		Assert.True(invocationExecution.WouldRenewLastSkillTime);
		Assert.Equal(
			[
				PlayerSummonSkillInvocationExecutionAction.SetTarget,
				PlayerSummonSkillInvocationExecutionAction.ResolveSkillTemplate,
				PlayerSummonSkillInvocationExecutionAction.RenewLastSkillTime,
				PlayerSummonSkillInvocationExecutionAction.UseSkill,
			],
			invocationExecution.Actions);
		Assert.Equal(
			[
				PlayerMercenarySkillExecutionAction.SetTarget,
				PlayerMercenarySkillExecutionAction.UseSkill,
			],
			valid.Actions);
		Assert.Equal(PlayerMercenarySkillExecutionStatus.InvalidMercenarySkill, invalid.Status);
		Assert.Equal(8002, invalid.ResolvedTarget?.ObjectId);
		Assert.Null(invalid.InvocationPlan);
		var audit = Assert.IsType<PlayerMercenarySkillExecutionAudit>(invalid.Audit);
		Assert.Equal(PlayerMercenarySkillExecutionAuditKind.InvalidMercenarySkill, audit.Kind);
		Assert.Empty(invalid.Actions);
	}

	[Fact]
	public async Task PlanInvocationExecution_BlocksDisabledMercenarySkillBeforeControllerUse()
	{
		var dataManager = await DataManager.LoadAsync(FindRepoRoot(), validateWhenCacheChanges: false);
		var service = new PlayerSummonSkillExecutionService();
		var skillTemplate = dataManager.StaticData.SkillTemplates.GetSkillTemplate(22107)
			?? throw new InvalidOperationException("Expected test skill template.");
		var player = new Player
		{
			ObjectId = 1,
		};
		player.SetSummonKnownObject(new PlayerSummonKnownObject(
			ObjectId: 8002,
			Kind: PlayerSummonKnownObjectKind.Creature,
			CreatorObjectId: 1,
			NpcTemplateId: 833288,
			NpcTemplateType: PlayerSummonKnownNpcTemplateType.Mercenary,
			DisabledSkillCooldownIds: new HashSet<int> { skillTemplate.CooldownId }));
		var valid = service.ValidateMercenaryExecution(
			player,
			CreateSummonCastSpell(summonObjectId: 8002, skillId: 22107, skillLevel: 1, targetObjectId: 8002),
			dataManager.StaticData.PetSkills,
			new PlayerSummonCastSpellTarget(8002, PlayerSummonKnownObjectKind.Creature, IsActorSelfTarget: true));

		var invocationExecution = service.PlanInvocationExecution(valid.InvocationPlan, dataManager.StaticData.SkillTemplates, player);

		Assert.Equal(PlayerMercenarySkillExecutionStatus.WouldInvokeController, valid.Status);
		Assert.Equal(PlayerSummonSkillInvocationExecutionStatus.DisabledNpcSkill, invocationExecution.Status);
		Assert.Equal(22107, invocationExecution.SkillTemplateId);
		Assert.Equal(skillTemplate.CooldownId, invocationExecution.SkillCooldownId);
		Assert.False(invocationExecution.WouldRenewLastSkillTime);
		Assert.Empty(invocationExecution.Actions);
	}

	private static string FindRepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "game-server")))
			directory = directory.Parent;

		return directory?.FullName ?? throw new InvalidOperationException("Unable to locate repository root.");
	}

	private static SkillTemplateSummary CreateSkillTemplate(string skillType)
	{
		return new SkillTemplateSummary(
			SkillId: 22107,
			Name: "summon_skill",
			NameId: 0,
			Level: 1,
			Group: "",
			Stack: "",
			SkillType: skillType,
			SkillSubType: "",
			CooldownId: 0,
			Cooldown: 0);
	}

	private static CmSummonCastSpell CreateSummonCastSpell(int summonObjectId, int skillId, int skillLevel, int targetObjectId)
	{
		var packet = new CmSummonCastSpell(205, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteD(summonObjectId);
		buffer.WriteH(skillId);
		buffer.WriteC(skillLevel);
		buffer.WriteD(targetObjectId);
		buffer.WriteD(0);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}
}
