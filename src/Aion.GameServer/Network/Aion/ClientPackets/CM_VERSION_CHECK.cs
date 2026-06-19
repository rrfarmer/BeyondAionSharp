using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Event;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_VERSION_CHECK (-Nemesiss-). Client version handshake; replies SM_VERSION_CHECK with the current event theme. EventService/SM_VERSION_CHECK red-tolerated.</summary>
public class CM_VERSION_CHECK : AionClientPacket
{
    private int aionClientVersion;
    private int npcScriptInterfaceVersion;
    private int windowsEncoding;
    private int windowsVersion;
    private int windowsSubVersion;
    private int liteInfo;

    public CM_VERSION_CHECK(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        aionClientVersion = ReadUH();
        npcScriptInterfaceVersion = ReadUH();
        windowsEncoding = ReadD();
        windowsVersion = ReadD();
        windowsSubVersion = ReadD();
        liteInfo = ReadC(); // info if client is fully downloaded? seen values: 1, 2 (client checks for "2-ESSENTIAL" in data\lite\LiteGroupOrder.xml)
    }

    protected override void RunImpl()
    {
        System.Console.Error.WriteLine($"[NET] CM_VERSION_CHECK: aionClientVersion={aionClientVersion} (INTERNAL_VERSION={SM_VERSION_CHECK.INTERNAL_VERSION}) npcScriptIfaceVer={npcScriptInterfaceVersion} liteInfo={liteInfo}");
        SendPacket(new SM_VERSION_CHECK(aionClientVersion, EventService.GetInstance().GetEventTheme()));
    }
}
