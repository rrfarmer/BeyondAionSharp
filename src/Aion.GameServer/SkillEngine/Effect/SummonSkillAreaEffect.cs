using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using System.Xml.Serialization;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/SummonSkillAreaEffect (ATracer) : SummonServantEffect. x==0&&y==0→effected pos fallback; group switch-arrows→switch statement (KN_THREATENINGWAVE/WI_SUMMONTORNADO/WI_DELAYEDSTRIKE); spawnServant(SKILLAREA); stateful anonymous Runnable(skillPos)→nested SkillAreaTask capturing servant; scheduleAtFixedRate(0,tickDelay)→ScheduleAtFixedRateTask; addTask(SKILL_USE). Servant/group red-tolerated.</summary>
[XmlType("SummonSkillAreaEffect")]
public class SummonSkillAreaEffect : SummonServantEffect
{
    public override void ApplyEffect(Effect effect)
    {
        float x = effect.GetX();
        float y = effect.GetY();
        float z = effect.GetZ();
        if (x == 0 && y == 0)
        {
            Creature effected = effect.GetEffected();
            x = effected.GetX();
            y = effected.GetY();
            z = effected.GetZ();
        }

        int tickDelay = 3000;
        int spawnDuration = time;

        string group = effect.GetSkillTemplate().GetGroup();
        if (group != null)
        {
            switch (group)
            {
                case "KN_THREATENINGWAVE":
                    spawnDuration = 15; // client files say 11s but description 15s
                    tickDelay = 2000;
                    break;
                case "WI_SUMMONTORNADO":
                    tickDelay = 1900;
                    break;
                case "WI_DELAYEDSTRIKE":
                    tickDelay = 5000;
                    spawnDuration = 9;
                    break;
            }
        }

        Servant servant = SpawnServant(effect, spawnDuration, NpcObjectType.SKILLAREA, x, y, z);
        if (effect.GetEffected() != null) // point skill without any initial target (we cannot trigger handleAttack with a null target)
            servant.GetAi().OnCreatureEvent(AiEventType.ATTACK, effect.GetEffected());

        SkillAreaTask msTask = new SkillAreaTask(servant);
        ScheduledTask task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct => { msTask.Run(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(tickDelay));
        servant.GetController().AddTask(TaskId.SKILL_USE, task);
    }

    private sealed class SkillAreaTask
    {
        private readonly Servant servant;
        private int skillPos = 0;

        public SkillAreaTask(Servant servant)
        {
            this.servant = servant;
        }

        public void Run()
        {
            servant.GetController().UseSkill(servant.GetSkillList().GetSkillOnPosition(skillPos).GetSkillId());
            skillPos++;
        }
    }
}
