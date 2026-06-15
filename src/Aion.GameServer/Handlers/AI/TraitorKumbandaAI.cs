using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Cheatkiller
/// </summary>
[AIName("traitorkumbanda")]
public class TraitorKumbandaAI : AggressiveNpcAI
{
    private bool isFinalBuff;

    public TraitorKumbandaAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (Rnd.Chance() < 5)
        {
            SpawnTimeAccelerator();
            SpawnKumbandaGhost();
        }
        if (!isFinalBuff && GetOwner().GetLifeStats().GetHpPercentage() <= 5)
        {
            isFinalBuff = true;
            AIActions.UseSkill(this, 20942);
        }
    }

    private void SpawnTimeAccelerator()
    {
        if (GetPosition().GetWorldMapInstance().GetNpc(283086) == null)
        {
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20726, 55, GetOwner()).UseNoAnimationSkill();
            Spawn(283086, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0);
            RndSpawn(283086, 6);
        }
    }

    private void SpawnKumbandaGhost()
    {
        if (GetPosition().GetWorldMapInstance().GetNpc(283085) == null && GetOwner().GetLifeStats().GetHpPercentage() <= 50)
        {
            Spawn(283085, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0);
        }
    }

    private void DeleteNpcs(List<Npc> npcs)
    {
        foreach (Npc npc in npcs)
        {
            if (npc != null)
            {
                npc.GetController().Delete();
            }
        }
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        DeleteNpcs(instance.GetNpcs(283086));
        DeleteNpcs(instance.GetNpcs(283088));
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        isFinalBuff = false;
    }

    private void RndSpawn(int npcId, int count)
    {
        for (int i = 0; i < count; i++)
            RndSpawnInRange(npcId, 10, 20);
    }
}
