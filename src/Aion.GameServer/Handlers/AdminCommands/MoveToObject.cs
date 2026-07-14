using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/MoveToObject (Rolandas).</summary>
public class MoveToObject : AdminCommand
{
    public MoveToObject()
        : base("movetoobj")
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr == null || paramsArr.Length != 1)
        {
            PacketSendUtility.SendMessage(admin, "Syntax : //movetoobj <object id>");
            return;
        }

        int objectId = 0;

        if (!TryParseInt(paramsArr[0], out objectId))
        {
            PacketSendUtility.SendMessage(admin, "Only numbers please!!!");
        }

        VisibleObject @object = Aion.GameServer.World.World.GetInstance().FindVisibleObject(objectId);
        if (@object == null)
        {
            PacketSendUtility.SendMessage(admin, "Cannot find object for spawn #" + objectId);
            return;
        }

        VisibleObject spawn = @object;

        TeleportService.TeleportTo(admin, spawn.GetWorldId(), spawn.GetSpawn().GetX(), spawn.GetSpawn().GetY(), spawn.GetSpawn().GetZ());
    }

    private void Info(Player player, string message)
    {
        PacketSendUtility.SendMessage(player, "Syntax : //movetoobj <object id>");
    }
}
