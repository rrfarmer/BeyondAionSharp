using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_LEGION_LEAVE_MEMBER (Simple). Member leave/kick notice (objId + msgId + name(s)).</summary>
public class SM_LEGION_LEAVE_MEMBER : AionServerPacket
{
    private string name;
    private string name1;
    private int playerObjId;
    private int msgId;

    public SM_LEGION_LEAVE_MEMBER(int msgId, int playerObjId, string name)
    {
        this.msgId = msgId;
        this.playerObjId = playerObjId;
        this.name = name;
    }

    public SM_LEGION_LEAVE_MEMBER(int msgId, int playerObjId, string name, string name1)
    {
        this.msgId = msgId;
        this.playerObjId = playerObjId;
        this.name = name;
        this.name1 = name1;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(playerObjId);
        WriteC(0x00); // isMember ? 1 : 0
        WriteD(0x00); // unix time for log off
        WriteD(msgId);
        WriteS(name);
        WriteS(name1);
    }
}
