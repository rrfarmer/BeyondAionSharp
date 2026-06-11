namespace Aion.GameServer.Services.Players;

/// <summary>Java parity: services/player/PlayerMailboxState. Java byte → sbyte.</summary>
public class PlayerMailboxState
{
    public const sbyte CLOSED = 0x00;
    public const sbyte REGULAR = 0x01;
    public const sbyte EXPRESS = 0x02;
}
