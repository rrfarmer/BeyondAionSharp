using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/AuraEffect (ATracer, kecimis, xTz) : EffectTemplate. @XmlAttribute distance/distance_z/skill_id; applyEffect: Player double-cast abuse guard via AuditLogger, else addToEffectedController; onPeriodicAction: Npc→applyAuraTo self, Player online+inTeam→BOOST_MANTRA_RANGE-scaled range, group members in range/rangeZ→applyAuraTo, else self; SM_MANTRA_EFFECT broadcast; applyAuraTo→Aion.GameServer.SkillEngine.SkillEngine.applyEffect(skillId); startEffect: setPeriodicTask(scheduleAtFixedRate AuraTask 0,6500ms, Position); inner Runnable AuraTask→nested (onPeriodicAction + Thread.Yield). AuditLogger/SM_MANTRA_EFFECT red-tolerated.</summary>
[XmlType("AuraEffect")]
public class AuraEffect : EffectTemplate
{
    [XmlAttribute]
    public int distance;
    [XmlAttribute("distance_z")]
    public int distanceZ;
    [XmlAttribute("skill_id")]
    public int skillId;

    public override void ApplyEffect(Effect effect)
    {
        Creature effector = effect.GetEffector();
        if (effector is Player && effector.GetEffectController().FindBySkillId(effect.GetSkillId()) != null)
        {
            AuditLogger.Log((Player)effector, "might be abusing CM_CASTSPELL mantra effect, skill id: " + effect.GetSkillId());
            return;
        }
        effect.AddToEffectedController();
    }

    public override void OnPeriodicAction(Effect effect)
    {
        Creature effector = effect.GetEffector();
        if (effector is Npc)
        {
            ApplyAuraTo(effector);
        }
        else
        {
            Player p = (Player)effector;
            if (!p.IsOnline())
            { // task check
                return;
            }
            if (p.IsInTeam())
            {
                int rangeBoost = effector.GetGameStats().GetStat(StatEnum.BOOST_MANTRA_RANGE, 100).GetCurrent();
                float rangeZ = distanceZ * rangeBoost / 100f;
                float range = distance * rangeBoost / 100f;
                foreach (Player player in p.GetCurrentGroup().GetOnlineMembers())
                {
                    if (p.Equals(player) || Math.Abs(p.GetZ() - player.GetZ()) <= rangeZ && PositionUtil.IsInRange(p, player, range, false))
                    {
                        ApplyAuraTo(player);
                    }
                }
            }
            else
            {
                ApplyAuraTo(effector);
            }
        }
        PacketSendUtility.BroadcastPacket(effector, new SM_MANTRA_EFFECT(effector, skillId));
    }

    private void ApplyAuraTo(Creature effected)
    {
        Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffect(skillId, effected, effected);
    }

    public override void StartEffect(Effect effect)
    {
        AuraTask task = new AuraTask(this, effect);
        effect.SetPeriodicTask(ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct => { task.Run(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(6500)), Position);
    }

    private sealed class AuraTask
    {
        private readonly AuraEffect outer;
        private readonly Effect effect;

        public AuraTask(AuraEffect outer, Effect effect)
        {
            this.outer = outer;
            this.effect = effect;
        }

        public void Run()
        {
            outer.OnPeriodicAction(effect);
            // This has the special effect of clearing the current thread's quantum and putting it to the end of the queue for its priority level. Will just
            // give-up the thread's turn, and gain it in the next round.
            Thread.Yield();
        }
    }

    public override void EndEffect(Effect effect)
    {
    }
}
