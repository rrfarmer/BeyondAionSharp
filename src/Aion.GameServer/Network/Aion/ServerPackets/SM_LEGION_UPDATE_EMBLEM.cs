using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_LEGION_UPDATE_EMBLEM (Simple, cura, Neon). Updates a legion emblem (id/type/argb). LegionEmblem red-tolerated.</summary>
public class SM_LEGION_UPDATE_EMBLEM : AionServerPacket
{
    private int legionId;
    private byte emblemId;
    private byte color_a;
    private byte color_r;
    private byte color_g;
    private byte color_b;
    private byte emblemType;

    public SM_LEGION_UPDATE_EMBLEM(int legionId, LegionEmblem emblem)
    {
        this.legionId = legionId;
        this.emblemId = emblem.GetEmblemId();
        this.color_a = emblem.GetColor_a();
        this.color_r = emblem.GetColor_r();
        this.color_g = emblem.GetColor_g();
        this.color_b = emblem.GetColor_b();
        this.emblemType = emblem.GetEmblemType().GetValue();
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(legionId);
        WriteC(emblemId);
        WriteC(emblemType);
        WriteC(color_a);
        WriteC(color_r);
        WriteC(color_g);
        WriteC(color_b);
    }
}
