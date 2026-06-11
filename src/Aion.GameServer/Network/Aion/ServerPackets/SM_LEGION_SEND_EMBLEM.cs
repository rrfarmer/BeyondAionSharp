using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_LEGION_SEND_EMBLEM (Simple, cura, Neon). Sends a legion emblem header (id/type/dataSize/argb/name) before emblem byte data. LegionEmblem red-tolerated.</summary>
public class SM_LEGION_SEND_EMBLEM : AionServerPacket
{
    private int legionId;
    private int emblemId;
    private byte emblemType;
    private int emblemDataSize; // used when EMBLEM_DATA is sent afterwards, so the client knows the incoming byte length
    private byte color_a;
    private byte color_r;
    private byte color_g;
    private byte color_b;
    private string legionName;

    public SM_LEGION_SEND_EMBLEM(int legionId, LegionEmblem emblem, int emblemDataSize, string legionName)
    {
        this.legionId = legionId;
        this.emblemId = emblem.GetEmblemId();
        this.emblemType = emblem.GetEmblemType().GetValue();
        this.emblemDataSize = emblemDataSize;
        this.color_a = emblem.GetColor_a();
        this.color_r = emblem.GetColor_r();
        this.color_g = emblem.GetColor_g();
        this.color_b = emblem.GetColor_b();
        this.legionName = legionName;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(legionId);
        WriteC(emblemId);
        WriteC(emblemType);
        WriteD(emblemDataSize);
        WriteC(color_a);
        WriteC(color_r);
        WriteC(color_g);
        WriteC(color_b);
        WriteS(legionName);
        WriteC(0x01);
    }
}
