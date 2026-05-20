namespace Aion.GameServer.Model.GameObjects;

// Java parity: services/BrokerService.getSettledItemsForPlayer + extractEarnedKinahForSoldItems.
public sealed record PlayerBrokerSettlementSummary(int SettledItemCount, long EarnedKinah)
{
	public bool HasSettledItems => SettledItemCount > 0;

	public static PlayerBrokerSettlementSummary Empty { get; } = new(0, 0);
}
