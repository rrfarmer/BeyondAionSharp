using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Model.Broker.Filter;

/// <summary>Java parity: model/broker/filter/BrokerMinMaxFilter (ATracer).</summary>
public class BrokerMinMaxFilter : BrokerFilter
{
    private readonly int min;
    private readonly int max;

    public BrokerMinMaxFilter(int min, int max)
    {
        this.min = min;
        this.max = max;
    }

    public override bool Accept(ItemTemplate template)
    {
        int templateMask = template.GetTemplateId() / 100000;
        return templateMask >= min && templateMask <= max;
    }
}
