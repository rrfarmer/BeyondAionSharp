using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_QUESTIONNAIRE (lhw, Kaipo). Sends survey HTML data (chunked); html.length()*2 byte count. </summary>
public class SM_QUESTIONNAIRE : AionServerPacket
{
    private int messageId;
    private byte chunk;
    private byte count;
    private string html;

    public SM_QUESTIONNAIRE(int messageId, byte chunk, byte count, string html)
    {
        this.messageId = messageId;
        this.chunk = chunk;
        this.count = count;
        this.html = html;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(messageId);
        WriteC(chunk);
        WriteC(count);
        WriteH(html.Length * 2);
        WriteS(html);
    }
}
