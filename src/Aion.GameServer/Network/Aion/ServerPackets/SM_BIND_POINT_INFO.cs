using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.World;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_BIND_POINT_INFO (sweetkr, Sarynth, Sykra). Bind-point display (obelisk type 0 / Kisk type 4): map/x/y/z + kisk objId. Kisk/WorldPosition red-tolerated.</summary>
public class SM_BIND_POINT_INFO : AionServerPacket
{
    private readonly int mapId;
    private readonly float x;
    private readonly float y;
    private readonly float z;
    private readonly byte bindPointType;
    private readonly int kiskObjId;

    public SM_BIND_POINT_INFO(int mapId, float x, float y, float z)
    {
        this.mapId = mapId;
        this.x = x;
        this.y = y;
        this.z = z;
        this.bindPointType = 0;
        this.kiskObjId = 0;
    }

    public SM_BIND_POINT_INFO(Kisk kisk)
    {
        if (kisk == null || !kisk.IsActive())
        {
            this.mapId = 0;
            this.x = 0;
            this.y = 0;
            this.z = 0;
            this.kiskObjId = 0;
        }
        else
        {
            WorldPosition pos = kisk.GetPosition();
            this.mapId = pos.GetMapId();
            this.x = pos.GetX();
            this.y = pos.GetY();
            this.z = pos.GetZ();
            this.kiskObjId = kisk.GetObjectId();
        }
        this.bindPointType = 4;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(bindPointType); // 0 obelisk, 4 Kisk
        WriteC(0x01); // unk
        WriteD(mapId);// map id
        WriteF(x); // x
        WriteF(y); // y
        WriteF(z); // z
        WriteD(kiskObjId); // if 0 and in type = 4 will clear the current display
    }
}
