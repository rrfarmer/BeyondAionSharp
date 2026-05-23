using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcCastingInterruptServiceTests
{
	[Fact]
	public void EvaluateIncomingDamage_CancelsItemSkill()
	{
		var service = new WorldNpcCastingInterruptService();
		service.SetCastingSkill(1, new WorldNpcCastingSkill(100, WorldNpcSkillMethod.Item, CancelRate: 0));

		var result = service.EvaluateIncomingDamage(1, attackerObjectId: 1001, damage: 10, notifyAttack: true, maxHp: 100);

		Assert.Equal(WorldNpcCastingInterruptStatus.ItemSkillCanceled, result.Status);
		Assert.True(result.Canceled);
		Assert.False(service.TryGetCastingSkill(1, out _));
	}

	[Fact]
	public void EvaluateIncomingDamage_CancelsGuaranteedCancelRate()
	{
		var service = new WorldNpcCastingInterruptService();
		service.SetCastingSkill(1, new WorldNpcCastingSkill(101, WorldNpcSkillMethod.Cast, CancelRate: 99999));

		var result = service.EvaluateIncomingDamage(1, attackerObjectId: 1001, damage: 10, notifyAttack: true, maxHp: 100);

		Assert.Equal(WorldNpcCastingInterruptStatus.GuaranteedCanceled, result.Status);
		Assert.True(result.Canceled);
		Assert.False(service.TryGetCastingSkill(1, out _));
	}

	[Fact]
	public void EvaluateIncomingDamage_UsesJavaChanceFormulaAndRoll()
	{
		var service = new WorldNpcCastingInterruptService();
		service.SetCastingSkill(
			1,
			new WorldNpcCastingSkill(
				102,
				WorldNpcSkillMethod.Cast,
				CancelRate: 50,
				OwnerConcentration: 10));

		var result = service.EvaluateIncomingDamage(
			1,
			attackerObjectId: 1001,
			damage: 20,
			notifyAttack: true,
			maxHp: 100,
			new WorldNpcCastingInterruptOptions(ChanceRoll: 4));

		Assert.Equal(WorldNpcCastingInterruptStatus.ChanceCanceled, result.Status);
		Assert.True(result.Canceled);
		Assert.Equal(68, result.CancelChance);
		Assert.Equal(4, result.ChanceRoll);
		Assert.False(service.TryGetCastingSkill(1, out _));
	}

	[Fact]
	public void EvaluateIncomingDamage_KeepsCastingWhenChanceRollResists()
	{
		var service = new WorldNpcCastingInterruptService();
		var skill = service.SetCastingSkill(1, new WorldNpcCastingSkill(103, WorldNpcSkillMethod.Cast, CancelRate: 50));

		var result = service.EvaluateIncomingDamage(
			1,
			attackerObjectId: 1001,
			damage: 20,
			notifyAttack: true,
			maxHp: 100,
			new WorldNpcCastingInterruptOptions(ChanceRoll: 70));

		Assert.Equal(WorldNpcCastingInterruptStatus.ChanceResisted, result.Status);
		Assert.False(result.Canceled);
		Assert.True(service.TryGetCastingSkill(1, out var stored));
		Assert.Equal(skill, stored);
	}

	[Fact]
	public void EvaluateIncomingDamage_DoesNotChanceCancelBoss()
	{
		var service = new WorldNpcCastingInterruptService();
		var skill = service.SetCastingSkill(
			1,
			new WorldNpcCastingSkill(
				104,
				WorldNpcSkillMethod.Cast,
				CancelRate: 50,
				OwnerIsBoss: true));

		var result = service.EvaluateIncomingDamage(
			1,
			attackerObjectId: 1001,
			damage: 20,
			notifyAttack: true,
			maxHp: 100,
			new WorldNpcCastingInterruptOptions(ChanceRoll: 0));

		Assert.Equal(WorldNpcCastingInterruptStatus.BossProtected, result.Status);
		Assert.False(result.Canceled);
		Assert.True(service.TryGetCastingSkill(1, out var stored));
		Assert.Equal(skill, stored);
	}
}
