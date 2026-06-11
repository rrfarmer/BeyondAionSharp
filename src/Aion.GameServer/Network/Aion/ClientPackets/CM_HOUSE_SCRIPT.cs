using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_HOUSE_SCRIPT (Rolandas, Neon, Sykra). Uploads/removes a house script (sign script) with size limits. PlayerScripts/SM_HOUSE_SCRIPTS red-tolerated.</summary>
public class CM_HOUSE_SCRIPT : AionClientPacket
{
    private int address;
    private int scriptId;
    private int totalSize;
    private int compressedSize;
    private int uncompressedSize;
    private byte[] scriptContent;

    public CM_HOUSE_SCRIPT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        address = ReadD();
        scriptId = ReadUC();
        totalSize = ReadUH();
        if (totalSize > 0)
        {
            compressedSize = ReadD();
            if (compressedSize <= SM_HOUSE_SCRIPTS.MAX_COMPRESSED_SCRIPT_SIZE)
            {
                uncompressedSize = ReadD();
                scriptContent = ReadB(compressedSize);
            }
        }
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (compressedSize > SM_HOUSE_SCRIPTS.MAX_COMPRESSED_SCRIPT_SIZE)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_SCRIPT_OVERFLOW());
            return;
        }
        House house = player.GetActiveHouse();
        if (house == null || house.GetAddress().GetId() != address)
        {
            AuditLogger.Log(player, "tried to modify script of house they don't own (address " + address + ")");
            return;
        }
        PlayerScripts scripts = house.GetPlayerScripts();
        if (totalSize == 0)
            scripts.Remove(scriptId);
        else
            scripts.Set(scriptId, scriptContent, uncompressedSize);
        PacketSendUtility.BroadcastPacket(player, new SM_HOUSE_SCRIPTS(address, scripts.Get(scriptId)));
    }
}
