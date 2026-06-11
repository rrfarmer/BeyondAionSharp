using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_CAPTCHA (Cura). Captcha challenge (type 1: image data) / result (type 3: correct flag + ban time).</summary>
public class SM_CAPTCHA : AionServerPacket
{
    private int type;
    private int count;
    private int size;
    private byte[] data;
    private bool isCorrect;
    private int banTime;

    public SM_CAPTCHA(int count, byte[] data)
    {
        this.type = 1;
        this.count = count;
        this.size = data.Length;
        this.data = data;
    }

    public SM_CAPTCHA(bool isCorrect, int banTime)
    {
        this.type = 3;
        this.isCorrect = isCorrect;
        this.banTime = banTime;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(type);

        switch (type)
        {
            case 0x01:
                WriteC(count);
                WriteD(size);
                WriteB(data);
                break;
            case 0x03:
                WriteH(isCorrect ? 1 : 0);

                // time setting can't be extracted (retail server default value:3000 sec)
                WriteD(banTime);
                break;
        }
    }
}
