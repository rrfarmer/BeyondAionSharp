using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcSkillResultCalculationServiceTests
{
	[Theory]
	[InlineData(0, 50, 0.5f)]
	[InlineData(7, 100, 1.0f)]
	[InlineData(13, 150, 1.5f)]
	public void Calculate_AppliesJavaRndDmgTypeOneBuckets(int roll, int expectedDamage, float expectedMultiplier)
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				RandomDamageType: 1,
				RandomRoll: roll)));

		Assert.Equal(WorldNpcSkillResultCalculationStatus.Calculated, result.Status);
		Assert.Equal(expectedDamage, result.FinalDamage);
		Assert.Equal(expectedMultiplier, result.RandomDamageMultiplier);
	}

	[Theory]
	[InlineData(69.999, 60, 0.6f)]
	[InlineData(70.0, 200, 2.0f)]
	public void Calculate_AppliesJavaChanceRndDmgTypeTwoBuckets(double chanceRoll, int expectedDamage, float expectedMultiplier)
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				RandomDamageType: 2,
				RandomChanceRoll: chanceRoll)));

		Assert.Equal(WorldNpcSkillResultCalculationStatus.Calculated, result.Status);
		Assert.Equal(expectedDamage, result.FinalDamage);
		Assert.Equal(expectedMultiplier, result.RandomDamageMultiplier);
	}

	[Fact]
	public void Calculate_RecordsCannotMissAndAttackUtilFlags()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 42,
			ShouldApplyAttackerMovementModifier: false,
			IgnoreShield: true,
			SendResult: false,
			ShouldIncreaseByOneTimeBoost: false,
			UsesTemplateDamage: true,
			Options: new WorldNpcSkillResultCalculationOptions(CannotMiss: true)));

		Assert.Equal(WorldNpcSkillResultCalculationStatus.Calculated, result.Status);
		Assert.Equal(42, result.FinalDamage);
		Assert.True(result.CannotMiss);
		Assert.False(result.CanDodgeOrResist);
		Assert.False(result.ShouldApplyAttackerMovementModifier);
		Assert.True(result.IgnoreShield);
		Assert.False(result.SendResult);
		Assert.False(result.ShouldIncreaseByOneTimeBoost);
		Assert.True(result.UsesTemplateDamage);
		Assert.Equal(WorldNpcSkillAttackStatus.NormalHit, result.AttackResult.AttackStatus);
		Assert.Equal(WorldNpcSkillHitType.PhysicalHit, result.AttackResult.HitType);
		Assert.False(result.AttackResult.ShieldChecked);
		Assert.True(result.AttackResult.LaunchSubEffect);
		Assert.Equal(42, result.EffectReserved.Value);
		Assert.Equal(WorldNpcEffectResourceType.Hp, result.EffectReserved.Type);
		Assert.False(result.EffectReserved.Send);
	}

	[Fact]
	public void Calculate_ReportsMissingRandomRollWithoutChangingDamage()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 42,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(RandomDamageType: 3)));

		Assert.Equal(WorldNpcSkillResultCalculationStatus.RandomRollMissing, result.Status);
		Assert.Equal(42, result.FinalDamage);
		Assert.Equal(1f, result.RandomDamageMultiplier);
	}

	[Fact]
	public void Calculate_BasePhysicalDamageMultiplierAppliesBeforeRandomDamage()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				RandomDamageType: 1,
				RandomRoll: 0,
				BaseDamageMultiplier: new WorldNpcSkillBaseDamageMultiplierOptions(
					WorldNpcSkillBaseDamageMultiplierKind.Physical,
					ObserverMultipliers: new[] { 1.5f, 2f }))));

		Assert.Equal(150, result.FinalDamage);
		Assert.True(result.BaseDamageMultiplier.Applied);
		Assert.Equal(300, result.BaseDamageMultiplier.FinalDamage);
		Assert.Equal(3f, result.BaseDamageMultiplier.Multiplier);
		Assert.Equal(2, result.BaseDamageMultiplier.ObserverMultiplierCount);
		Assert.Equal(0.5f, result.RandomDamageMultiplier);
	}

	[Fact]
	public void Calculate_BaseMagicalDamageMultiplierHonorsOneTimeBoostGate()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: false,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				BaseDamageMultiplier: new WorldNpcSkillBaseDamageMultiplierOptions(
					WorldNpcSkillBaseDamageMultiplierKind.Magical,
					ObserverMultipliers: new[] { 2f }))));

		Assert.Equal(100, result.FinalDamage);
		Assert.False(result.BaseDamageMultiplier.Applied);
		Assert.True(result.BaseDamageMultiplier.SkippedByOneTimeBoost);
		Assert.Equal(1f, result.BaseDamageMultiplier.Multiplier);
	}

	[Fact]
	public void Calculate_BaseMagicalDamageMultiplierAppliesWhenOneTimeBoostAllowed()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 80,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				BaseDamageMultiplier: new WorldNpcSkillBaseDamageMultiplierOptions(
					WorldNpcSkillBaseDamageMultiplierKind.Magical,
					ObserverMultipliers: new[] { 1.25f }))));

		Assert.Equal(100, result.FinalDamage);
		Assert.True(result.BaseDamageMultiplier.Applied);
		Assert.Equal(100f, result.BaseDamageMultiplier.ExactFinalDamage, precision: 3);
		Assert.Equal(1.25f, result.BaseDamageMultiplier.Multiplier);
	}

	[Fact]
	public void Calculate_BaseDamageMultiplierRecordsUnknownObserverInputs()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				BaseDamageMultiplier: new WorldNpcSkillBaseDamageMultiplierOptions(
					WorldNpcSkillBaseDamageMultiplierKind.Physical,
					ObserverMultipliersKnown: false))));

		Assert.Equal(100, result.FinalDamage);
		Assert.False(result.BaseDamageMultiplier.Applied);
		Assert.True(result.BaseDamageMultiplier.HasUnresolvedInputs);
		Assert.True(result.BaseDamageMultiplier.ObserverMultipliersInputMissing);
	}

	[Fact]
	public void Calculate_CriticalDamageAppliesJavaWeaponMultiplierFortitudeAndCritAdd()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AttackStatus: WorldNpcSkillAttackStatus.Critical,
				CriticalDamage: new WorldNpcSkillCriticalDamageOptions(
					Element: WorldNpcSkillDamageModifierElement.Physical,
					WeaponGroup: WorldNpcSkillWeaponGroup.Dagger,
					CriticalAddDamage: 20,
					TargetIsPlayer: true,
					CriticalDamageReduce: 300))));

		Assert.Equal(220, result.FinalDamage);
		Assert.True(result.CriticalDamage.Applied);
		Assert.Equal(2.2f, result.CriticalDamage.Coefficient, precision: 3);
		Assert.Equal(220f, result.CriticalDamage.ExactFinalDamage, precision: 3);
	}

	[Fact]
	public void Calculate_CriticalDamageSkipsNonCriticalStatus()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AttackStatus: WorldNpcSkillAttackStatus.NormalHit,
				CriticalDamage: new WorldNpcSkillCriticalDamageOptions(CriticalAddDamage: 50))));

		Assert.Equal(100, result.FinalDamage);
		Assert.False(result.CriticalDamage.Applied);
		Assert.True(result.CriticalDamage.SkippedByStatus);
	}

	[Fact]
	public void Calculate_CriticalDamageRecordsMissingFortitudeInput()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AttackStatus: WorldNpcSkillAttackStatus.Critical,
				CriticalDamage: new WorldNpcSkillCriticalDamageOptions(TargetIsPlayer: true))));

		Assert.Equal(100, result.FinalDamage);
		Assert.False(result.CriticalDamage.Applied);
		Assert.True(result.CriticalDamage.HasUnresolvedInputs);
		Assert.True(result.CriticalDamage.CriticalDamageReduceInputMissing);
	}

	[Fact]
	public void Calculate_BlockedDamageAppliesJavaShieldCap()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AttackStatus: WorldNpcSkillAttackStatus.Block,
				BlockedDamage: new WorldNpcSkillBlockedDamageOptions(
					TargetIsPlayer: true,
					HasShield: true,
					DamageReduceStat: 50f,
					ShieldReduceMax: 20))));

		Assert.Equal(80, result.FinalDamage);
		Assert.True(result.BlockedDamage.Applied);
		Assert.Equal(20f, result.BlockedDamage.ReduceValue);
		Assert.True(result.BlockedDamage.CappedByShield);
	}

	[Fact]
	public void Calculate_BlockedDamageRecordsMissingReductionInputs()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AttackStatus: WorldNpcSkillAttackStatus.Block,
				BlockedDamage: new WorldNpcSkillBlockedDamageOptions(
					TargetIsPlayer: true,
					HasShield: true))));

		Assert.Equal(100, result.FinalDamage);
		Assert.False(result.BlockedDamage.Applied);
		Assert.True(result.BlockedDamage.HasUnresolvedInputs);
		Assert.True(result.BlockedDamage.DamageReduceStatInputMissing);
		Assert.True(result.BlockedDamage.ShieldReduceMaxInputMissing);
	}

	[Fact]
	public void Calculate_FinalizationAppliesSkillResultTailOrder()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 120,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				Finalization: new WorldNpcSkillFinalizationOptions(
					EffectorIsNpc: true,
					EffectorNpcOwnerDamageMultiplier: 1.5f,
					HasSkill: true,
					EffectedListCount: 3,
					TemplateIsShared: true,
					PvpPveMultiplier: 0.5f,
					EffectedIsNpc: true,
					EffectedNpcDamageMultiplier: 2f))));

		Assert.Equal(60, result.FinalDamage);
		Assert.Equal(60, result.AttackResult.Damage);
		Assert.Equal(60, result.EffectReserved.Value);
		Assert.True(result.Finalization.Applied);
		Assert.Equal(120, result.Finalization.OriginalDamage);
		Assert.Equal(180f, result.Finalization.DamageAfterEffectorNpc, precision: 3);
		Assert.Equal(60f, result.Finalization.DamageAfterShared, precision: 3);
		Assert.Equal(30f, result.Finalization.DamageAfterPvpPve, precision: 3);
		Assert.Equal(30f, result.Finalization.DamageAfterZeroClamp, precision: 3);
		Assert.Equal(60f, result.Finalization.ExactFinalDamage, precision: 3);
		Assert.True(result.Finalization.EffectorNpcApplied);
		Assert.True(result.Finalization.SharedDamageApplied);
		Assert.True(result.Finalization.PvpPveApplied);
		Assert.True(result.Finalization.EffectedNpcApplied);
	}

	[Fact]
	public void Calculate_FinalizationClampsNegativeBeforeEffectedNpc()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 10,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				Finalization: new WorldNpcSkillFinalizationOptions(
					PvpPveMultiplier: -0.1f,
					EffectedIsNpc: true,
					EffectedNpcDamageMultiplier: 2f))));

		Assert.Equal(0, result.FinalDamage);
		Assert.Equal(0, result.AttackResult.Damage);
		Assert.Equal(0, result.EffectReserved.Value);
		Assert.True(result.Finalization.Applied);
		Assert.Equal(-1f, result.Finalization.DamageAfterPvpPve, precision: 3);
		Assert.Equal(0f, result.Finalization.DamageAfterZeroClamp, precision: 3);
		Assert.True(result.Finalization.ZeroClampApplied);
		Assert.True(result.Finalization.EffectedNpcApplied);
	}

	[Theory]
	[InlineData(1, true)]
	[InlineData(3, false)]
	public void Calculate_FinalizationSkipsSharedWhenSingleTargetOrTemplateNotShared(int effectedListCount, bool templateIsShared)
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 90,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				Finalization: new WorldNpcSkillFinalizationOptions(
					HasSkill: true,
					EffectedListCount: effectedListCount,
					TemplateIsShared: templateIsShared,
					PvpPveMultiplier: 1f))));

		Assert.Equal(90, result.FinalDamage);
		Assert.True(result.Finalization.Applied);
		Assert.False(result.Finalization.SharedDamageApplied);
		Assert.Equal(90f, result.Finalization.DamageAfterShared, precision: 3);
	}

	[Fact]
	public void Calculate_FinalizationRecordsMissingInputs()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				Finalization: new WorldNpcSkillFinalizationOptions(
					EffectorIsNpc: true,
					HasSkill: true,
					TemplateIsShared: true,
					EffectedIsNpc: true))));

		Assert.Equal(100, result.FinalDamage);
		Assert.False(result.Finalization.Applied);
		Assert.True(result.Finalization.HasUnresolvedInputs);
		Assert.True(result.Finalization.EffectorNpcHookMissing);
		Assert.True(result.Finalization.SharedTargetCountMissing);
		Assert.True(result.Finalization.PvpPveInputMissing);
		Assert.True(result.Finalization.EffectedNpcHookMissing);
	}

	[Fact]
	public void Calculate_CreatesJavaAttackResultAndEffectReservedSurface()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 55,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AttackStatus: WorldNpcSkillAttackStatus.Critical,
				HitType: WorldNpcSkillHitType.MagicalHit,
				EffectPosition: 2)));

		Assert.Equal(55, result.AttackResult.Damage);
		Assert.Equal(WorldNpcSkillAttackStatus.Critical, result.AttackResult.AttackStatus);
		Assert.Equal(WorldNpcSkillHitType.MagicalHit, result.AttackResult.HitType);
		Assert.True(result.AttackResult.ShieldChecked);
		Assert.Equal(0, result.AttackResult.ReflectedDamage);
		Assert.Equal(0, result.AttackResult.ProtectedDamage);
		Assert.True(result.AttackResult.LaunchSubEffect);
		Assert.Equal(2, result.EffectReserved.Position);
		Assert.Equal(55, result.EffectReserved.Value);
		Assert.Equal(55, result.EffectReserved.ValueToSend);
		Assert.Equal(WorldNpcEffectResourceType.Hp, result.EffectReserved.Type);
		Assert.True(result.EffectReserved.IsDamage);
		Assert.True(result.EffectReserved.Send);
	}

	[Fact]
	public void Calculate_EffectReservedValueToSendIsNegativeForNonDamage()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 25,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				ResourceType: WorldNpcEffectResourceType.Mp,
				IsDamage: false)));

		Assert.Equal(WorldNpcEffectResourceType.Mp, result.EffectReserved.Type);
		Assert.False(result.EffectReserved.IsDamage);
		Assert.Equal(-25, result.EffectReserved.ValueToSend);
	}

	[Fact]
	public void Calculate_PhysicalStatusCalculationMirrorsJavaBlockCriticalOrder()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 40,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AttackStatusCalculation: new WorldNpcSkillAttackStatusCalculationOptions(
					WorldNpcSkillAttackStatusCalculationKind.Physical,
					AccuracyModifier: 15,
					CriticalProbability: 75,
					IsSkill: false,
					TargetIsPlayer: true,
					TargetHasShield: true,
					DodgeResult: false,
					BlockResult: true,
					ParryResult: true,
					CriticalResult: true))));

		Assert.Equal(WorldNpcSkillAttackStatus.CriticalBlock, result.AttackResult.AttackStatus);
		Assert.Equal(WorldNpcSkillAttackStatus.CriticalBlock, result.AttackStatusCalculation.FinalStatus);
		Assert.Equal(WorldNpcSkillAttackStatus.Block, result.AttackStatusCalculation.BaseStatus);
		Assert.Equal(15, result.AttackStatusCalculation.AccuracyModifier);
		Assert.Equal(75, result.AttackStatusCalculation.CriticalProbability);
		Assert.True(result.AttackStatusCalculation.DodgeChecked);
		Assert.True(result.AttackStatusCalculation.BlockChecked);
		Assert.False(result.AttackStatusCalculation.ParryChecked);
		Assert.True(result.AttackStatusCalculation.CriticalChecked);
		Assert.True(result.AttackStatusCalculation.CriticalUpgraded);
		Assert.False(result.AttackStatusCalculation.HasUnresolvedProbabilityInputs);
	}

	[Fact]
	public void Calculate_PhysicalStatusCalculationConvertsOffHandAfterCritical()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 40,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AttackStatusCalculation: new WorldNpcSkillAttackStatusCalculationOptions(
					WorldNpcSkillAttackStatusCalculationKind.Physical,
					IsMainHand: false,
					IsSkill: false,
					TargetIsPlayer: true,
					DodgeResult: false,
					ParryResult: true,
					CriticalResult: true))));

		Assert.Equal(WorldNpcSkillAttackStatus.OffHandCriticalParry, result.AttackResult.AttackStatus);
		Assert.Equal(WorldNpcSkillAttackStatus.OffHandCriticalParry, result.AttackStatusCalculation.FinalStatus);
		Assert.Equal(WorldNpcSkillAttackStatus.Parry, result.AttackStatusCalculation.BaseStatus);
		Assert.True(result.AttackStatusCalculation.OffHandConverted);
		Assert.True(result.AttackStatusCalculation.ParryChecked);
		Assert.True(result.AttackStatusCalculation.CriticalUpgraded);
	}

	[Fact]
	public void Calculate_PhysicalCannotMissRecordsJavaProbeOnlyChecks()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 40,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AttackStatusCalculation: new WorldNpcSkillAttackStatusCalculationOptions(
					WorldNpcSkillAttackStatusCalculationKind.Physical,
					CannotMiss: true,
					CriticalResult: false))));

		Assert.Equal(WorldNpcSkillAttackStatus.NormalHit, result.AttackResult.AttackStatus);
		Assert.True(result.CannotMiss);
		Assert.False(result.CanDodgeOrResist);
		Assert.True(result.AttackStatusCalculation.ProbeOnly);
		Assert.True(result.AttackStatusCalculation.DodgeChecked);
		Assert.True(result.AttackStatusCalculation.BlockChecked);
		Assert.True(result.AttackStatusCalculation.ParryChecked);
		Assert.True(result.AttackStatusCalculation.DodgeInputMissing);
		Assert.True(result.AttackStatusCalculation.BlockInputMissing);
		Assert.True(result.AttackStatusCalculation.ParryInputMissing);
		Assert.True(result.AttackStatusCalculation.HasUnresolvedProbabilityInputs);
	}

	[Fact]
	public void Calculate_MagicalStatusCalculationResistShortCircuitsCritical()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 40,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AttackStatusCalculation: new WorldNpcSkillAttackStatusCalculationOptions(
					WorldNpcSkillAttackStatusCalculationKind.Magical,
					IsSkill: false,
					MagicalResistResult: true,
					CriticalResult: true))));

		Assert.Equal(WorldNpcSkillAttackStatus.Resist, result.AttackResult.AttackStatus);
		Assert.Equal(WorldNpcSkillAttackStatus.Resist, result.AttackStatusCalculation.FinalStatus);
		Assert.True(result.AttackStatusCalculation.MagicalResistChecked);
		Assert.False(result.AttackStatusCalculation.CriticalChecked);
		Assert.False(result.AttackStatusCalculation.CriticalUpgraded);
	}

	[Fact]
	public void Calculate_MagicalStatusCalculationAppliesSkillCritical()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 40,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AttackStatusCalculation: new WorldNpcSkillAttackStatusCalculationOptions(
					WorldNpcSkillAttackStatusCalculationKind.Magical,
					CriticalProbability: 150,
					IsSkill: true,
					ApplyMagicalCritical: true,
					CriticalResult: true))));

		Assert.Equal(WorldNpcSkillAttackStatus.Critical, result.AttackResult.AttackStatus);
		Assert.Equal(150, result.AttackStatusCalculation.CriticalProbability);
		Assert.False(result.AttackStatusCalculation.MagicalResistChecked);
		Assert.True(result.AttackStatusCalculation.CriticalChecked);
		Assert.True(result.AttackStatusCalculation.CriticalUpgraded);
		Assert.False(result.AttackStatusCalculation.HasUnresolvedProbabilityInputs);
	}

	[Fact]
	public void Calculate_MagicalStatusCalculationSkipsCriticalWhenMcritNotApplied()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 40,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AttackStatusCalculation: new WorldNpcSkillAttackStatusCalculationOptions(
					WorldNpcSkillAttackStatusCalculationKind.Magical,
					ApplyMagicalCritical: false,
					CriticalResult: true))));

		Assert.Equal(WorldNpcSkillAttackStatus.NormalHit, result.AttackResult.AttackStatus);
		Assert.False(result.AttackStatusCalculation.ApplyMagicalCritical);
		Assert.False(result.AttackStatusCalculation.CriticalChecked);
		Assert.False(result.AttackStatusCalculation.CriticalUpgraded);
	}

	[Fact]
	public void Calculate_DamageModifierSkipsDodgeAndResistLikeJava()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AttackStatus: WorldNpcSkillAttackStatus.Dodge,
				DamageModifier: new WorldNpcSkillDamageModifierOptions())));

		Assert.Equal(100, result.FinalDamage);
		Assert.Equal(100, result.AttackResult.Damage);
		Assert.True(result.DamageModifier.WasRequested);
		Assert.False(result.DamageModifier.Applied);
		Assert.True(result.DamageModifier.SkippedForCounterStatus);
		Assert.False(result.DamageModifier.HasUnresolvedInputs);
		Assert.Equal(WorldNpcSkillAttackStatus.Dodge, result.DamageModifier.BaseStatus);
	}

	[Fact]
	public void Calculate_DamageModifierRecordsUnresolvedStatInputs()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				DamageModifier: new WorldNpcSkillDamageModifierOptions())));

		Assert.Equal(100, result.FinalDamage);
		Assert.False(result.DamageModifier.Applied);
		Assert.True(result.DamageModifier.HasUnresolvedInputs);
		Assert.True(result.DamageModifier.DefenseInputMissing);
		Assert.True(result.DamageModifier.PvpPveInputMissing);
	}

	[Fact]
	public void Calculate_DamageModifierAppliesJavaParryMultiplier()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AttackStatus: WorldNpcSkillAttackStatus.Parry,
				DamageModifier: new WorldNpcSkillDamageModifierOptions(
					Defense: 10f,
					PvpPveMultiplier: 1f))));

		Assert.Equal(59, result.FinalDamage);
		Assert.Equal(59, result.AttackResult.Damage);
		Assert.True(result.DamageModifier.Applied);
		Assert.Equal(0.6f, result.DamageModifier.MainMultiplier);
		Assert.Equal(0.6f, result.DamageModifier.OffMultiplier);
		Assert.Equal(59.4f, result.DamageModifier.ExactFinalDamage, precision: 3);
	}

	[Fact]
	public void Calculate_DamageModifierAppliesJavaBlockReductionCap()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AttackStatus: WorldNpcSkillAttackStatus.Block,
				DamageModifier: new WorldNpcSkillDamageModifierOptions(
					Defense: 0f,
					PvpPveMultiplier: 1f,
					TargetIsPlayer: true,
					BlockReduceRatio: 0.5f,
					BlockReduceMax: 20))));

		Assert.Equal(80, result.FinalDamage);
		Assert.True(result.DamageModifier.Applied);
		Assert.Equal(0.5f, result.DamageModifier.BlockReduceRatio);
		Assert.Equal(20, result.DamageModifier.BlockReduceMax);
		Assert.Equal(20f, result.DamageModifier.BlockReduction);
	}

	[Fact]
	public void Calculate_DamageModifierAppliesJavaWeaponCriticalMultiplierAndFortitude()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 50,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AttackStatus: WorldNpcSkillAttackStatus.Critical,
				DamageModifier: new WorldNpcSkillDamageModifierOptions(
					Defense: 0f,
					PvpPveMultiplier: 1f,
					TargetIsPlayer: true,
					MainHandWeaponGroup: WorldNpcSkillWeaponGroup.Dagger,
					CriticalDamageReduce: 300))));

		Assert.Equal(100, result.FinalDamage);
		Assert.True(result.DamageModifier.Applied);
		Assert.Equal(2.0f, result.DamageModifier.MainMultiplier, precision: 3);
		Assert.Equal(1.3f, result.DamageModifier.OffMultiplier, precision: 3);
		Assert.Equal(100f, result.DamageModifier.ExactFinalDamage, precision: 3);
		Assert.Equal(2.3f, WorldNpcSkillWeaponGroup.Dagger.GetJavaCriticalMultiplier(), precision: 3);
	}

	[Fact]
	public void Calculate_AdditionalHitsSkipExactDodgeAndResistLikeJava()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AttackStatus: WorldNpcSkillAttackStatus.Dodge,
				AdditionalHits: new WorldNpcSkillAdditionalHitOptions(
					AttackerIsPlayer: true,
					HasMainHandWeapon: true,
					MainHandWeaponHitCount: 3,
					MainHandRoll: 3))));

		Assert.True(result.AdditionalHits.WasRequested);
		Assert.False(result.AdditionalHits.Eligible);
		Assert.True(result.AdditionalHits.SkippedByStatus);
		Assert.Empty(result.AdditionalHits.GeneratedHits);
	}

	[Fact]
	public void Calculate_AdditionalHitsGenerateMainHandAmplification()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AdditionalHits: new WorldNpcSkillAdditionalHitOptions(
					AttackerIsPlayer: true,
					HasMainHandWeapon: true,
					MainHandWeaponHitCount: 3,
					MainHandRoll: 3))));

		Assert.True(result.AdditionalHits.Eligible);
		Assert.Equal(2, result.AdditionalHits.MainHandAdditionalHitCount);
		Assert.Equal(0, result.AdditionalHits.OffHandAdditionalHitCount);
		Assert.Equal(2, result.AdditionalHits.AmplificationLoopCount);
		Assert.Collection(
			result.AdditionalHits.GeneratedHits,
			hit =>
			{
				Assert.Equal(10, hit.Damage);
				Assert.Equal(WorldNpcSkillAttackStatus.NormalHit, hit.AttackStatus);
				Assert.False(hit.IsOffHand);
			},
			hit =>
			{
				Assert.Equal(10, hit.Damage);
				Assert.Equal(WorldNpcSkillAttackStatus.NormalHit, hit.AttackStatus);
				Assert.False(hit.IsOffHand);
			});
	}

	[Fact]
	public void Calculate_AdditionalHitsGenerateOffHandAmplification()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AdditionalHits: new WorldNpcSkillAdditionalHitOptions(
					AttackerIsPlayer: true,
					HasMainHandWeapon: true,
					MainHandWeaponHitCount: 3,
					MainHandRoll: 1,
					HasOffHandAttackResult: true,
					HasOffHandWeapon: true,
					OffHandWeaponHitCount: 3,
					OffHandRoll: 2,
					OffHandDamage: 80,
					OffHandHitType: WorldNpcSkillHitType.MagicalHit))));

		Assert.True(result.AdditionalHits.Eligible);
		Assert.Equal(0, result.AdditionalHits.MainHandAdditionalHitCount);
		Assert.Equal(2, result.AdditionalHits.OffHandAdditionalHitCount);
		Assert.Collection(
			result.AdditionalHits.GeneratedHits,
			hit =>
			{
				Assert.Equal(8, hit.Damage);
				Assert.Equal(WorldNpcSkillAttackStatus.OffHandNormalHit, hit.AttackStatus);
				Assert.Equal(WorldNpcSkillHitType.MagicalHit, hit.HitType);
				Assert.True(hit.IsOffHand);
			},
			hit =>
			{
				Assert.Equal(8, hit.Damage);
				Assert.Equal(WorldNpcSkillAttackStatus.OffHandNormalHit, hit.AttackStatus);
				Assert.Equal(WorldNpcSkillHitType.MagicalHit, hit.HitType);
				Assert.True(hit.IsOffHand);
			});
	}

	[Fact]
	public void Calculate_AdditionalHitsRecordLowDamageWithoutGeneratedHit()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 9,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AdditionalHits: new WorldNpcSkillAdditionalHitOptions(
					AttackerIsPlayer: true,
					HasMainHandWeapon: true,
					MainHandWeaponHitCount: 3,
					MainHandRoll: 3))));

		Assert.Equal(2, result.AdditionalHits.MainHandAdditionalHitCount);
		Assert.Equal(2, result.AdditionalHits.AmplificationLoopCount);
		Assert.Empty(result.AdditionalHits.GeneratedHits);
	}

	[Fact]
	public void Calculate_AdditionalHitsRecordMissingRollInputs()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AdditionalHits: new WorldNpcSkillAdditionalHitOptions(
					AttackerIsPlayer: true,
					HasMainHandWeapon: true,
					MainHandWeaponHitCount: 3))));

		Assert.True(result.AdditionalHits.Eligible);
		Assert.True(result.AdditionalHits.HasUnresolvedInputs);
		Assert.True(result.AdditionalHits.MainHandRollMissing);
		Assert.Empty(result.AdditionalHits.GeneratedHits);
	}

	[Fact]
	public void Calculate_NpcAiDamageModifierSkipsWhenNoNpcParticipant()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				NpcAiDamageModifier: new WorldNpcSkillNpcAiDamageModifierOptions())));

		Assert.Equal(100, result.FinalDamage);
		Assert.True(result.NpcAiDamageModifier.WasRequested);
		Assert.False(result.NpcAiDamageModifier.Applied);
		Assert.False(result.NpcAiDamageModifier.HasNpcParticipant);
		Assert.False(result.NpcAiDamageModifier.HasUnresolvedInputs);
	}

	[Fact]
	public void Calculate_NpcAiDamageModifierAppliesAttackerThenAttackedHooks()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				NpcAiDamageModifier: new WorldNpcSkillNpcAiDamageModifierOptions(
					AttackerIsNpc: true,
					AttackedIsNpc: true,
					AttackerNpcOwnerDamageMultiplier: 1.5f,
					AttackedNpcDamageMultiplier: 0.5f))));

		Assert.Equal(75, result.FinalDamage);
		Assert.Equal(75, result.AttackResult.Damage);
		Assert.Equal(75, result.EffectReserved.Value);
		Assert.True(result.NpcAiDamageModifier.Applied);
		Assert.Equal(100, result.NpcAiDamageModifier.PrimaryOriginalDamage);
		Assert.Equal(75, result.NpcAiDamageModifier.PrimaryFinalDamage);
		Assert.Equal(75f, result.NpcAiDamageModifier.PrimaryExactFinalDamage, precision: 3);
	}

	[Fact]
	public void Calculate_NpcAiDamageModifierAppliesGeneratedAdditionalHits()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AdditionalHits: new WorldNpcSkillAdditionalHitOptions(
					AttackerIsPlayer: true,
					HasMainHandWeapon: true,
					MainHandWeaponHitCount: 3,
					MainHandRoll: 3),
				NpcAiDamageModifier: new WorldNpcSkillNpcAiDamageModifierOptions(
					AttackedIsNpc: true,
					AttackedNpcDamageMultiplier: 2f))));

		Assert.Equal(200, result.FinalDamage);
		Assert.Collection(
			result.NpcAiDamageModifier.AdditionalHits,
			hit =>
			{
				Assert.Equal(10, hit.OriginalDamage);
				Assert.Equal(20, hit.FinalDamage);
				Assert.False(hit.IsOffHand);
			},
			hit =>
			{
				Assert.Equal(10, hit.OriginalDamage);
				Assert.Equal(20, hit.FinalDamage);
				Assert.False(hit.IsOffHand);
			});
	}

	[Fact]
	public void Calculate_NpcAiDamageModifierRecordsMissingHookInputs()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				NpcAiDamageModifier: new WorldNpcSkillNpcAiDamageModifierOptions(AttackerIsNpc: true))));

		Assert.Equal(100, result.FinalDamage);
		Assert.False(result.NpcAiDamageModifier.Applied);
		Assert.True(result.NpcAiDamageModifier.HasNpcParticipant);
		Assert.True(result.NpcAiDamageModifier.HasUnresolvedInputs);
		Assert.True(result.NpcAiDamageModifier.AttackerNpcOwnerHookMissing);
		Assert.False(result.NpcAiDamageModifier.AttackedNpcHookMissing);
	}

	[Fact]
	public void Calculate_ShieldObserverSkipsWhenIgnoreShield()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: true,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				ShieldObserver: new WorldNpcSkillShieldObserverOptions(
					Outputs: new[]
					{
						new WorldNpcSkillShieldObserverOutput(WorldNpcSkillShieldType.Normal, FinalDamage: 25),
					}))));

		Assert.Equal(100, result.FinalDamage);
		Assert.False(result.AttackResult.ShieldChecked);
		Assert.False(result.ShieldObserver.WasChecked);
		Assert.False(result.ShieldObserver.Applied);
		Assert.True(result.ShieldObserver.SkippedByIgnoreShield);
		Assert.Equal(100, result.EffectReserved.Value);
	}

	[Fact]
	public void Calculate_ShieldObserverAppliesMpShieldOutput()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				ShieldObserver: new WorldNpcSkillShieldObserverOptions(
					Outputs: new[]
					{
						new WorldNpcSkillShieldObserverOutput(
							WorldNpcSkillShieldType.MpShield,
							FinalDamage: 70,
							MpAbsorbed: 12,
							MpShieldSkillId: 7101,
							LaunchSubEffect: false),
					}))));

		Assert.Equal(70, result.FinalDamage);
		Assert.Equal(70, result.AttackResult.Damage);
		Assert.Equal(70, result.EffectReserved.Value);
		Assert.True(result.ShieldObserver.WasChecked);
		Assert.True(result.ShieldObserver.Applied);
		Assert.Equal(16, result.AttackResult.ShieldType);
		Assert.Equal(12, result.AttackResult.MpAbsorbed);
		Assert.Equal(7101, result.AttackResult.MpShieldSkillId);
		Assert.False(result.AttackResult.LaunchSubEffect);
		Assert.Equal(16, WorldNpcSkillShieldType.MpShield.GetJavaId());
	}

	[Fact]
	public void Calculate_ShieldObserverOrsShieldTypesAndRecordsReflectProtectFields()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				ShieldObserver: new WorldNpcSkillShieldObserverOptions(
					Outputs: new[]
					{
						new WorldNpcSkillShieldObserverOutput(WorldNpcSkillShieldType.Normal, FinalDamage: 85),
						new WorldNpcSkillShieldObserverOutput(
							WorldNpcSkillShieldType.Reflector,
							ReflectedDamage: 22,
							ReflectedSkillId: 3001,
							SchedulesReflectedAttack: true),
						new WorldNpcSkillShieldObserverOutput(
							WorldNpcSkillShieldType.Protect,
							FinalDamage: 50,
							ProtectedSkillId: 4100,
							ProtectedDamage: 35,
							ProtectorId: 77,
							LaunchSubEffect: false),
					}))));

		Assert.Equal(50, result.FinalDamage);
		Assert.Equal(11, result.AttackResult.ShieldType);
		Assert.Equal(22, result.AttackResult.ReflectedDamage);
		Assert.Equal(3001, result.AttackResult.ReflectedSkillId);
		Assert.Equal(4100, result.AttackResult.ProtectedSkillId);
		Assert.Equal(35, result.AttackResult.ProtectedDamage);
		Assert.Equal(77, result.AttackResult.ProtectorId);
		Assert.False(result.AttackResult.LaunchSubEffect);
		Assert.Collection(
			result.ShieldObserver.Outputs,
			output => Assert.Equal(85, output.DamageAfter),
			output =>
			{
				Assert.Equal(85, output.DamageBefore);
				Assert.True(output.SchedulesReflectedAttack);
			},
			output =>
			{
				Assert.Equal(50, output.DamageAfter);
				Assert.Equal(11, output.ShieldTypeAfter);
			});
	}

	[Fact]
	public void Calculate_ShieldObserverSkipsCounterStatusesLikeJava()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				AttackStatus: WorldNpcSkillAttackStatus.Resist,
				ShieldObserver: new WorldNpcSkillShieldObserverOptions(
					Outputs: new[]
					{
						new WorldNpcSkillShieldObserverOutput(WorldNpcSkillShieldType.Normal, FinalDamage: 0),
					}))));

		Assert.Equal(100, result.FinalDamage);
		Assert.True(result.ShieldObserver.WasChecked);
		Assert.False(result.ShieldObserver.Applied);
		Assert.True(result.ShieldObserver.SkippedByCounterStatus);
		Assert.Empty(result.ShieldObserver.Outputs);
	}

	[Fact]
	public void Calculate_ShieldObserverRecordsUnknownObserverOutputs()
	{
		var service = new WorldNpcSkillResultCalculationService();

		var result = service.Calculate(new WorldNpcSkillResultCalculationRequest(
			InputDamage: 100,
			ShouldApplyAttackerMovementModifier: true,
			IgnoreShield: false,
			SendResult: true,
			ShouldIncreaseByOneTimeBoost: true,
			UsesTemplateDamage: false,
			Options: new WorldNpcSkillResultCalculationOptions(
				ShieldObserver: new WorldNpcSkillShieldObserverOptions(ObserverOutputsKnown: false))));

		Assert.Equal(100, result.FinalDamage);
		Assert.True(result.ShieldObserver.WasChecked);
		Assert.False(result.ShieldObserver.Applied);
		Assert.True(result.ShieldObserver.HasUnresolvedInputs);
		Assert.True(result.ShieldObserver.ObserverOutputInputMissing);
	}

	[Theory]
	[InlineData(WorldNpcSkillAttackStatus.Dodge, 0, true, false)]
	[InlineData(WorldNpcSkillAttackStatus.OffHandDodge, 1, true, false)]
	[InlineData(WorldNpcSkillAttackStatus.NormalHit, 10, false, false)]
	[InlineData(WorldNpcSkillAttackStatus.CriticalDodge, -64, true, true)]
	[InlineData(WorldNpcSkillAttackStatus.Critical, -54, false, true)]
	[InlineData(WorldNpcSkillAttackStatus.OffHandCritical, -37, false, true)]
	public void AttackStatusHelpers_MirrorJavaIdsAndFlags(
		WorldNpcSkillAttackStatus status,
		int expectedJavaId,
		bool expectedCounter,
		bool expectedCritical)
	{
		Assert.Equal(expectedJavaId, status.GetJavaId());
		Assert.Equal(expectedCounter, status.IsCounterSkill());
		Assert.Equal(expectedCritical, status.IsCritical());
	}

	[Theory]
	[InlineData(WorldNpcSkillAttackStatus.Dodge, WorldNpcSkillAttackStatus.OffHandDodge)]
	[InlineData(WorldNpcSkillAttackStatus.Parry, WorldNpcSkillAttackStatus.OffHandParry)]
	[InlineData(WorldNpcSkillAttackStatus.Block, WorldNpcSkillAttackStatus.OffHandBlock)]
	[InlineData(WorldNpcSkillAttackStatus.Resist, WorldNpcSkillAttackStatus.OffHandResist)]
	[InlineData(WorldNpcSkillAttackStatus.Buf, WorldNpcSkillAttackStatus.OffHandBuf)]
	[InlineData(WorldNpcSkillAttackStatus.NormalHit, WorldNpcSkillAttackStatus.OffHandNormalHit)]
	[InlineData(WorldNpcSkillAttackStatus.Critical, WorldNpcSkillAttackStatus.OffHandCritical)]
	[InlineData(WorldNpcSkillAttackStatus.CriticalDodge, WorldNpcSkillAttackStatus.OffHandCriticalDodge)]
	public void GetOffHandStatus_MirrorsJavaMainHandMapping(
		WorldNpcSkillAttackStatus status,
		WorldNpcSkillAttackStatus expected)
	{
		Assert.Equal(expected, status.GetOffHandStatus());
	}

	[Fact]
	public void GetOffHandStatus_RejectsJavaInvalidMainHandStatus()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => WorldNpcSkillAttackStatus.OffHandDodge.GetOffHandStatus());
	}

	[Theory]
	[InlineData(WorldNpcSkillAttackStatus.CriticalDodge, WorldNpcSkillAttackStatus.Dodge)]
	[InlineData(WorldNpcSkillAttackStatus.OffHandCriticalDodge, WorldNpcSkillAttackStatus.Dodge)]
	[InlineData(WorldNpcSkillAttackStatus.CriticalResist, WorldNpcSkillAttackStatus.Resist)]
	[InlineData(WorldNpcSkillAttackStatus.OffHandParry, WorldNpcSkillAttackStatus.Parry)]
	[InlineData(WorldNpcSkillAttackStatus.OffHandCriticalBlock, WorldNpcSkillAttackStatus.Block)]
	[InlineData(WorldNpcSkillAttackStatus.Critical, WorldNpcSkillAttackStatus.Critical)]
	public void GetBaseStatus_MirrorsJavaBaseStatusMapping(
		WorldNpcSkillAttackStatus status,
		WorldNpcSkillAttackStatus expected)
	{
		Assert.Equal(expected, status.GetBaseStatus());
	}

	[Theory]
	[InlineData(WorldNpcSkillAttackStatus.Dodge, WorldNpcSkillAttackStatus.CriticalDodge)]
	[InlineData(WorldNpcSkillAttackStatus.OffHandDodge, WorldNpcSkillAttackStatus.OffHandCriticalDodge)]
	[InlineData(WorldNpcSkillAttackStatus.Parry, WorldNpcSkillAttackStatus.CriticalParry)]
	[InlineData(WorldNpcSkillAttackStatus.OffHandParry, WorldNpcSkillAttackStatus.OffHandCriticalParry)]
	[InlineData(WorldNpcSkillAttackStatus.Block, WorldNpcSkillAttackStatus.CriticalBlock)]
	[InlineData(WorldNpcSkillAttackStatus.OffHandBlock, WorldNpcSkillAttackStatus.OffHandCriticalBlock)]
	[InlineData(WorldNpcSkillAttackStatus.NormalHit, WorldNpcSkillAttackStatus.Critical)]
	[InlineData(WorldNpcSkillAttackStatus.OffHandNormalHit, WorldNpcSkillAttackStatus.OffHandCritical)]
	[InlineData(WorldNpcSkillAttackStatus.Resist, WorldNpcSkillAttackStatus.Resist)]
	public void GetCriticalStatusFor_MirrorsJavaCriticalMapping(
		WorldNpcSkillAttackStatus status,
		WorldNpcSkillAttackStatus expected)
	{
		Assert.Equal(expected, status.GetCriticalStatusFor());
	}
}
