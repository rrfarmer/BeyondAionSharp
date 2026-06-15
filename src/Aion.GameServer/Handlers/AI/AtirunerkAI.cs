using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/events/iceFestival/AtirunerkAI.</summary>
[AIName("atirunerk")]
public class AtirunerkAI : GeneralNpcAI
{
    public AtirunerkAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        base.HandleDialogStart(player);
        QuestEngine.QuestEngine.GetInstance().OnDialog(new QuestEnv(GetOwner(), player, 80719, DialogAction.QUEST_SELECT));
    }
}
