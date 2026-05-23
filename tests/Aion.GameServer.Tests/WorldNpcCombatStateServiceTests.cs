using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcCombatStateServiceTests
{
	[Fact]
	public void AddDamage_AccumulatesDamageAndHateByAttacker()
	{
		var service = new WorldNpcCombatStateService();

		service.AddDamage(
			npcObjectId: 1,
			attackerObjectId: 1001,
			damage: 25,
			notifyAttack: true,
			WorldNpcDamageHopType.Damage);
		var state = service.AddDamage(
			npcObjectId: 1,
			attackerObjectId: 1001,
			damage: 30,
			notifyAttack: false,
			WorldNpcDamageHopType.Damage);

		Assert.Equal(1, state.NpcObjectId);
		Assert.Equal(0, state.AttackedCount);
		var entry = Assert.Single(state.HateEntries);
		Assert.Equal(1001, entry.AttackerObjectId);
		Assert.Equal(55, entry.Damage);
		Assert.Equal(55, entry.Hate);
		Assert.False(entry.NotifyAttack);
		Assert.Equal(WorldNpcDamageHopType.Damage, entry.HopType);
	}

	[Fact]
	public void IncrementAttackedCount_TracksPostReduceAttackCount()
	{
		var service = new WorldNpcCombatStateService();

		service.IncrementAttackedCount(1);
		var state = service.IncrementAttackedCount(1);

		Assert.Equal(2, state.AttackedCount);
		Assert.Empty(state.HateEntries);
	}

	[Fact]
	public void Clear_RemovesCombatRuntimeState()
	{
		var service = new WorldNpcCombatStateService();

		service.AddDamage(1, 1001, 25, notifyAttack: true, WorldNpcDamageHopType.Damage);
		service.IncrementAttackedCount(1);
		service.Clear(1);

		Assert.False(service.TryGetState(1, out _));
	}
}
