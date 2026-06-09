using Aion.GameServer.Model.Templates.Item;

namespace Aion.GameServer.Model.Broker.Filter;

/// <summary>Java parity: model/broker/filter/BrokerAllAcceptFilter (ATracer).</summary>
public class BrokerAllAcceptFilter : BrokerFilter
{
    public override bool Accept(ItemTemplate template)
    {
        return true;
    }
}
