using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author MrPoke, Nephis
/// </summary>
public class _1845OpeningDoors : AbstractQuestHandler
{
    public _1845OpeningDoors() : base(1845)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(278591).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(278624).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798316).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204390).AddOnTalkEvent(questId);
        qe.RegisterQuestItem(182202181, questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = env.GetVisibleObject() is Npc npc ? npc.GetNpcId() : 0;
        if (targetId == 0)
        {
            if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1 && (qs == null || qs.IsStartable()))
            {
                QuestService.StartQuest(env);
                return CloseDialogWindow(env);
            }
        }
        else if (targetId == 278591) // Deidamia
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                    return DefaultCloseDialog(env, 0, 1);
            }
        }
        else if (targetId == 278624) // Lycian
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1693);
                else if (env.GetDialogActionId() == DialogAction.SETPRO2)
                    return DefaultCloseDialog(env, 1, 2);
            }
        }
        else if (targetId == 798316) // Anusis
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                {
                    RemoveQuestItem(env, 182202181, 1);
                    return DefaultCloseDialog(env, 2, 2, true, true);
                }
            }
            return SendQuestEndDialog(env);
        }
        return false;
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        int id = item.GetItemTemplate().GetTemplateId();

        if (id != 182202181)
            return HandlerResult.UNKNOWN;
        SendQuestDialog(env, 4);
        return HandlerResult.SUCCESS;
    }
}
