using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _80295DurableDaevanionWeapon : AbstractQuestHandler
{
    public _80295DurableDaevanionWeapon() : base(80295)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(831387).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(831387).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();

        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 831387)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    int plate = player.GetEquipment().ItemSetPartsEquipped(303);
                    int chain = player.GetEquipment().ItemSetPartsEquipped(302);
                    int leather = player.GetEquipment().ItemSetPartsEquipped(301);
                    int cloth = player.GetEquipment().ItemSetPartsEquipped(300);
                    int gunner = player.GetEquipment().ItemSetPartsEquipped(372);

                    if (plate != 5 && chain != 5 && leather != 5 && cloth != 5 && gunner != 5)
                        return SendQuestDialog(env, 1003);
                    else
                        return SendQuestDialog(env, 4762);
                }
                else
                    return SendQuestStartDialog(env);
            }
        }

        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);

        if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 831387)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1011);
                        return false;
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        if (var == 0)
                        {
                            return CheckQuestItems(env, 0, 1, true, 5, 0);
                        }
                        break;
                    case DialogAction.SELECT2:
                        if (var == 0)
                            return SendQuestDialog(env, 1352);
                        break;
                }
            }
            return false;
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 831387)
            {
                return SendQuestEndDialog(env);
            }
            return false;
        }
        return false;
    }
}
