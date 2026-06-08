using Aion.GameServer.Model;

namespace Aion.GameServer.GeoEngine.Collision;

/// <summary>
/// Java parity: geoEngine/collision/IgnoreProperties.
/// Java's <c>race</c> can be null (enum is a reference); C# uses a nullable <see cref="Race"/>.
/// </summary>
public class IgnoreProperties
{
    public static readonly IgnoreProperties ELYOS = new(Race.ELYOS, 0);
    public static readonly IgnoreProperties ASMODIANS = new(Race.ASMODIANS, 0);
    public static readonly IgnoreProperties BALAUR = new(Race.DRAKAN, 0);
    public static readonly IgnoreProperties ANY_RACE = new(null, 0);

    private readonly Race? _race;
    private readonly int _staticId;

    private IgnoreProperties(Race? race, int staticId)
    {
        _race = race;
        _staticId = staticId;
    }

    public static IgnoreProperties Of(Race? race, int staticId)
    {
        if (staticId == 0)
        {
            if (race == Race.ELYOS)
                return ELYOS;
            if (race == Race.ASMODIANS)
                return ASMODIANS;
            if (race == Race.DRAKAN)
                return BALAUR;
        }
        return new IgnoreProperties(race, staticId);
    }

    public static IgnoreProperties Of(Race? race)
    {
        return Of(race, 0);
    }

    public static IgnoreProperties Of(int staticId)
    {
        return Of(null, staticId);
    }

    public Race? GetRace()
    {
        return _race;
    }

    public int GetStaticId()
    {
        return _staticId;
    }

    public override string ToString()
    {
        return "[IgnoreProperties] Race: " + _race + " staticId: " + _staticId;
    }
}
