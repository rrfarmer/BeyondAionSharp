namespace Aion.GameServer.Model.GameObjects;

public sealed record PendingSoulBindRequest(int ItemObjectId, long Slot, string ItemName);
