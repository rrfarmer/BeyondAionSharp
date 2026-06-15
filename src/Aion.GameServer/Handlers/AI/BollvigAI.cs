using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/heiron/BollvigAI (Ritsu).</summary>
[AIName("bollvig")]
public class BollvigAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(75, 50, 25);
    private ScheduledTask firstTask;
    private ScheduledTask secondTask;
    private ScheduledTask thirdTask;
    private ScheduledTask lastTask;

    public BollvigAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        Npc npc = GetPosition().GetWorldMapInstance().GetNpc(204655);
        if (npc != null)
            npc.GetController().Delete();
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 75:
            case 50:
                CancelTask();
                UseFirstSkillTree();
                break;
            case 25:
                CancelTask();
                FirstSkill();
                break;
        }
    }

    private void UseFirstSkillTree()
    {
        UseSkill(17861); // Sleep of Death
        RndSpawnInRange(280802);
        RndSpawnInRange(280802);
        RndSpawnInRange(280803);
        RndSpawnInRange(280803);
        FirstSkill();
    }

    private void FirstSkill()
    {
        int hpPercent = GetLifeStats().GetHpPercentage();
        if (50 >= hpPercent && hpPercent > 25)
        {
            firstTask = ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                UseSkill(18034); // Nerve Absorption
                RndSpawnInRange(280804);
                return ValueTask.CompletedTask;
            }, System.TimeSpan.FromMilliseconds(10000));
        }
        else if (hpPercent <= 25)
        {
            UseSkill(18037); // Blood Cell Destruction
        }

        secondTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            SkillThree();
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(31000));
    }

    private void SkillThree()
    {
        UseSkill(17899); // Charming Attraction
        thirdTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            int hpPercent = GetLifeStats().GetHpPercentage();
            if (hpPercent <= 75 && hpPercent > 50)
            {
                UseSkill(18025); // Curse of Soul
                FirstSkill();
            }
            else if (hpPercent <= 50 && hpPercent > 25)
            {
                UseSkill(18025); // Curse of Soul
                FirstSkill();
            }
            else if (hpPercent <= 25)
            {
                UseSkill(18027); // Mortal Cutting
                lastTask = ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    SkillThree();
                    return ValueTask.CompletedTask;
                }, System.TimeSpan.FromMilliseconds(11000));
            }

            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(5000));
    }

    private void CancelTask()
    {
        if (firstTask != null && !firstTask.IsDone())
            firstTask.Cancel(true);
        else if (secondTask != null && !secondTask.IsDone())
            secondTask.Cancel(true);
        else if (thirdTask != null && !thirdTask.IsDone())
            thirdTask.Cancel(true);
        else if (lastTask != null && !lastTask.IsDone())
            lastTask.Cancel(true);
    }

    private void RndSpawnInRange(int npcId)
    {
        double angleRadians = (Math.PI / 180) * Rnd.NextFloat(360f);
        float x = (float)(Math.Cos(angleRadians) * 10);
        float y = (float)(Math.Sin(angleRadians) * 10);
        Spawn(npcId, 1001 + x, 2828 + y, 235.66f, (sbyte)0);
    }

    private void UseSkill(int skillId)
    {
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), skillId, 50, GetTarget()).UseSkill();
    }

    protected override void HandleBackHome()
    {
        CancelTask();
        base.HandleBackHome();
        hpPhases.Reset();
    }

    protected override void HandleDespawned()
    {
        CancelTask();
        DeleteSummons(280802);
        DeleteSummons(280803);
        DeleteSummons(280804);
        base.HandleDespawned();
        if (CheckNpc())
            Spawn(204655, 1001f, 2828f, 235.66f, (sbyte)0);
    }

    protected override void HandleDied()
    {
        CancelTask();
        DeleteSummons(280802);
        DeleteSummons(280803);
        DeleteSummons(280804);
        base.HandleDied();
        if (CheckNpc())
            Spawn(204655, 1001f, 2828f, 235.66f, (sbyte)0);
    }

    private void DeleteSummons(int npcId)
    {
        if (GetPosition().GetWorldMapInstance().GetNpcs(npcId) != null)
        {
            List<Npc> npcs = GetPosition().GetWorldMapInstance().GetNpcs(npcId);
            foreach (Npc npc in npcs)
            {
                npc.GetController().Delete();
            }
        }
    }

    private bool CheckNpc()
    {
        WorldMapInstance map = GetPosition().GetWorldMapInstance();
        return map.GetNpc(204655) == null && (map.GetNpc(212314) == null || map.GetNpc(212314).IsDead());
    }
}
