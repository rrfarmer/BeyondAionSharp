using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.GameObjects.Player.Title;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_TITLE_INFO (cura, xTz, -Enomine-). Title packet by action: 0=list, 1=self set, 3=broadcast set, 4/5=mentor flag self/broadcast, 6=bonus-stat title. Converges PlayerEnterWorldService SM_TITLE_INFO(int/Player) ctors. switch-on-action; getTitles/secondsUntilExpiration->PascalCase. TitleList/Title/AionServerPacket red-tolerated.</summary>
public class SM_TITLE_INFO : AionServerPacket
{
    private TitleList titleList;
    private int action; // 0: list, 1: self set, 3: broad set
    private int titleId;
    private int bonusTitleId;
    private int playerObjId;

    public SM_TITLE_INFO(Player player)
    {
        this.action = 0;
        this.titleList = player.GetTitleList();
    }

    public SM_TITLE_INFO(int titleId)
    {
        this.action = 1;
        this.titleId = titleId;
    }

    public SM_TITLE_INFO(Player player, int titleId)
    {
        this.action = 3;
        this.playerObjId = player.GetObjectId();
        this.titleId = titleId;
    }

    public SM_TITLE_INFO(bool flag)
    {
        this.action = 4;
        this.titleId = flag ? 1 : 0;
    }

    public SM_TITLE_INFO(Player player, bool flag)
    {
        this.action = 5;
        this.playerObjId = player.GetObjectId();
        this.titleId = flag ? 1 : 0;
    }

    public SM_TITLE_INFO(int action, int bonusTitleId)
    {
        this.action = action;
        this.bonusTitleId = bonusTitleId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(action);
        switch (action)
        {
            case 0:
                WriteC(0x00);
                WriteH(titleList.Size());
                foreach (Title title in titleList.GetTitles())
                {
                    WriteD(title.GetId());
                    WriteD(title.SecondsUntilExpiration());
                }
                break;
            case 1: // self set
                WriteH(titleId);
                break;
            case 3: // broad set
                WriteD(playerObjId);
                WriteH(titleId);
                break;
            case 4: // Mentor flag self
                WriteH(titleId);
                break;
            case 5: // broad set mentor fleg
                WriteD(playerObjId);
                WriteH(titleId);
                break;
            case 6:// Title wich will take BonusStats from
                WriteH(bonusTitleId);
                break;
        }
    }
}
