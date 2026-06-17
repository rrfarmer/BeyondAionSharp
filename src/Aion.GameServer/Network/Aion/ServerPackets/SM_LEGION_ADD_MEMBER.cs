using Aion.GameServer.Configs.Network;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Model;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_LEGION_ADD_MEMBER (Simple). New legion member info (objId/name/rank/isMember/class/level/map + msg). Player/NetworkConfig red-tolerated.</summary>
public class SM_LEGION_ADD_MEMBER : AionServerPacket
{
    private Player player;
    private bool isMember;
    private int msgId;
    private string text;

    public SM_LEGION_ADD_MEMBER(Player player, bool isMember, int msgId, string text)
    {
        this.player = player;
        this.isMember = isMember;
        this.msgId = msgId;
        this.text = text;
    }

    protected override void WriteImpl(AionConnection con)
    {
        // Java parity (writeImpl audited 1:1 vs game-server/src/com/aionemu/gameserver/network/aion/serverpackets/SM_LEGION_ADD_MEMBER.java): 2026-06-17. Live Player + LegionMember graph.
        WriteD(player.GetObjectId());
        WriteS(player.GetName());
        WriteC(player.GetLegionMember().GetRank().GetRankId());
        WriteC(isMember ? 0x01 : 0x00);// is New Member?
        WriteC(player.GetCommonData().GetPlayerClass().GetClassId());
        WriteC(player.GetLevel());
        WriteD(player.GetPosition().GetMapId());
        WriteD(NetworkConfig.GAMESERVER_ID); // TODO: add to account model?
        WriteD(msgId);
        WriteS(text);
    }
}
