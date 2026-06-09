using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.GameObjects.Player;

/// <summary>Java parity: model/gameobjects/player/BindPointPosition implements Persistable.</summary>
public class BindPointPosition : IPersistable
{
    private int mapId;
    private float x;
    private float y;
    private float z;
    private byte heading;
    private IPersistable.PersistentState persistentState;

    public BindPointPosition(int mapId, float x, float y, float z, byte heading)
    {
        this.mapId = mapId;
        this.x = x;
        this.y = y;
        this.z = z;
        this.heading = heading;
        this.persistentState = IPersistable.PersistentState.NEW;
    }

    /// <summary>Returns the mapId.</summary>
    public int GetMapId()
    {
        return mapId;
    }

    /// <summary>Returns the x.</summary>
    public float GetX()
    {
        return x;
    }

    /// <summary>Returns the y.</summary>
    public float GetY()
    {
        return y;
    }

    /// <summary>Returns the z.</summary>
    public float GetZ()
    {
        return z;
    }

    /// <summary>Returns the heading.</summary>
    public byte GetHeading()
    {
        return heading;
    }

    /// <summary>the persistentState</summary>
    public IPersistable.PersistentState GetPersistentState()
    {
        return persistentState;
    }

    /// <param name="persistentState">the persistentState to set</param>
    public void SetPersistentState(IPersistable.PersistentState persistentState)
    {
        switch (persistentState)
        {
            case IPersistable.PersistentState.UPDATE_REQUIRED:
                if (this.persistentState == IPersistable.PersistentState.NEW)
                    break;
                goto default;
            default:
                this.persistentState = persistentState;
                break;
        }
    }
}
