using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Services.Transfers;

/// <summary>Java parity: services/transfers/TransferablePlayer. Java byte → sbyte.</summary>
public class TransferablePlayer
{
    public int playerId;
    public int accountId;
    public int targetAccountId;
    public Player player;
    public sbyte targetServerId;
    public int taskId;

    public TransferablePlayer(int playerId, int accountId, int targetAccountId)
    {
        this.playerId = playerId;
        this.accountId = accountId;
        this.targetAccountId = targetAccountId;
    }
}
