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
