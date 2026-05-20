using Aion.GameServer.World;

namespace Aion.GameServer.Model.GameObjects;

public sealed class Player
{
	public int ObjectId { get; init; }

	public int AccountId { get; init; }

	public string Name { get; init; } = string.Empty;

	public string PlayerClass { get; init; } = string.Empty;

	public string Race { get; init; } = string.Empty;

	public string Gender { get; init; } = string.Empty;

	public long Exp { get; init; }

	public bool IsOnline { get; set; }

	public DateTime? LastOnline { get; set; }

	public WorldPosition Position { get; init; }

	public IReadOnlyList<InventoryItem> InventoryItems { get; set; } = Array.Empty<InventoryItem>();
}
