using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_HOUSE_RENDER (Rolandas, Neon). Renders a house via AbstractHouseInfoPacket.WriteCommonInfo. House red-tolerated.</summary>
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
