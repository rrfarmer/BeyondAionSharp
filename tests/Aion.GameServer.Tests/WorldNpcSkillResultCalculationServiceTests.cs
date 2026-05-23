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
}
