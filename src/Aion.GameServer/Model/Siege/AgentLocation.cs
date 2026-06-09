using Aion.GameServer.Model.Templates.Siegelocation;

namespace Aion.GameServer.Model.Siege;

/// <summary>Java parity: model/siege/AgentLocation (Estrayl).</summary>
public class AgentLocation : SiegeLocation
{
    public AgentLocation(SiegeLocationTemplate template)
        : base(template)
    {
    }

    public override int GetNextState()
    {
        return IsVulnerable() ? STATE_INVULNERABLE : STATE_VULNERABLE;
    }

    public override SiegeRace GetRace()
    {
        return SiegeRace.BALAUR;
    }
}
