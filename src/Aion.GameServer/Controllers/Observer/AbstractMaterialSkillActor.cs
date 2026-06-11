using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Materials;
using Aion.GameServer.Model.Templates.World;
using Aion.GameServer.Services;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Time.Gametime;

namespace Aion.GameServer.Controllers.Observer;

/// <summary>Java parity: controllers/observer/AbstractMaterialSkillActor (Yeats, Neon) : AbstractCollisionObserver. AtomicReference&lt;Future&lt;?&gt;&gt;→ScheduledTask field + Interlocked (compareAndSet→CompareExchange==null, getAndSet→Exchange); scheduleAtFixedRate(Runnable)→ScheduleAtFixedRateTask(ct=>{task.Run();...}); stateful inner MaterialSkillTask→nested class capturing outer; synchronized(skills)→lock; volatile fields; enum ==; getClass().getSimpleName()→GetType().Name; sbyte intentions (matches base). MaterialSkill/Effect.ForceType/SkillEngine red-tolerated.</summary>
public abstract class AbstractMaterialSkillActor : AbstractCollisionObserver
{
    private ScheduledTask task;
    private readonly TaskId taskId;
    protected volatile List<MaterialSkill> skills;
    protected volatile bool isTouched = false;

    public AbstractMaterialSkillActor(Creature creature, Spatial geometry, sbyte intentions, CheckType checkType, TaskId taskId, List<MaterialSkill> skills)
        : base(creature, geometry, intentions, checkType)
    {
        this.taskId = taskId;
        this.skills = skills;
    }

    public void Act()
    {
        if (skills.Count != 0 && !creature.GetController().HasTask(taskId))
        {
            MaterialSkillTask msTask = new MaterialSkillTask(this);
            ScheduledTask t = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct => { msTask.Run(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(1000));
            if (Interlocked.CompareExchange(ref task, t, null) == null)
                creature.GetController().AddTask(taskId, task);
            else // should not happen
                t.Cancel(false);
        }
    }

    public void Abort()
    {
        ScheduledTask t = Interlocked.Exchange(ref task, (ScheduledTask)null);
        if (t != null)
            creature.GetController().CancelTaskIfPresent(taskId, t);
    }

    public override void Died(Creature creature)
    {
        isTouched = false;
        Abort();
    }

    private MaterialSkill FindFirstSkillWithMatchingCondition()
    {
        lock (skills)
        {
            foreach (MaterialSkill skill in skills)
            {
                if (MatchActConditions(skill))
                    return skill;
            }
        }
        return null;
    }

    private bool MatchActConditions(MaterialSkill skill)
    {
        if (skill.GetConditions().Count == 0)
            return true;
        foreach (MaterialActCondition condition in skill.GetConditions())
        {
            if (condition == MaterialActCondition.NIGHT && GameTimeService.GetInstance().GetGameTime().GetDayTime() == DayTime.NIGHT)
                return true;
            if (condition == MaterialActCondition.SUNNY) // sunny actually means "not raining" (fireplaces don't burn during rain)
            {
                WeatherEntry weatherEntry = WeatherService.GetInstance().FindWeatherEntry(creature);
                bool isRain = weatherEntry.GetWeatherName() != null && weatherEntry.GetWeatherName().StartsWith("RAIN");
                if (!isRain || weatherEntry.IsBefore()) // before means "before" the weather (e.g. clouds before rain)
                    return true;
            }
        }
        return false;
    }

    private class MaterialSkillTask
    {
        private readonly AbstractMaterialSkillActor outer;
        private MaterialSkill skill;
        private int secondsElapsed;

        public MaterialSkillTask(AbstractMaterialSkillActor outer)
        {
            this.outer = outer;
        }

        public void Run()
        {
            if (secondsElapsed++ % (skill == null ? 1 : skill.GetFrequency()) != 0)
                return;
            if (!outer.isTouched)
                return;
            if (!outer.creature.IsSpawned() || outer.creature.IsDead())
                return;
            if (outer.creature is Player player && player.IsProtectionActive())
                return;
            if ((skill = outer.FindFirstSkillWithMatchingCondition()) == null) // skip if currently nothing matches (fires are off while raining)
                return;
            if (GeoDataConfig.GEO_MATERIALS_SHOWDETAILS && outer.creature is Player player2 && player2.IsStaff())
                PacketSendUtility.SendMessage(player2, outer.GetType().Name + " use skill=" + skill.GetId());
            SkillEngine.GetInstance().ApplyEffectDirectly(skill.GetId(), skill.GetSkillLevel(), outer.creature, outer.creature, null, Effect.ForceType.MATERIAL_SKILL);
        }
    }
}
