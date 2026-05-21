namespace Aion.GameServer.Model.GameObjects;

public sealed record PendingChargeAllRequest(
	int SenderObjectId,
	int ChargeWay,
	long PaymentAmount,
	IReadOnlyList<PendingChargeAllItem> Items);

public sealed record PendingChargeAllItem(
	int ObjectId,
	int ItemId,
	int PreviousCharge,
	int TargetCharge,
	int Level);
