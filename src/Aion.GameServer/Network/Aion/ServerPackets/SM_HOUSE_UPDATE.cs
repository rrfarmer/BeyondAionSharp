using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_HOUSE_UPDATE (Rolandas, Neon). House settings update (3 unk shorts + WriteCommonInfo). House red-tolerated.</summary>
// Java parity (writeImpl audited 1:1 vs game-server/.../SM_HOUSE_UPDATE.java + AbstractHouseInfoPacket.writeCommonInfo): 2026-06-17 — 3 unk shorts then LegionService singleton + live House/registry/emblem graph; PartType.values()/getRooms() order+count match (T2 audit-only).
public class SM_HOUSE_UPDATE : AbstractHouseInfoPacket
{
    public SM_HOUSE_UPDATE(House house)
        : base(house)
    {
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(1); // unk
        WriteH(0);
        WriteH(1); // unk (if this is 0 any changed house settings are ignored on client side)

        WriteCommonInfo();
    }
}
