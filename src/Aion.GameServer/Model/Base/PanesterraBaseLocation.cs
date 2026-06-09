using Aion.GameServer.Model.Templates.Base;

namespace Aion.GameServer.Model.Base;

/// <summary>Java parity: model/base/PanesterraBaseLocation (Estrayl).</summary>
public class PanesterraBaseLocation : BaseLocation
{
    public PanesterraBaseLocation(BaseTemplate template)
        : base(template)
    {
        if (template.GetType_() == BaseType.PANESTERRA_FACTION_CAMP)
            occupier = BaseOccupier.PEACE;
    }
}
