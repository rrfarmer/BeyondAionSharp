using System;

namespace Aion.GameServer.Model.Broker;

/// <summary>
/// Java parity: model/broker/BrokerMessages (kosyachok). Per-instance id (not ordinal) → enum + extension GetId().
/// </summary>
public enum BrokerMessages
{
    CANT_REGISTER_ITEM,
    NO_SPACE_AVAIABLE,
    NO_ENOUGHT_KINAH
}

public static class BrokerMessagesExtensions
{
    public static int GetId(this BrokerMessages m) => m switch
    {
        BrokerMessages.CANT_REGISTER_ITEM => 2,
        BrokerMessages.NO_SPACE_AVAIABLE => 3,
        BrokerMessages.NO_ENOUGHT_KINAH => 5,
        _ => throw new ArgumentOutOfRangeException(),
    };
}
