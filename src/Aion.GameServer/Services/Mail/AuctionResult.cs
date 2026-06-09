using System;

namespace Aion.GameServer.Services.Mail;

/// <summary>Java parity: services/mail/AuctionResult. id==ordinal→GetId()=(int)t; getResultFromId null-on-miss→AuctionResult?.</summary>
public enum AuctionResult
{
    FAILED_BID,
    CANCELED_BID,
    FAILED_SALE,
    SUCCESS_SALE,
    WIN_BID,
    GRACE_START,
    GRACE_FAIL,
    GRACE_SUCCESS
}

public static class AuctionResultExtensions
{
    public static int GetId(this AuctionResult r) => (int) r;

    public static AuctionResult? GetResultFromId(int resultId)
    {
        foreach (AuctionResult result in Enum.GetValues(typeof(AuctionResult)))
        {
            if (result.GetId() == resultId)
                return result;
        }
        return null;
    }
}
