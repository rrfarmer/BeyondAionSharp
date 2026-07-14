using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Delete (Luno, Bobobear, Neon). Removes a spawn from world.</summary>
public class Delete : AdminCommand
{
    public Delete()
        : base("delete", "Removes a spawn from world.")
    {
        SetSyntaxInfo(
            " - Deletes the object you are targeting.",
            "<range> - Deletes all objects around you in given radius in meters.");
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            if (admin.GetTarget() == null)
                SendInfo(admin);
            else
                DeleteObject(admin, admin.GetTarget(), true);
        }
        else
        {
            int[] count = { 0 };
            float range = ParseFloat(paramsArr[0]);
            admin.GetKnownList().ForEachObject(obj =>
            {
                if (PositionUtil.IsInRange(admin, obj, range) && DeleteObject(admin, obj, false))
                    count[0]++;
            });
            SendInfo(admin, "Deleted " + count[0] + (count[0] == 1 ? " object." : " objects."));
        }
    }

    private bool DeleteObject(Player admin, VisibleObject target, bool notifyOnFail)
    {
        if (!(target is Npc) && !(target is Gatherable) && !(target is HouseObject))
        {
            if (notifyOnFail)
                PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
            return false;
        }

        SpawnTemplate spawn = target.GetSpawn();
        if (spawn != null) // house objects have no spawn template
        {
            if (spawn.HasPool())
            {
                if (notifyOnFail)
                    SendInfo(admin, "Can't delete pooled spawn template.");
                return false;
            }

            if (!spawn.GetType().Equals(typeof(SpawnTemplate)))
            {
                if (notifyOnFail)
                    SendInfo(admin, "Can't delete special spawns (spawn type: " + spawn.GetType().Name.Replace("Template", "") + ").");
                return false;
            }
        }

        target.GetController().Delete();
        if (DataManager.SPAWNS_DATA.SaveSpawn(target, true))
            SendInfo(admin, "Spawn removed permanently. " + target.GetType().Name + " will not spawn on server start anymore.");
        return true;
    }
}
