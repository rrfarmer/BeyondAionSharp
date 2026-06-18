using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Walker;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/SpawnUpdate (KID, Rolandas, Neon). Updates spawn data.</summary>
public class SpawnUpdate : AdminCommand
{
    public SpawnUpdate()
        : base("spawnu", "Updates spawn data.")
    {
        SetSyntaxInfo(
            "<x|y|z|h> [value] - Update X, Y, or Z coordinate or the heading of the selected npc/gatherable (default: takes your current position, optional: the specified value).",
            "<xyz|xyzh> - Update position or position and heading of the selected npc/gatherable to your own one.",
            "<w> [walker_id] - Set walker data of the selected npc (default: remove walker data, optional: set walker id to npc).");
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        VisibleObject target = admin.GetTarget();
        if (target is Npc && paramsArr[0].Equals("w", System.StringComparison.OrdinalIgnoreCase))
        {
            UpdateWalker(admin, (Npc)target, paramsArr.Length == 2 ? paramsArr[1].ToUpper() : null);
            return;
        }

        if (!(target is Npc) && !(target is Gatherable))
        {
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
            return;
        }

        float? x = null, y = null, z = null;
        byte? h = null;
        if (paramsArr[0].Equals("xyz", System.StringComparison.OrdinalIgnoreCase))
        {
            x = admin.GetX();
            y = admin.GetY();
            z = admin.GetZ();
        }
        else if (paramsArr[0].Equals("xyzh", System.StringComparison.OrdinalIgnoreCase))
        {
            x = admin.GetX();
            y = admin.GetY();
            z = admin.GetZ();
            h = admin.GetHeading();
        }
        else if (paramsArr[0].Equals("x", System.StringComparison.OrdinalIgnoreCase))
        {
            if (paramsArr.Length == 1)
                x = admin.GetX();
            else
                x = float.Parse(paramsArr[1]);
        }
        else if (paramsArr[0].Equals("y", System.StringComparison.OrdinalIgnoreCase))
        {
            if (paramsArr.Length == 1)
                y = admin.GetY();
            else
                y = float.Parse(paramsArr[1]);
        }
        else if (paramsArr[0].Equals("z", System.StringComparison.OrdinalIgnoreCase))
        {
            if (paramsArr.Length == 1)
                z = admin.GetZ();
            else
                z = float.Parse(paramsArr[1]);
        }
        else if (paramsArr[0].Equals("h", System.StringComparison.OrdinalIgnoreCase))
        {
            if (paramsArr.Length == 1)
                h = admin.GetHeading();
            else
                h = byte.Parse(paramsArr[1]);
        }
        else
        {
            SendInfo(admin);
            return;
        }

        WorldPosition tPos = target.GetPosition();
        tPos.SetXYZH(x, y, z, h);

        if (target is Npc npc)
            PacketSendUtility.SendPacket(admin, new SM_NPC_INFO(npc, admin));
        else
            PacketSendUtility.SendPacket(admin, new SM_GATHERABLE_INFO(target));
        SendInfo(admin,
            "Updated " + target.GetType().Name + "'s coordinates to\nX:" + tPos.GetX() + " Y:" + tPos.GetY() + " Z:" + tPos.GetZ() + " H:"
                + tPos.GetHeading() + ".");

        if (!DataManager.SPAWNS_DATA.SaveSpawn(target, false))
            SendInfo(admin, "Could not save spawn. Maybe it's a special or temporary spawn (siege, base, invasion, ...) which cannot be altered.");
    }

    private void UpdateWalker(Player admin, Npc target, string walkerId)
    {
        SpawnTemplate spawn = target.GetSpawn();
        string oldId = spawn.GetWalkerId();
        if (oldId == null && walkerId == null)
        {
            SendInfo(admin, "Npc has no walker_id that could be removed.");
        }
        else
        {
            if (walkerId != null)
            {
                WalkerTemplate template = DataManager.WALKER_DATA.GetWalkerTemplate(walkerId);
                if (template == null)
                {
                    SendInfo(admin, "No such template exists in npc_walker.xml.");
                    return;
                }
                List<SpawnGroup> allSpawns = DataManager.SPAWNS_DATA.GetSpawnsByWorldId(target.GetWorldId());
                List<SpawnTemplate> allSpots = allSpawns.SelectMany(s => s.GetSpawnTemplates()).ToList();
                List<SpawnTemplate> sameIds = allSpots.Where(s => s.GetWalkerId().Equals(walkerId)).ToList();
                if (sameIds.Count >= template.GetPool())
                {
                    SendInfo(admin, "Can not assign, walker pool reached the limit.");
                    return;
                }
            }
            spawn.SetWalkerId(walkerId);
            PacketSendUtility.SendPacket(admin, new SM_DELETE(target));
            PacketSendUtility.SendPacket(admin, new SM_NPC_INFO(target, admin));
            if (walkerId == null)
                SendInfo(admin, "Removed npcs walker_id " + oldId + " for " + target.GetNpcId() + ".");
            else
                SendInfo(admin, "Updated npcs walker_id from " + oldId + " to " + walkerId + ".");
            if (!DataManager.SPAWNS_DATA.SaveSpawn(target, false))
                SendInfo(admin, "Could not save spawn.");
        }
    }
}
