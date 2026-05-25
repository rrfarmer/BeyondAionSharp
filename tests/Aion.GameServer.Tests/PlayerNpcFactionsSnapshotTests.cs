using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests;

public sealed class PlayerNpcFactionsSnapshotTests
{
	[Fact]
	public void CanStartAssignedQuest_MatchesJavaNpcFactionStartQuestGuard()
	{
		var snapshot = new PlayerNpcFactionsSnapshot(
		[
			new PlayerNpcFactionState(
				FactionId: 2,
				IsActive: true,
				IsMentor: false,
				TimeEpochSeconds: 0,
				State: PlayerNpcFactionQuestState.Noting,
				QuestId: 35007),
			new PlayerNpcFactionState(
				FactionId: 4,
				IsActive: false,
				IsMentor: false,
				TimeEpochSeconds: 0,
				State: PlayerNpcFactionQuestState.Noting,
				QuestId: 35008),
		]);

		Assert.True(snapshot.CanStartAssignedQuest(factionId: 2, questId: 35007));
		Assert.False(snapshot.CanStartAssignedQuest(factionId: 2, questId: 35008));
		Assert.False(snapshot.CanStartAssignedQuest(factionId: 4, questId: 35008));
		Assert.False(snapshot.CanStartAssignedQuest(factionId: 9, questId: 35009));
	}
}
