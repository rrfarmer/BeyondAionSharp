using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Item;

namespace Aion.GameServer.Model.Broker.Filter;

/// <summary>Java parity: model/broker/filter/BrokerPlayerClassFilter.</summary>
public class BrokerPlayerClassFilter : BrokerFilter
{
    private PlayerClass playerClass;

    public BrokerPlayerClassFilter(PlayerClass playerClass)
        : base()
    {
        this.playerClass = playerClass;
    }

    public override bool Accept(ItemTemplate template)
    {
        return template.IsClassSpecific(playerClass);
    }
}
