using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Online (VladimirZ, Neon).</summary>
public class Online : AdminCommand
{
    public Online()
        : base("online", "Shows the number of online players.")
    {
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        int elyosCount = 0;
        int asmoCount = 0;

        foreach (Player player in Aion.GameServer.World.World.GetInstance().GetAllPlayers())
        {
            if (player.GetRace() == Race.ELYOS)
                elyosCount++;
            else
                asmoCount++;
        }

        string countInfo = (elyosCount + asmoCount) + " (" + elyosCount + " Elyos / " + asmoCount + " Asmo" + (asmoCount == 1 ? ")" : "s)");
        PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_LIST_USER(countInfo));
    }
}
