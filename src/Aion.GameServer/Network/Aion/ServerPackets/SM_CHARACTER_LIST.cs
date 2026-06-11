using Aion.GameServer.Model.Account;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_CHARACTER_LIST (Nemesiss, AEJTester, Neon). Server sends the account's character list to the client. Extends AbstractPlayerInfoPacket; writes playOk2, char count, then WritePlayerInfo per character.</summary>
public class SM_CHARACTER_LIST : AbstractPlayerInfoPacket
{
    /// <summary>
    /// PlayOk2 - we dont care...
    /// </summary>
    private readonly int playOk2;

    /// <summary>
    /// Constructs new <c>SM_CHARACTER_LIST</c> packet
    /// </summary>
    public SM_CHARACTER_LIST(int playOk2)
    {
        this.playOk2 = playOk2;
    }

    protected override void WriteImpl(AionConnection con)
    {
        Account account = con.GetAccount();

        WriteD(playOk2);
        WriteC(account.Size()); // character count
        foreach (PlayerAccountData playerData in account.GetPlayerAccDataList())
        {
            WritePlayerInfo(playerData, con);
        }
    }
}
