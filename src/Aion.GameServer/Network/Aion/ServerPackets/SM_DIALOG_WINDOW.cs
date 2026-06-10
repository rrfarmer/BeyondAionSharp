using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_DIALOG_WINDOW (alexa026). Opens an NPC dialog page (with optional quest id); appends mailbox state for the mail page or town id for the town-challenge page. DialogPage.id()->GetId(); mailBoxState public field. DialogPage/TownService/AionServerPacket red-tolerated.</summary>
public class SM_DIALOG_WINDOW : AionServerPacket
{
    private readonly int targetObjectId;
    private readonly int dialogPageId;
    private readonly int questId;

    public SM_DIALOG_WINDOW(int targetObjectId, int dialogPageId)
        : this(targetObjectId, dialogPageId, 0)
    {
    }

    public SM_DIALOG_WINDOW(int targetObjectId, int dialogPageId, int questId)
    {
        this.targetObjectId = targetObjectId;
        this.dialogPageId = dialogPageId;
        this.questId = questId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        Player player = con.GetActivePlayer();
        WriteD(targetObjectId);
        WriteH(dialogPageId);
        WriteD(questId);
        WriteH(0);
        if (dialogPageId == DialogPage.MAIL.GetId())
        {
            WriteH(player.GetMailbox().mailBoxState);
        }
        else if (dialogPageId == DialogPage.TOWN_CHALLENGE_TASK.GetId())
        {
            WriteH(TownService.GetInstance().GetTownIdByPosition(player));
        }
        else
            WriteH(0);
    }
}
