using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.ConquerorAndProtectorSystem;
using static Aion.GameServer.Network.Aion.ServerPackets.AbstractPlayerInfoPacket;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_CONQUEROR_PROTECTOR (Source, xTz; SM_SERIAL_KILLER pre-4.8). Sends conqueror/protector buff level + intruder-scan cooldown, intruder lists (rank/pos/name), or broadcast rank packets, keyed by type. Converges ConquerorAndProtectorService SM_CONQUEROR_PROTECTOR usages. switch fallthrough->stacked case labels; Collection->ICollection; getName(true)->GetName(true). AionServerPacket/write* methods red-tolerated.</summary>
public class SM_CONQUEROR_PROTECTOR : AionServerPacket
{
    private int type;
    private int buffLvl;
    private int cooldown;
    private Player player;
    private ICollection<Player> intruders;

    public SM_CONQUEROR_PROTECTOR(int type, int buffLvl, int cooldown)
    {
        this.type = type;
        this.buffLvl = buffLvl;
        this.cooldown = cooldown;
    }

    public SM_CONQUEROR_PROTECTOR(int type, int buffLvl)
    {
        this.type = type;
        this.buffLvl = buffLvl;
    }

    public SM_CONQUEROR_PROTECTOR(int type, Player player)
    {
        this.type = type;
        this.player = player;
    }

    public SM_CONQUEROR_PROTECTOR(ICollection<Player> intruders, bool displayCd)
    {
        this.type = displayCd ? 5 : 4;
        this.intruders = intruders;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(type);
        WriteD(0x01);
        WriteD(0x01);
        switch (type)
        {
            case 0: // conqueror + no announcement
            case 1: // conqueror + announcement
            case 7: // protector + cd no announcement
            case 8: // protector + announcement
                WriteH(0x01);
                WriteD(buffLvl);
                WriteD(cooldown); // intruder scan cooldown
                break;
            case 4: // intruder scan (without cd)
            case 5: // intruder scan (with cd)
                WriteH(intruders.Count);
                foreach (Player player in intruders)
                {
                    CPInfo info = ConquerorAndProtectorService.GetInstance().GetCPInfoForCurrentMap(player);
                    WriteD(info == null ? 0 : info.GetRank());
                    WriteD(player.GetObjectId());
                    WriteD(0x01); // unk
                    WriteD(player.GetAbyssRank().GetRank().GetId());
                    WriteH(player.GetLevel());
                    WriteF(player.GetX());
                    WriteF(player.GetY());
                    WriteS(player.GetName(true), CHARNAME_MAX_LENGTH);
                    WriteB(new byte[66]); // unk
                    WriteD(1941); // unk
                    WriteD(1942); // unk
                    WriteD(1943); // unk
                    WriteD(1944); // unk
                    WriteH(7); // unk
                }
                break;
            case 6: // conqueror
            case 9: // protector
                WriteH(0x01);// unk
                CPInfo info = ConquerorAndProtectorService.GetInstance().GetCPInfoForCurrentMap(player);
                WriteD(info == null ? 0 : info.GetRank());
                WriteD(player.GetObjectId());
                break;
        }
    }
}
