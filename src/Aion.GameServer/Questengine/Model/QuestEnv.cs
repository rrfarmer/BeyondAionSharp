using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Questengine.Model;

/// <summary>Java parity: questEngine/model/QuestEnv (MrPoke).</summary>
public class QuestEnv
{
    private VisibleObject visibleObject;
    private Aion.GameServer.Model.GameObjects.Player.Player player;
    private int questId;
    private int dialogActionId;
    private bool isDialogContinuationFromPreQuest;
    private int extendedRewardIndex;

    public QuestEnv(VisibleObject visibleObject, Aion.GameServer.Model.GameObjects.Player.Player player, int questId)
        : this(visibleObject, player, questId, DialogAction.NULL)
    {
    }

    public QuestEnv(VisibleObject visibleObject, Aion.GameServer.Model.GameObjects.Player.Player player, int questId, int dialogActionId)
    {
        this.visibleObject = visibleObject;
        this.player = player;
        this.questId = questId;
        this.dialogActionId = dialogActionId;
    }

    public VisibleObject GetVisibleObject()
    {
        return visibleObject;
    }

    public void SetVisibleObject(VisibleObject visibleObject)
    {
        this.visibleObject = visibleObject;
    }

    public Aion.GameServer.Model.GameObjects.Player.Player GetPlayer()
    {
        return player;
    }

    public void SetPlayer(Aion.GameServer.Model.GameObjects.Player.Player player)
    {
        this.player = player;
    }

    public int GetQuestId()
    {
        return questId;
    }

    public void SetQuestId(int questId)
    {
        this.questId = questId;
    }

    public int GetDialogActionId()
    {
        return dialogActionId;
    }

    public void SetDialogActionId(int dialogActionId)
    {
        this.dialogActionId = dialogActionId;
    }

    public bool IsDialogContinuationFromPreQuest()
    {
        return isDialogContinuationFromPreQuest;
    }

    public void SetDialogContinuationFromPreQuest(bool isDialogContinuationFromPreQuest)
    {
        this.isDialogContinuationFromPreQuest = isDialogContinuationFromPreQuest;
    }

    /// <returns>the target template id, 0 if no target (GetVisibleObject()) is set.</returns>
    public int GetTargetId()
    {
        return visibleObject == null ? 0 : visibleObject.GetObjectTemplate().GetTemplateId();
    }

    public void SetExtendedRewardIndex(int index)
    {
        this.extendedRewardIndex = index;
    }

    public int GetExtendedRewardIndex()
    {
        return this.extendedRewardIndex;
    }
}
