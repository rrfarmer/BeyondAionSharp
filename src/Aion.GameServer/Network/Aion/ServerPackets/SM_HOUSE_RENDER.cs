using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_HOUSE_RENDER (Rolandas, Neon). Renders a house via AbstractHouseInfoPacket.WriteCommonInfo. House red-tolerated.</summary>
// Java parity (writeImpl audited 1:1 vs game-server/.../SM_HOUSE_RENDER.java + AbstractHouseInfoPacket.writeCommonInfo): 2026-06-17 — reads LegionService singleton + live House/registry/emblem graph; PartType.values()/getRooms() order+count match (T2 audit-only).
public class SM_HOUSE_RENDER : AbstractHouseInfoPacket
{
    public SM_HOUSE_RENDER(House house)
        : base(house)
    {
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteCommonInfo();
    }
}
