using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Item;

namespace Aion.GameServer.Model.Broker.Filter;

/// <summary>Java parity: model/broker/filter/BrokerPlayerClassExtraFilter.</summary>
public class BrokerPlayerClassExtraFilter : BrokerPlayerClassFilter
{
    private int mask;

    public BrokerPlayerClassExtraFilter(int mask, PlayerClass playerClass)
        : base(playerClass)
    {
        this.mask = mask;
    }

    public override bool Accept(ItemTemplate template)
    {
        return base.Accept(template) && mask == template.GetTemplateId() / 100000;
    }
}
