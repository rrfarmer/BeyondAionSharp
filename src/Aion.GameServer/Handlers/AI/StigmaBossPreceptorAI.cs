using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/empyreanCrucible/StigmaBossPreceptorAI (@author w4terbomb).</summary>
[AIName("stigma_boss_preceptor")]
public class StigmaBossPreceptorAI : CrucibleBossAI
{
    private const int HEALING_TOTEM = 282375, ETERNAL_SERVANT = 282376;
    private int heavyIn;

    public StigmaBossPreceptorAI(Npc owner) : base(owner)
    {
    }

    private bool IsLight()
    {
        return GetNpcId() == 217586;
    }

    protected override void OnFightStart()
    {
        Say(IsLight() ? 1500223 : 1500226);
        Cast(19615, 15);
        heavyIn = 45;
    }

    protected override void OnFightTick(int seconds, int hp)
    {
        if (Every(seconds, 20, 20))
            Cast(hp > 50 ? 19615 : 19618, 15);
        if (--heavyIn <= 0)
        {
            if (hp > 50)
            {
                Cast(19617, 10);
                Cast(19616, 10, NpcSkillTargetAttribute.RANDOM);
                heavyIn = 45;
            }
            else
            {
                Cast(19621, 10, NpcSkillTargetAttribute.RANDOM);
                WorldPosition p = GetPosition();
                SpawnTemporary(HEALING_TOTEM, p.GetX(), p.GetY(), p.GetZ(), 30000);
                heavyIn = 40;
            }
        }
        if (Every(seconds, 10, 30) && hp <= 50)
        {
            WorldPosition p = GetPosition();
            if (hp > 30)
            {
                Cast(19634, 10, NpcSkillTargetAttribute.ME);
                SpawnServant(p.GetX() + Rnd.Get(-5, 5), p.GetY() + Rnd.Get(-5, 5), p.GetZ());
                SpawnServant(1798.31f, 783.21f, p.GetZ());
                SpawnServant(1798.01f, 810.14f, p.GetZ());
            }
            else
            {
                SpawnServant(p.GetX() + Rnd.Get(-5, 5), p.GetY() + Rnd.Get(-5, 5), p.GetZ());
                SpawnServant(p.GetX() + Rnd.Get(-10, 10), p.GetY() + Rnd.Get(-10, 10), p.GetZ());
                SpawnServant(1798.01f, 810.14f, p.GetZ());
                SpawnServant(1784.74f, 776.63f, p.GetZ());
                SpawnServant(1784.74f, 815.63f, p.GetZ());
            }
        }
        if (Passed(80))
        {
            Cast(19619, 10);
            Cast(19618, 15);
            Cast(19615, 15);
        }
        if (Passed(40))
        {
            Cast(19612, 10, NpcSkillTargetAttribute.ME);
            Cast(19620, 15);
            Cast(19618, 15);
            Cast(19615, 15);
            Cast(19635, 10);
        }
    }

    private void SpawnServant(float x, float y, float z)
    {
        SpawnTemporary(ETERNAL_SERVANT, x, y, z, 45000);
    }

    private void SpawnTemporary(int npcId, float x, float y, float z, int lifeTime)
    {
        if (Spawn(npcId, x, y, z, (sbyte)0) is Npc npc)
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                npc.GetController().DeleteIfAliveOrCancelRespawn();
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(lifeTime));
    }

    protected override void OnFightEnd()
    {
        foreach (Npc npc in GetPosition().GetWorldMapInstance().GetNpcs(HEALING_TOTEM, ETERNAL_SERVANT))
            npc.GetController().Delete();
        if (!IsDead())
            Say(IsLight() ? 1500224 : 1500227);
    }

    protected override void HandleDied()
    {
        Say(IsLight() ? 1500225 : 1500228);
        base.HandleDied();
    }
}
