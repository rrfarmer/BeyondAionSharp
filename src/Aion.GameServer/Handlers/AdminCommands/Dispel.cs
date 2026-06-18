using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Dispel (Hilgert, Estrayl).</summary>
public class Dispel : AdminCommand
{
    public Dispel()
        : base("dispel", "Removes all effects including transformations.")
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        VisibleObject target = admin.GetTarget();
        if (target == null)
            target = admin;

        if (target is Creature creature)
        {
            creature.GetEffectController().RemoveAllEffects();
            creature.GetEffectController().RemoveTransformEffects();
            SendInfo(admin, "Removed all effects of " + target + ".");
        }
    }
}
