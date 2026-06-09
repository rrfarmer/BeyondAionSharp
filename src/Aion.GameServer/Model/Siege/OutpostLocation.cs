using System.Collections.Generic;
using Aion.GameServer.Model.Templates.Siegelocation;

namespace Aion.GameServer.Model.Siege;

/// <summary>Java parity: model/siege/OutpostLocation.</summary>
public class OutpostLocation : SiegeLocation
{
    public OutpostLocation(SiegeLocationTemplate template)
        : base(template)
    {
    }

    public override int GetNextState()
    {
        return IsVulnerable() ? STATE_INVULNERABLE : STATE_VULNERABLE;
    }

    public List<int> GetFortressDependency()
    {
        return GetTemplate().GetFortressDependency();
    }

    public bool IsSilenteraAllowed()
    {
        return true;
    }
}
