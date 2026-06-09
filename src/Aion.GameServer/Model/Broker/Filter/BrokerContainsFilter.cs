using System.Linq;
using Aion.GameServer.Model.Templates.Item;

namespace Aion.GameServer.Model.Broker.Filter;

/// <summary>Java parity: model/broker/filter/BrokerContainsFilter (ATracer).</summary>
public class BrokerContainsFilter : BrokerFilter
{
    private int[] masks;

    public BrokerContainsFilter(params int[] masks)
    {
        this.masks = masks;
    }

    public override bool Accept(ItemTemplate template)
    {
        return masks.Contains(template.GetTemplateId() / 100000);
    }
}
