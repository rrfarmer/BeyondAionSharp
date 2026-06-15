using System;
using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Cheatkiller
/// </summary>
[AIName("xdrakanpriest")]
public class DrakanPriestAI : AggressiveNpcAI
{
    public DrakanPriestAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (Rnd.Chance() < 3)
        {
            SpawnServants(282988, Rnd.Get(1, 3));
        }
    }

    internal void SpawnServants(int npcId, int count)
    {
        List<Servant> servants = FindServants();
        if (servants.Count == 0)
        {
            RndSpawn(npcId, count);
            PacketSendUtility.BroadcastMessage(GetOwner(), 341784);
        }
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        DespawnServants();
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        DespawnServants();
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        DespawnServants();
    }

    private void DespawnServants()
    {
        FindServants().ForEach(servant => servant.GetController().DeleteIfAliveOrCancelRespawn());
    }

    private List<Servant> FindServants()
    {
        List<Servant> servants = new List<Servant>();
        GetPosition().GetWorldMapInstance().ForEachNpc(npc =>
        {
            if (npc is Servant servant && GetOwner().Equals(servant.GetCreator()))
            {
                servants.Add(servant);
            }
        });
        return servants;
    }

    private void RndSpawn(int npcId, int count)
    {
        for (int i = 0; i < count; i++)
        {
            SpawnTemplate template = RndSpawnInRange(npcId);
            VisibleObjectSpawner.SpawnEnemyServant(template, GetOwner().GetInstanceId(), GetOwner(), (byte)GetOwner().GetLevel());
        }
    }

    private SpawnTemplate RndSpawnInRange(int npcId)
    {
        double angleRadians = Math.PI / 180 * Rnd.NextFloat(360f);
        float x1 = (float)(Math.Cos(angleRadians) * 5);
        float y1 = (float)(Math.Sin(angleRadians) * 5);
        return Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(GetPosition().GetMapId(), npcId, GetPosition().GetX() + x1, GetPosition().GetY() + y1, GetPosition().GetZ(),
            GetPosition().GetHeading());
    }
}
