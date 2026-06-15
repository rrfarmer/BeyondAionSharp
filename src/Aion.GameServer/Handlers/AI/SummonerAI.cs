using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Ai;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/SummonerAI (@author xTz).</summary>
[AIName("summoner")]
public class SummonerAI : AggressiveNpcAI
{
    private readonly List<int> spawnedNpc = new List<int>();
    private List<Percentage> percentage = new List<Percentage>();
    private volatile int spawnedPercent = 0;

    public SummonerAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        CheckPercentage(GetLifeStats().GetHpPercentage());
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        RemoveAndResetHelperSpawns();
        percentage.Clear();
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        RemoveAndResetHelperSpawns();
    }

    protected override void HandleNotAtHome()
    {
        base.HandleNotAtHome();
        if (GetState() == AIState.WALKING)
            RemoveAndResetHelperSpawns();
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        percentage = new List<Percentage>(DataManager.AI_DATA.GetAiTemplate(GetNpcId()).GetSummons().GetPercentage());
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        RemoveAndResetHelperSpawns();
        percentage.Clear();
    }

    private void RemoveAndResetHelperSpawns()
    {
        lock (spawnedNpc)
        {
            foreach (int obj in spawnedNpc)
            {
                VisibleObject? npc = World.World.GetInstance().FindVisibleObject(obj);
                if (npc != null && npc.IsSpawned())
                {
                    npc.GetController().Delete();
                }
            }
            spawnedNpc.Clear();
        }
        spawnedPercent = 0;
    }

    protected void AddHelpersSpawn(int objId)
    {
        lock (spawnedNpc)
        {
            spawnedNpc.Add(objId);
        }
    }

    private void CheckPercentage(int hpPercentage)
    {
        foreach (Percentage percent in percentage)
        {
            if (spawnedPercent != 0 && spawnedPercent <= percent.GetPercent())
            {
                continue;
            }

            if (hpPercentage <= percent.GetPercent())
            {
                int skill = percent.GetSkillId();
                if (skill != 0)
                    AIActions.UseSkill(this, skill);

                if (percent.IsIndividual())
                {
                    HandleIndividualSpawnedSummons(percent);
                }
                else if (percent.GetSummons() != null)
                {
                    HandleBeforeSpawn(percent);
                    foreach (SummonGroup summonGroup in percent.GetSummons())
                    {
                        SummonGroup sg = summonGroup;
                        ThreadPoolManager.GetInstance().Schedule(_ => { SpawnHelpers(sg); return System.Threading.Tasks.ValueTask.CompletedTask; }, (long)summonGroup.GetSchedule());
                    }
                }
                spawnedPercent = percent.GetPercent();
            }
        }
    }

    protected void SpawnHelpers(SummonGroup summonGroup)
    {
        if (!IsDead() && CheckBeforeSpawn())
        {
            int count = Rnd.Get(summonGroup.GetMinCount(), summonGroup.GetMaxCount());
            for (int i = 0; i < count; i++)
            {
                VisibleObject npc;
                if (summonGroup.GetDistance() != 0)
                    npc = RndSpawnInRange(summonGroup.GetNpcId(), summonGroup.GetDistance());
                else
                    npc = Spawn(summonGroup.GetNpcId(), summonGroup.GetX(), summonGroup.GetY(), summonGroup.GetZ(), (sbyte)summonGroup.GetH());

                AddHelpersSpawn(npc.GetObjectId());
            }
            HandleSpawnFinished(summonGroup);
        }
    }

    protected virtual bool CheckBeforeSpawn()
    {
        return true;
    }

    protected virtual void HandleBeforeSpawn(Percentage percent)
    {
    }

    protected virtual void HandleSpawnFinished(SummonGroup summonGroup)
    {
    }

    protected virtual void HandleIndividualSpawnedSummons(Percentage percent)
    {
    }
}
