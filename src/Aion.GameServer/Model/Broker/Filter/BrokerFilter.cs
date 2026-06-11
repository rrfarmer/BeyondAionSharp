using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Model.Broker.Filter;

/// <summary>Java parity: model/broker/filter/BrokerFilter (ATracer).</summary>
public abstract class BrokerFilter
{
    public abstract bool Accept(ItemTemplate template);
}
