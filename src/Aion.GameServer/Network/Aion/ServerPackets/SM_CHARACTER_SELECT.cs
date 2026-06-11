using Aion.GameServer.Configs.Main;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_CHARACTER_SELECT (cura). Passkey window/result packet for character selection. SecurityConfig red-tolerated.</summary>
public class SM_CHARACTER_SELECT : AionServerPacket
{
    private int type; // 0: new passkey input window, 1: passkey input window, 2: message window
    private short messageType; // 0: newpasskey complete, 2: passkey edit complete, 3: passkey input
    private int wrongCount;

    public SM_CHARACTER_SELECT(int type)
    {
        this.type = type;
    }

    public SM_CHARACTER_SELECT(int type, short messageType, int wrongCount)
    {
        this.type = type;
        this.messageType = messageType;
        this.wrongCount = wrongCount;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(type);

        switch (type)
        {
            case 0:
                break;
            case 1:
                break;
            case 2:
                WriteH(messageType); // 0: newpasskey complete, 2: passkey edit complete, 3: passkey input
                WriteC(wrongCount > 0 ? 1 : 0); // 0: right passkey, 1: wrong passkey
                WriteD(wrongCount); // wrong passkey input count
                WriteD(SecurityConfig.PASSKEY_WRONG_MAXCOUNT); // Enter the number of possible wrong numbers (retail
                // server default value: 5)
                break;
        }
    }
}
