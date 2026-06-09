namespace Aion.GameServer.Model.GameObjects.Player;

/// <summary>Represents a player who has been blocked. Java parity: model/gameobjects/player/BlockedPlayer.</summary>
public class BlockedPlayer
{
    private readonly int objId;
    private readonly string name;
    private string reason;
    private readonly object _lock = new object();

    public BlockedPlayer(int objId, string name, string reason)
    {
        this.objId = objId;
        this.name = name;
        this.reason = reason;
    }

    public int GetObjId()
    {
        return objId;
    }

    public string GetName()
    {
        return name;
    }

    public string GetReason()
    {
        lock (_lock)
        {
            return reason;
        }
    }

    public void SetReason(string reason)
    {
        lock (_lock)
        {
            this.reason = reason;
        }
    }
}
