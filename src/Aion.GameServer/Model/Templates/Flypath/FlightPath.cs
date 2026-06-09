namespace Aion.GameServer.Model.Templates.Flypath;

/// <summary>Java parity: model/templates/flypath/FlightPath.</summary>
public class FlightPath
{
    private readonly Type type;
    private readonly int id;
    private int distance;

    public FlightPath(Type type, int id, int distance)
    {
        this.type = type;
        this.id = id;
        this.distance = distance;
    }

    public Type GetType_()
    {
        return type;
    }

    public int GetId()
    {
        return id;
    }

    public int GetDistance()
    {
        return distance;
    }

    public void SetDistance(int distance)
    {
        this.distance = distance;
    }

    public enum Type
    {
        FLIGHT_TRANSPORTER,
        WINDSTREAM,
    }
}
