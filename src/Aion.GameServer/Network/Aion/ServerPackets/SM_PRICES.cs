using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Trade;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_PRICES (xavier, Sarynth, Wakizashi). Sends influence-based buying price %, price modifier, and tax for the player's race. Converges PlayerEnterWorldService; uses ported PricesService. AionServerPacket red-tolerated.</summary>
public class SM_PRICES : AionServerPacket
{
    protected override void WriteImpl(AionConnection con)
    {
        WriteC(PricesService.GetGlobalPrices(con.GetActivePlayer().GetRace())); // Display Buying Price %
        WriteC(PricesService.GetGlobalPricesModifier()); // Buying Modified Price %
        WriteC(PricesService.GetTaxes(con.GetActivePlayer().GetRace())); // Tax = -100 + C %
    }
}
