using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_QUEST_ACTION (VladimirZ). Quest add/update/abandon/timer/share/unk notice. Nested ActionType enum ids 1..6 == enum value -> getId()=(int)actionType; switch labels qualified. QuestState/QuestTemplate/QuestExtraCategory red-tolerated.</summary>
public class SM_QUEST_ACTION : AionServerPacket
{
    private ActionType actionType;
    private int questId;
    private int status;
    private int step;
    private int flags;
    private int timer;
    private int sharerId;
    private bool shareInAlliance;

    public SM_QUEST_ACTION(ActionType actionType, QuestState qs)
    {
        this.actionType = actionType;
        this.questId = qs.GetQuestId();
        this.status = qs.GetStatus().Value();
        this.step = qs.GetQuestVars().GetQuestVars();
        this.flags = qs.GetFlags();
    }

    public SM_QUEST_ACTION(int questId)
    {
        this.actionType = ActionType.UNK;
        this.questId = questId;
    }

    public SM_QUEST_ACTION(int questId, int timer)
    {
        this.actionType = ActionType.TIMER;
        this.questId = questId;
        this.timer = timer;
    }

    public SM_QUEST_ACTION(int questId, int sharerId, bool shareInAlliance)
    {
        this.actionType = ActionType.SHARE;
        this.questId = questId;
        this.sharerId = sharerId;
        this.shareInAlliance = shareInAlliance;
    }

    protected override void WriteImpl(AionConnection con)
    {
        QuestTemplate questTemplate = DataManager.QUEST_DATA.GetQuestById(questId);
        if (questTemplate != null && questTemplate.GetExtraCategory() != QuestExtraCategory.NONE)
            return;
        WriteC((int)actionType);
        WriteD(questId);
        switch (actionType)
        {
            case ActionType.ADD:
                WriteC(status);// quest status goes by ENUM value
                WriteC(0x0);
                WriteD(step | flags << 24);// current quest step
                WriteH(0);
                WriteC(0); // seen sometimes 1 for campaign quests
                break;
            case ActionType.UPDATE:
                WriteC(status);// quest status goes by ENUM value
                WriteC(0x0);
                WriteD(step | flags << 24);// current quest step
                WriteH(0); // seen sometimes 1 when status == COMPLETED
                break;
            case ActionType.ABANDON:
                WriteD(0);
                break;
            case ActionType.TIMER:
                WriteD(timer);// sets client timer ie 84030000 is 900 seconds/15 mins
                WriteC(timer > 0 ? 1 : 0);
                break;
            case ActionType.SHARE:
                WriteD(sharerId);
                WriteD(shareInAlliance ? 1 : 0); // 0: group, 1: alliance
                break;
            case ActionType.UNK:
                WriteH(0x01);// ???
                WriteH(0x0);
                break;
        }
    }

    public enum ActionType
    {
        ADD = 1,
        UPDATE = 2,
        ABANDON = 3,
        TIMER = 4,
        SHARE = 5,
        UNK = 6,
    }
}
