using System;
using System.Collections.Generic;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Collide (Rolandas).</summary>
public class Collide : AdminCommand
{
    public Collide()
        : base("collide", "Geo debugging tool.")
    {
        SetSyntaxInfo(
            " - Lists collisions between your target and the ground.",
            "me - Lists collisions between you and your target."
        );
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        bool isMe = false;
        if (paramsArr.Length > 0 && !(isMe = "me".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase)))
        {
            SendInfo(admin);
            return;
        }
        VisibleObject target = admin.GetTarget();
        if (target == null)
        {
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
            return;
        }

        sbyte intentions = CollisionIntention.PHYSICAL.GetId();
        float x = target.GetX();
        float y = target.GetY();
        float z = target.GetZ();
        float targetX, targetY, targetZ;

        if (!isMe)
        {
            targetX = x;
            targetY = y;
            targetZ = z - 10;
        }
        else
        {
            targetX = admin.GetX();
            targetY = admin.GetY();
            targetZ = admin.GetZ() + admin.GetObjectTemplate().GetBoundRadius().GetUpper() / 6;
            SendInfo(admin, "From target towards you:");
        }

        SendInfo(admin, "Target: X=" + x + "; Y=" + y + "; Z=" + z);

        CollisionResults results = GeoService.GetInstance().GetCollisions(target, targetX, targetY, targetZ, intentions, null);
        CollisionResult closest = results.GetClosestCollision();

        if (results.Size() == 0)
        {
            SendInfo(admin, "No collisions found.");
            closest = null;
        }
        else
        {
            ListCollisions(admin, results, closest);
        }

        CollisionResult closestOpposite = null;

        if (isMe)
        {
            SendInfo(admin, "From you towards your target:");
            SendInfo(admin, "Admin: X=" + admin.GetX() + "; Y=" + admin.GetY() + "; Z=" + admin.GetZ());

            results = GeoService.GetInstance().GetCollisions(admin, target.GetX(), target.GetY(),
                target.GetZ() + target.GetObjectTemplate().GetBoundRadius().GetUpper() / 2, intentions, null);
            closestOpposite = results.GetClosestCollision();

            if (results.Size() == 0)
            {
                SendInfo(admin, "No collisions found.");
                closestOpposite = null;
            }
            else
            {
                ListCollisions(admin, results, closestOpposite);
            }
        }

        if (!isMe && closest != null && closest.GetContactPoint().Z + 0.5f < target.GetZ())
        {
            SendInfo(admin, "Closest collision is below target's Z coordinate!");
        }
        else
        {
            if (closest != null)
            {
                SpawnTemplate spawn = SpawnEngine.SpawnEngine.NewSpawn(admin.GetWorldId(), 200000, closest.GetContactPoint().X, closest.GetContactPoint().Y,
                    closest.GetContactPoint().Z, (byte) 0, 0);
                SpawnEngine.SpawnEngine.SpawnObject(spawn, admin.GetInstanceId());
            }
            if (closestOpposite != null)
            {
                SpawnTemplate spawn = SpawnEngine.SpawnEngine.NewSpawn(admin.GetWorldId(), 200000, closestOpposite.GetContactPoint().X,
                    closestOpposite.GetContactPoint().Y, closestOpposite.GetContactPoint().Z, (byte) 0, 0);
                SpawnEngine.SpawnEngine.SpawnObject(spawn, admin.GetInstanceId());
            }
        }
    }

    private void ListCollisions(Player admin, CollisionResults results, CollisionResult closestOpposite)
    {
        int count = 1;
        int closestId = 0;
        string description = "";

        using (IEnumerator<CollisionResult> iter = results.GetEnumerator())
        {
            while (iter.MoveNext())
            {
                CollisionResult result = iter.Current;
                if (result.Equals(closestOpposite))
                    closestId = count;
                if (result.GetGeometry() == null)
                    description += count + ". " + result.GetContactPoint().ToString() + "\n";
                else
                {
                    if (result.GetGeometry().GetName() == null)
                    {
                        description += count + ". " + result.GetContactPoint().ToString() + "; parent=" + result.GetGeometry().GetParent().GetName() + "\n";
                    }
                    else
                        description += count + ". " + result.GetContactPoint().ToString() + "; name=" + result.GetGeometry().GetName() + "\n";
                }
                count++;
            }
        }
        description += "-----------------------\nClosest: " + closestId + ". Distance: " + closestOpposite.GetDistance();
        SendInfo(admin, description);
    }
}
