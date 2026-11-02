using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>
/// Java parity: network/aion/serverpackets/SM_SELECT_DECOMPOSABLE_RESULT (xTz).
/// Answers the selection the client made in the decomposable window. It carries no items, only whether the chosen one was handed out.
/// </summary>
public class SM_SELECT_DECOMPOSABLE_RESULT : AionServerPacket
{
    /*
     * The client closes the window on both results. Retail never sends NOT_GRANTED, its picks always land in the cube, even past its size.
     * We send it for a full cube, so a flood of picks can't overfill it, and for a box that is already gone.
     * Result 2 keeps the window open with "can't acquire the item right now" and answers a per item drop quota, which we don't have.
     */
    public const int GRANTED = 0;
    public const int NOT_GRANTED = 1;

    private readonly int objectId;
    private readonly int result;

    public SM_SELECT_DECOMPOSABLE_RESULT(int objectId, int result)
    {
        this.objectId = objectId;
        this.result = result;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteQ(objectId); // 64 bit object id
        WriteC(result);
    }
}
