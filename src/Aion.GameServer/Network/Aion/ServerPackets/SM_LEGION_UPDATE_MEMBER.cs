using Aion.GameServer.Configs.Network;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_LEGION_UPDATE_MEMBER (Simple). Updates a legion member entry (rank/class/level/world/online/lastonline + msg). LegionMember/NetworkConfig red-tolerated.</summary>
public class SM_LEGION_UPDATE_MEMBER : AionServerPacket
{
    private readonly LegionMember legionMember;
    private readonly int msgId;
    private readonly string text;

    public SM_LEGION_UPDATE_MEMBER(Player player, int msgId, string text)
        : this(player.GetLegionMember(), msgId, text)
    {
    }

    public SM_LEGION_UPDATE_MEMBER(LegionMember legionMember, int msgId, string text)
    {
        this.legionMember = legionMember;
        this.msgId = msgId;
        this.text = text;
    }

    public SM_LEGION_UPDATE_MEMBER(LegionMember legionMember)
        : this(legionMember, 0, null)
    {
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(legionMember.GetObjectId());
        WriteC(legionMember.GetRank().GetRankId());
        WriteC(legionMember.GetPlayerClass().GetClassId());
        WriteC(legionMember.GetLevel());
        WriteD(legionMember.GetWorldId());
        WriteC(legionMember.IsOnline() ? 1 : 0);
        WriteD(legionMember.IsOnline() ? 0 : legionMember.GetLastOnlineEpochSeconds());
        WriteD(NetworkConfig.GAMESERVER_ID); // TODO: add to account model?
        WriteD(msgId);
        WriteS(text);
    }
}
