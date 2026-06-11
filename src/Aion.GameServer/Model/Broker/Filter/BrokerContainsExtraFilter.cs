using System.Linq;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Model.Broker.Filter;

/// <summary>Java parity: model/broker/filter/BrokerContainsExtraFilter (ATracer).</summary>
public class BrokerContainsExtraFilter : BrokerFilter
{
    private int[] masks;

    public BrokerContainsExtraFilter(params int[] masks)
    {
        this.masks = masks;
    }

    public override bool Accept(ItemTemplate template)
    {
        return masks.Contains(template.GetTemplateId() / 10000);
    }
}
