using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_UPDATE_NOTE (xavier). Updates a player's target note (objId + note). Player red-tolerated.</summary>
public class SM_UPDATE_NOTE : AionServerPacket
{
    private readonly int targetObjId;
    private readonly string note;

    public SM_UPDATE_NOTE(Player player)
    {
        this.targetObjId = player.GetObjectId();
        this.note = player.GetCommonData().GetNote();
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(targetObjId);
        WriteS(note);
    }
}
