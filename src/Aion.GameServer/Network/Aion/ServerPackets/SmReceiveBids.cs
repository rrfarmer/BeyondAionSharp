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
		if (EnumerateNewAuctionResults(player).Any())
			return new SmReceiveBids(0);
		return null;
	}

	public static IReadOnlyList<SmSystemMessage> CreateLoginSystemMessages(Player player)
	{
		// Java parity: services/HousingBidService.onPlayerLogin sends per-result SM_SYSTEM_MESSAGE before SM_RECEIVE_BIDS.
		var packets = new List<SmSystemMessage>();
		foreach (var result in EnumerateNewAuctionResults(player))
		{
			switch (result.Result)
			{
				case HousingAuctionResult.FailedBid:
					packets.Add(SmSystemMessage.HousingBidCancel());
					break;
				case HousingAuctionResult.WinBid:
				case HousingAuctionResult.GraceStart:
					if (result.Address.HasValue)
						packets.Add(SmSystemMessage.HousingBidWin(result.Address.Value));
					break;
				case HousingAuctionResult.FailedSale:
					if (result.Address.HasValue)
						packets.Add(SmSystemMessage.HousingAuctionFail(result.Address.Value));
					break;
				case HousingAuctionResult.SuccessSale:
				case HousingAuctionResult.GraceSuccess:
					if (result.Address.HasValue)
						packets.Add(SmSystemMessage.HousingAuctionSuccess(result.Address.Value));
					break;
			}
		}

		return packets;
	}

	private static IEnumerable<HousingAuctionLoginResult> EnumerateNewAuctionResults(Player player)
	{
		foreach (var mail in player.Mailbox)
		{
			if (!mail.IsUnread || !mail.SenderName.StartsWith(HouseAuctionMailSender, StringComparison.Ordinal))
				continue;
			if (player.LastOnline.HasValue && player.LastOnline.Value > mail.ReceivedTime)
				continue;

			var titleParts = mail.Title.Split(',');
			if (titleParts.Length == 0 || !int.TryParse(titleParts[0], out var resultId) || !IsLoginAuctionResult(resultId))
				continue;

			var bodyParts = mail.Message.Split(',');
			var address = bodyParts.Length > 1 && int.TryParse(bodyParts[1], out var parsedAddress) ? parsedAddress : (int?)null;
			yield return new HousingAuctionLoginResult((HousingAuctionResult)resultId, address);
		}
	}

	private static bool IsLoginAuctionResult(int resultId)
	{
		// Java parity: services/mail/AuctionResult values consumed by HousingBidService.onPlayerLogin.
		return resultId is
			(int)HousingAuctionResult.FailedBid or
			(int)HousingAuctionResult.FailedSale or
			(int)HousingAuctionResult.SuccessSale or
			(int)HousingAuctionResult.WinBid or
			(int)HousingAuctionResult.GraceStart or
			(int)HousingAuctionResult.GraceSuccess;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_RECEIVE_BIDS.writeImpl.
		buffer.WriteD(_unknown);
	}

	private enum HousingAuctionResult
	{
		FailedBid = 0,
		FailedSale = 2,
		SuccessSale = 3,
		WinBid = 4,
		GraceStart = 5,
		GraceSuccess = 7,
	}

	private readonly record struct HousingAuctionLoginResult(HousingAuctionResult Result, int? Address);
}
