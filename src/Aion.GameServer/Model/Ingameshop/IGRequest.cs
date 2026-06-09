namespace Aion.GameServer.Model.Ingameshop;

/// <summary>Java parity: model/ingameshop/IGRequest.</summary>
public class IGRequest
{
    public IGRequest(int requestId, int playerId, int itemObjId)
    {
        this.requestId = requestId;
        this.playerId = playerId;
        this.itemObjId = itemObjId;
    }

    public IGRequest(int requestId, int playerId, string receiver, string message, int itemObjId)
    {
        this.requestId = requestId;
        this.playerId = playerId;
        this.receiver = receiver;
        this.message = message;
        this.itemObjId = itemObjId;
        gift = true;
    }

    public IGRequest(int requestId, int playerId, int cost, bool a)
    {
        this.requestId = requestId;
        this.playerId = playerId;
        this.cost = cost;
        sync = a;
    }

    public bool gift = false, sync = false;
    public int playerId;
    public int cost;
    public int requestId, itemObjId;
    public string receiver, message;
    public int accountId;
}
