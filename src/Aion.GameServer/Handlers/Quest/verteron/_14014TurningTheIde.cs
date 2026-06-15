using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Items;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Artur, Ritsu, Majka
/// </summary>
public class _14014TurningTheIde : AbstractQuestHandler
{
    private readonly int[] mobs = { 210178, 216892 };

    public _14014TurningTheIde() : base(14014)
    {
    }

    public override void Register()
    {
        int[] questNpcs = { 203146, 203147, 802045, 203098 };
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterQuestItem(182215314, questId);
        foreach (int questNpc in questNpcs)
            qe.RegisterQuestNpc(questNpc).AddOnTalkEvent(questId);
        foreach (int mob in mobs)
            qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npcObj)
            targetId = npcObj.GetNpcId();

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 203146:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                                return SendQuestDialog(env, 1011);
                            return false;
                        case DialogAction.SETPRO1:
                            ItemService.AddItem(player, 182215314, 1);
                            return DefaultCloseDialog(env, 0, 1); // 1
                    }
                    break;
                case 203147:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 2)
                                return SendQuestDialog(env, 1693);
                            return false;
                        case DialogAction.SETPRO3:
                            return DefaultCloseDialog(env, 2, 3); // 3
                    }
                    break;
                case 802045:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 3)
                                return SendQuestDialog(env, 2034);
                            return false;
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            if (var == 3)
                                return CheckQuestItems(env, 3, 5, false, 2375, 2120);
                            break;
                    }
                    break;
                case 203098:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 8)
                                return SendQuestDialog(env, 3057);
                            return false;
                        case DialogAction.SELECT_QUEST_REWARD:
                            return DefaultCloseDialog(env, 8, 8, true, false);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203098)
                return SendQuestEndDialog(env);
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        return DefaultOnKillEvent(env, mobs, 5, 7) || DefaultOnKillEvent(env, mobs, 7, true);
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            if (qs.GetQuestVarById(0) == 1 && player.IsInsideZone(ZoneName.Get("TURSIN_OUTPOST_210030000")))
            {
                return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 1, 2, false, 18));
            }
        }
        return HandlerResult.FAILED;
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 14010);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 14010);
    }
}
