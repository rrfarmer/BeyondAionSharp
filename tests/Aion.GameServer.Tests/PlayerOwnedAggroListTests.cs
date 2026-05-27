using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests;

public sealed class PlayerOwnedAggroListTests
{
	[Fact]
	public void TryAddKnownAttacker_UsesPlayerKnownListOnlyAwareness()
	{
		var aggroList = new PlayerOwnedAggroList();

		var accepted = aggroList.TryAddKnownAttacker(
			attackerObjectId: KnownAttackerObjectId,
			damage: 25,
			hate: 250,
			ownerKnownListKnowsAttacker: true);
		var rejected = aggroList.TryAddKnownAttacker(
			attackerObjectId: UnknownAttackerObjectId,
			damage: 50,
			hate: 500,
			ownerKnownListKnowsAttacker: false);

		Assert.True(accepted);
		Assert.False(rejected);
		var entry = Assert.Single(aggroList.Entries);
		Assert.Equal(KnownAttackerObjectId, entry.AttackerObjectId);
		Assert.Equal(25, entry.Damage);
		Assert.Equal(250, entry.Hate);
	}

	[Fact]
	public void TryAddKnownAttacker_AccumulatesDamageAndClampsHateLikeAggroInfo()
	{
		var aggroList = new PlayerOwnedAggroList();

		aggroList.TryAddKnownAttacker(KnownAttackerObjectId, damage: 10, hate: 0, ownerKnownListKnowsAttacker: true);
		aggroList.TryAddKnownAttacker(KnownAttackerObjectId, damage: -5, hate: -100, ownerKnownListKnowsAttacker: true);

		var entry = Assert.Single(aggroList.Entries);
		Assert.Equal(10, entry.Damage);
		Assert.Equal(2, entry.Hate);
	}

	[Fact]
	public void Clear_ReturnsEntriesClearsAllAndCancelsHateReductionTask()
	{
		var aggroList = new PlayerOwnedAggroList();
		aggroList.TryAddKnownAttacker(KnownAttackerObjectId, damage: 25, hate: 250, ownerKnownListKnowsAttacker: true);
		aggroList.TryAddKnownAttacker(SecondAttackerObjectId, damage: 5, hate: 50, ownerKnownListKnowsAttacker: true);
		aggroList.MarkHateReductionTaskActiveForParity();

		var clearedEntries = aggroList.Clear();

		Assert.Equal([KnownAttackerObjectId, SecondAttackerObjectId], clearedEntries.Select(entry => entry.AttackerObjectId));
		Assert.Empty(aggroList.Entries);
		Assert.False(aggroList.HasHateReductionTask);
	}

	[Fact]
	public void PlayerOwnsExecutableAggroList()
	{
		var player = new Player { ObjectId = OwnerPlayerObjectId };

		var added = player.AggroList.TryAddKnownAttacker(
			KnownAttackerObjectId,
			damage: 80,
			hate: 800,
			ownerKnownListKnowsAttacker: true);

		Assert.True(added);
		Assert.Equal(KnownAttackerObjectId, Assert.Single(player.AggroList.Entries).AttackerObjectId);
	}

	private const int OwnerPlayerObjectId = 1001;
	private const int KnownAttackerObjectId = 2001;
	private const int UnknownAttackerObjectId = 2002;
	private const int SecondAttackerObjectId = 2003;
}
