using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>Java parity: model/templates/quest/HandlerSideDrop (vlog, Rolandas).</summary>
public class HandlerSideDrop : QuestDrop
{
    private int neededAmount;

    public HandlerSideDrop(int questId, int npcId, int itemId, int amount, int chance)
    {
        this.questId = questId;
        this.npcId = npcId;
        this.itemId = itemId;
        this.chance = chance;

        foreach (QuestDrop drop in DataManager.QUEST_DATA.GetQuestById(questId).GetQuestDrop())
        {
            if (drop.npcId == npcId && drop.itemId == itemId)
            {
                this.dropEachMember = drop.dropEachMember;
                break;
            }
        }
        this.neededAmount = amount;
    }

    public HandlerSideDrop(int questId, int npcId, int itemId, int amount, int chance, int step)
        : this(questId, npcId, itemId, amount, chance)
    {
        this.collecting_step = step;
    }

    public int GetNeededAmount()
    {
        return neededAmount;
    }
}
