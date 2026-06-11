using System;
using System.Collections.Generic;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_HOUSE_SCRIPTS (Rolandas, Neon, Sykra). Sends a house's player scripts (compressed bytes + padding) or removes them. Converges PlayerEnterWorldService (SplitList paging via STATIC_BODY_SIZE/DYNAMIC_BODY_PART_SIZE_CALCULATOR). Function->Func; Collections.empty/singletonList->new List; SCRIPT_PADDING -51==0xCD; PlayerScript red-tolerated (deferred text-block).</summary>
public class SM_HOUSE_SCRIPTS : AionServerPacket
{
    public const int STATIC_BODY_SIZE = 6;
    private static readonly byte[] SCRIPT_PADDING = { 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD }; // Java -51; values seem to not matter, but length does
    public static readonly int MAX_COMPRESSED_SCRIPT_SIZE = AionServerPacket.MAX_USABLE_PACKET_BODY_SIZE - STATIC_BODY_SIZE - 11 - SCRIPT_PADDING.Length;
    public static readonly Func<PlayerScript, int> DYNAMIC_BODY_PART_SIZE_CALCULATOR =
        script => script.HasData() ? 11 + script.CompressedBytes().Length + SCRIPT_PADDING.Length : 3;

    private readonly int houseAddress;
    private readonly List<PlayerScript> scripts;

    public SM_HOUSE_SCRIPTS(int houseAddress, PlayerScript script)
    {
        this.houseAddress = houseAddress;
        this.scripts = script == null ? new List<PlayerScript>() : new List<PlayerScript> { script };
    }

    public SM_HOUSE_SCRIPTS(int houseAddress, List<PlayerScript> scripts)
    {
        this.houseAddress = houseAddress;
        this.scripts = scripts;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(houseAddress);
        WriteH(scripts.Count);
        foreach (PlayerScript script in scripts)
        {
            WriteC(script.Id());
            if (script.HasData())
            {
                byte[] scriptContent = script.CompressedBytes();
                WriteH(8 + scriptContent.Length + SCRIPT_PADDING.Length); // total following byte size for this script
                WriteD(scriptContent.Length + SCRIPT_PADDING.Length);
                WriteD(script.UncompressedSize());
                WriteB(scriptContent);
                WriteB(SCRIPT_PADDING);
            }
            else
            {
                WriteH(0); // removes script from the in-game list
            }
        }
    }
}
