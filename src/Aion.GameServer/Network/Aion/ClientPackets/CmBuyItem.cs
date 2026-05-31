using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed record CmBuyItemEntry(
	int ItemObjectId,
	long Count);

public sealed class CmBuyItem : GameClientPacket
{
	public const int MaxItemAmount = 36;
	public const long MaxItemCount = 20_000;

	public CmBuyItem(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int SellerObjectId { get; private set; }

	public int TradeActionId { get; private set; }

	public int Amount { get; private set; }

	public bool IsAudit { get; private set; }

	public IReadOnlyList<CmBuyItemEntry> Items { get; private set; } = Array.Empty<CmBuyItemEntry>();

	public CmBuyItemEntry? AuditItem { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_BUY_ITEM.readImpl.
		SellerObjectId = buffer.ReadD();
		TradeActionId = buffer.ReadH();
		Amount = buffer.ReadH();

		if (Amount > MaxItemAmount)
		{
			IsAudit = true;
			return;
		}

		var items = new List<CmBuyItemEntry>(Amount);
		for (var i = 0; i < Amount; i++)
		{
			var item = new CmBuyItemEntry(buffer.ReadD(), buffer.ReadQ());
			if (item.Count < 0 || (item.ItemObjectId <= 0 && TradeActionId != 0) || item.Count > MaxItemCount)
			{
				IsAudit = true;
				AuditItem = item;
				break;
			}

			items.Add(item);
		}

		Items = items;
	}
}
