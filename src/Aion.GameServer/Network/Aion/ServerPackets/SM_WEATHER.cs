using Aion.GameServer.Model.Templates.World;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_WEATHER (ATracer, Kwazar, Nemesiss). Sends per-zone weather codes. WeatherEntry red-tolerated.</summary>
public class SM_WEATHER : AionServerPacket
{
    private WeatherEntry[] weatherEntries;

    public SM_WEATHER(WeatherEntry[] weatherEntries)
    {
        this.weatherEntries = weatherEntries;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(0x00);// unk
        WriteC(weatherEntries.Length);
        foreach (WeatherEntry entry in weatherEntries)
            WriteC(entry.GetCode());
    }
}
