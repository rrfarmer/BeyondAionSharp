namespace Aion.GameServer.Services.Players;

/// <summary>Java parity: services/player/PlayerMailboxState. Java byte constants; mirrored as C# byte to match Mailbox.mailBoxState (byte).</summary>
public class PlayerMailboxState
{
    public const byte CLOSED = 0x00;
    public const byte REGULAR = 0x01;
    public const byte EXPRESS = 0x02;
}
