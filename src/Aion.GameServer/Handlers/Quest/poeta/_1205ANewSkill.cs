using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author MrPoke
/// </summary>
public class _1205ANewSkill : AbstractQuestHandler
{
    public _1205ANewSkill() : base(1205)
    {
    }

    public override void Register()
    {
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterQuestNpc(203087).AddOnTalkEvent(questId); // Warrior
        qe.RegisterQuestNpc(203088).AddOnTalkEvent(questId); // Scout
        qe.RegisterQuestNpc(203089).AddOnTalkEvent(questId); // Mage
        qe.RegisterQuestNpc(203090).AddOnTalkEvent(questId); // Priest
        qe.RegisterQuestNpc(801210).AddOnTalkEvent(questId); // Engineer
        qe.RegisterQuestNpc(801211).AddOnTalkEvent(questId); // Artist
    }

    public override void OnLevelChangedEvent(Player player)
    {
        if (player.GetQuestStateList().HasQuest(questId))
            return;
        QuestEnv env = new QuestEnv(null, player, questId);
        if (QuestService.StartQuest(env))
        {
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            qs.SetStatus(QuestStatus.REWARD);
            switch (player.GetPlayerClass().GetStartingClass())
            {
                case PlayerClass.WARRIOR:
                    qs.SetQuestVar(1);
                    qs.SetRewardGroup(0);
                    break;
                case PlayerClass.SCOUT:
                    qs.SetQuestVar(2);
                    qs.SetRewardGroup(1);
                    break;
                case PlayerClass.MAGE:
                    qs.SetQuestVar(3);
                    qs.SetRewardGroup(2);
                    break;
                case PlayerClass.PRIEST:
                    qs.SetQuestVar(4);
                    qs.SetRewardGroup(3);
                    break;
                case PlayerClass.ENGINEER:
                    qs.SetQuestVar(5);
                    qs.SetRewardGroup(4);
                    break;
                case PlayerClass.ARTIST:
                    qs.SetQuestVar(6);
                    qs.SetRewardGroup(5);
                    break;
            }
            UpdateQuestStatus(env);
        }
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.REWARD)
            return false;

        int targetId = env.GetTargetId();
        PlayerClass playerClass = player.GetPlayerClass().GetStartingClass();
        switch (targetId)
        {
            case 203087:
                if (playerClass == PlayerClass.WARRIOR)
                {
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 1011);
                    else
                        return SendQuestEndDialog(env);
                }
                return false;
            case 203088:
                if (playerClass == PlayerClass.SCOUT)
                {
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 1352);
                    else
                        return SendQuestEndDialog(env);
                }
                return false;
            case 203089:
                if (playerClass == PlayerClass.MAGE)
                {
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 1693);
                    else
                        return SendQuestEndDialog(env);
                }
                return false;
            case 203090:
                if (playerClass == PlayerClass.PRIEST)
                {
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 2034);
                    else
                        return SendQuestEndDialog(env);
                }
                return false;
            case 801210:
                if (playerClass == PlayerClass.ENGINEER)
                {
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 2375);
                    else
                        return SendQuestEndDialog(env);
                }
                return false;
            case 801211:
                if (playerClass == PlayerClass.ARTIST)
                {
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 2716);
                    else
                        return SendQuestEndDialog(env);
                }
                return false;
        }
        return false;
    }
}
