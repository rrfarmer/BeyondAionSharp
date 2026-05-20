using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmReceiveBids : GameServerPacket
{
	public const int PacketOpCode = 259;
	private const string HouseAuctionMailSender = "$$HS_AUCTION_MAIL";

	private readonly int _unknown;

	public SmReceiveBids(int unknown)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_RECEIVE_BIDS(int).
		_unknown = unknown;
	}

	public static SmReceiveBids? CreateLoginPacket(Player player)
	{
		// Java parity: services/HousingBidService.onPlayerLogin refresh trigger from mailbox system letters.
		if (player.Mailbox.Any(IsNewAuctionResultMail))
			return new SmReceiveBids(0);
		return null;

		bool IsNewAuctionResultMail(PlayerMail mail)
		{
			if (!mail.IsUnread || !mail.SenderName.StartsWith(HouseAuctionMailSender, StringComparison.Ordinal))
				return false;
			if (player.LastOnline.HasValue && player.LastOnline.Value > mail.ReceivedTime)
				return false;

			var titleParts = mail.Title.Split(',');
			if (titleParts.Length == 0 || !int.TryParse(titleParts[0], out var resultId))
				return false;

			return resultId is 0 or 2 or 3 or 4 or 5 or 7;
		}
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_RECEIVE_BIDS.writeImpl.
		buffer.WriteD(_unknown);
	}
}
