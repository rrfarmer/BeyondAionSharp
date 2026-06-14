using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Morph (ATracer, aionchs-, Wylovech, Neon). Morphs a player into any npc.</summary>
public class Morph : AdminCommand
{
    public Morph()
        : base("morph", "Morphs a player into any npc.")
    {
        SetSyntaxInfo(
            " - morphs you into the npc you are targeting.",
            "<id> - Morphs your target into the specified npc (0 to cancel)."
        );
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0 && !(admin.GetTarget() is Npc))
        {
            SendInfo(admin);
            return;
        }

        Player target = admin.GetTarget() is Player p ? p : admin;
        int npcId;

        if (paramsArr.Length == 0 && admin.GetTarget() is Npc npc)
        {
            npcId = npc.GetNpcId();
        }
        else
        {
            if (!int.TryParse(paramsArr[0], out npcId))
            {
                SendInfo(admin);
                return;
            }
        }

        if (npcId < 0 || npcId > 0 && npcId < 200000)
        {
            SendInfo(admin, "Invalid ID.");
            return;
        }

        target.GetTransformModel().Apply(npcId);

        if (npcId == 0)
        {
            SendInfo(admin, "Cancelled" + (target.Equals(admin) ? "" : " " + target.GetName() + "'s") + " morph.");
        }
        else
        {
            SendInfo(admin, "You morphed" + (target.Equals(admin) ? "" : " " + target.GetName()) + " into " + ChatUtil.Path(npcId, true) + ".");
            if (!target.Equals(admin))
                SendInfo(target, ChatUtil.Name(admin) + " morphed you into an npc form.");
        }
    }
}
