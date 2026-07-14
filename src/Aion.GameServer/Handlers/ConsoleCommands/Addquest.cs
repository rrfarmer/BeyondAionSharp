using System.Collections.Generic;
using System.Text.RegularExpressions;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Addquest (ginho1). Starts a quest for the target player.</summary>
public class Addquest : ConsoleCommand
{
    public Addquest()
        : base("addquest")
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 1)
        {
            Info(admin, null);
            return;
        }

        VisibleObject target = admin.GetTarget();
        if (target == null)
        {
            PacketSendUtility.SendMessage(admin, "No target selected.");
            return;
        }

        if (target is not Player player)
        {
            PacketSendUtility.SendMessage(admin, "This command can only be used on a player!");
            return;
        }

        // Java parity: regex "[quest:(...)]" link, else parseInt(params[0]); NumberFormatException -> info(admin, null).
        int id;
        string quest = paramsArr[0];
        Match result = Regex.Match(quest, "\\[quest:([^%]+)]");
        if (result.Success)
        {
            if (!TryParseInt(result.Groups[1].Value, out id))
            {
                Info(admin, null);
                return;
            }
        }
        else if (!TryParseInt(paramsArr[0], out id))
        {
            Info(admin, null);
            return;
        }

        QuestEnv env = new QuestEnv(null, player, id);

        if (QuestService.StartQuest(env))
        {
            PacketSendUtility.SendMessage(admin, "Quest started.");
        }
        else
        {
            QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(id);
            List<XMLStartCondition> preconditions = template.GetXMLStartConditions();
            if (preconditions != null && preconditions.Count > 0)
            {
                foreach (XMLStartCondition condition in preconditions)
                {
                    List<FinishedQuestCond> finisheds = condition.GetFinishedPreconditions();
                    if (finisheds != null && finisheds.Count > 0)
                    {
                        foreach (FinishedQuestCond fcondition in finisheds)
                        {
                            QuestState qs1 = admin.GetQuestStateList().GetQuestState(fcondition.GetQuestId());
                            if (qs1 == null || qs1.GetStatus() != QuestStatus.COMPLETE)
                            {
                                PacketSendUtility.SendMessage(admin, "You have to finish " + fcondition.GetQuestId() + " first!");
                            }
                        }
                    }
                }
            }
            PacketSendUtility.SendMessage(admin, "Quest not started. Some preconditions failed");
        }
    }

    // Java parity: Addquest.info(Player, String) — ChatCommand has no info() in the C# port, so this is a private helper.
    private void Info(Player player, string message)
    {
        PacketSendUtility.SendMessage(player, "syntax ///addquest <id quest>");
    }
}
