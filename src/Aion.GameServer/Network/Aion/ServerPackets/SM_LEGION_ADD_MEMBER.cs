using Aion.GameServer.Configs.Network;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

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
