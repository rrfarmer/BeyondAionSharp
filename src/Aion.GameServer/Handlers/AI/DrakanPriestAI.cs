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

    /// <summary>
    /// Retail'''s <c>live_time</c> on a summoned servant, by npc id.
    /// </summary>
    /// <remarks>
    /// <b>Only 281839 carries one</b> — <c>XDrakan_PeB_ver40</c> gives <c>BXDrakan_ESer_55_An</c> twenty
    /// seconds. The other servant this hierarchy summons, 281621, appears in no timed spawn in the
    /// pattern data and stays permanent, as retail leaves it.
    /// <para>
    /// <b>Without it the summon happened once.</b> <see cref=SpawnServants/> is guarded on finding no
    /// servants already standing, and the only cleanup was <c>HandleBackHome</c> and
    /// <c>HandleDespawned</c> — so a servant that never expired meant the guard never passed again and
    /// the priest summoned nothing for the rest of the fight. <b>Death cleanup is not a lifetime</b>, for
    /// the ninth time in this log.
    /// </para>
    /// <para>
    /// The 240-second naga servants in the same audit row belong to <c>Naga_PeA*</c> and are different
    /// npcs (280638-280640, 281301) that this hierarchy does not spawn. <b>Deliberately not applied.</b>
    /// </para>
    /// </remarks>
    private static int LifeOf(int npcId) => npcId == 281839 ? 20 : 0;

    private void RndSpawn(int npcId, int count)
    {
        for (int i = 0; i < count; i++)
        {
            SpawnTemplate template = RndSpawnInRange(npcId);
            Expire(
                VisibleObjectSpawner.SpawnEnemyServant(
                    template, GetOwner().GetInstanceId(), GetOwner(), (byte)GetOwner().GetLevel()),
                LifeOf(npcId));
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
