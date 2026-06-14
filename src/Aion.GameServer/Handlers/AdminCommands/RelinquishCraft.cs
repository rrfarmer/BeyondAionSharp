using Aion.GameServer.Model.Craft;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Craft;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/RelinquishCraft (synchro2, Neon).</summary>
public class RelinquishCraft : AdminCommand
{
    public RelinquishCraft()
        : base("relinquishcraft", "Removes a players crafting expert or master status.")
    {
        SetSyntaxInfo(
            "<skillId> <expert|master> - Removes master or expert status of your target for the given crafting skill.",
            "<name> <skillId> <expert|master> - Removes the players master or expert status for the given crafting skill.");
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 2)
        {
            SendInfo(admin);
            return;
        }

        int i = 0;
        Player target;
        if (paramsArr.Length == 3)
        {
            string playerName = Util.ConvertName(paramsArr[i++]);
            target = Aion.GameServer.World.World.GetInstance().GetPlayer(playerName);
            if (target == null)
            {
                PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_NO_SUCH_USER(playerName));
                return;
            }
        }
        else
        {
            if (admin.GetTarget() is Player p)
                target = p;
            else
            {
                PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
                return;
            }
        }

        Profession? profession = ProfessionExtensions.GetBySkillId(int.TryParse(paramsArr[i++], out int skillId) ? skillId : 0);
        if (profession == null || !profession.Value.IsCrafting())
        {
            SendInfo(admin, "Invalid skill ID.");
            return;
        }

        if ("expert".Equals(paramsArr[i], System.StringComparison.OrdinalIgnoreCase))
        {
            if (RelinquishCraftStatus.RelinquishExpertStatus(target, profession.Value, 0))
                SendInfo(admin, "Successfully removed expert status for " + profession.Value);
            else
                SendInfo(admin, target.GetName() + " doesn't have " + profession.Value + " on expert.");
        }
        else if ("master".Equals(paramsArr[i], System.StringComparison.OrdinalIgnoreCase))
        {
            if (RelinquishCraftStatus.RelinquishMasterStatus(target, profession.Value, 0))
                SendInfo(admin, "Successfully removed master status for " + profession.Value);
            else
                SendInfo(admin, target.GetName() + " doesn't have " + profession.Value + " on master.");
        }
        else
            SendInfo(admin);
    }
}
