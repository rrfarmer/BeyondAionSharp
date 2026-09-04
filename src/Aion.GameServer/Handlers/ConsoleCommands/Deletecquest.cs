using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Deletecquest (ginho1, Neon). Deletes a quest from the target's quest list.</summary>
public class Deletecquest : ConsoleCommand
{
    public Deletecquest()
        : base("deletecquest", "Deletes a quest from the players quest list.")
    {
        SetSyntaxInfo("<quest link|ID> - Deletes the quest from your target's quest list (defaults to your character, if no player is targeted).");
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        Player player = admin.GetTarget() is Player target ? target : admin;
        Aion.GameServer.Handlers.AdminCommands.Quest questCommand = ChatProcessor.GetInstance().GetCommand<Aion.GameServer.Handlers.AdminCommands.Quest>();
        questCommand.DeleteQuest(admin, player, ChatUtil.GetQuestId(paramsArr[0]));
    }
}
