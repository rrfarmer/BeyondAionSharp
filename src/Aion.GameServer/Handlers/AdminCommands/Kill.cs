using System;
using System.Linq;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Kill (ATracer, Wakizashi, Neon, Sykra).</summary>
public class Kill : AdminCommand
{
    public Kill()
        : base("kill", "Kills the specified NPC(s) or player.")
    {
        SetSyntaxInfo(
            " - kills your target (can be NPC or player)",
            "all [neutral|enemy|npcId] - kills all NPCs in the surrounding area (default: all, optional: only neutral/hostile NPCs/specific NPC)",
            "<range (in meters)> [neutral|enemy|npcId] - kills NPCs in the specified radius around you (default: all, optional: only neutral/hostile NPCs/specific NPC)");
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        VisibleObject target = player.GetTarget();

        if (paramsArr.Length > 2 || (paramsArr.Length == 0 && target == null))
        {
            SendInfo(player);
            return;
        }

        if (paramsArr.Length == 0)
        {
            if (target is Creature creature)
            {
                string targetInfo = target.GetType().Name.ToLower() + ": ";
                if (target is Npc)
                    targetInfo += ChatUtil.Path(target, true);
                else
                    targetInfo += ChatUtil.Capitalize(target.Name);
                if (DoKill(player, creature))
                    SendInfo(player, "Killed " + targetInfo);
                else
                    SendInfo(player, "Couldn't kill " + targetInfo);
            }
            else
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
            }
        }
        else
        {
            Func<Creature, bool> filter;
            if (paramsArr[0].Equals("all", System.StringComparison.OrdinalIgnoreCase))
            {
                filter = _ => true;
            }
            else
            {
                float range = ParseFloat(paramsArr[0]);
                if (range < 0)
                {
                    SendInfo(player, "The given range must be larger than 0.");
                    return;
                }
                // if input was integer, add 0.999 so it matches the client's displayed target distance (client doesn't round up at .5)
                float finalRange = range == Math.Round(range) ? range + 0.999f : range;
                filter = creature => PositionUtil.IsInRange(player, creature, finalRange);
            }
            if (paramsArr.Length == 2)
            {
                Func<Creature, bool> baseFilter = filter;
                if (paramsArr[1].Equals("neutral", System.StringComparison.OrdinalIgnoreCase))
                {
                    filter = creature => baseFilter(creature) && !player.IsEnemy(creature);
                }
                else if (paramsArr[1].Equals("enemy", System.StringComparison.OrdinalIgnoreCase))
                {
                    filter = creature => baseFilter(creature) && player.IsEnemy(creature);
                }
                else
                {
                    int npcId = ParseInt(paramsArr[1]);
                    filter = creature => baseFilter(creature) && creature.GetObjectTemplate().GetTemplateId() == npcId;
                }
            }
            int count = 0;
            foreach (Creature creature in player.GetKnownList().Stream()
                .Where(obj => obj.Get() is Creature c && !(c is Player))
                .Select(o => (Creature)o.Get())
                .Where(filter)
                .ToList())
            {
                if (DoKill(player, creature))
                    count++;
            }
            SendInfo(player, count + " NPC(s) were killed.");
        }
    }

    private bool DoKill(Player attacker, Creature target)
    {
        if (target.IsDead() || target.GetLifeStats().IsAboutToDie())
            return false;

        // Java: onAttack(..., maxHp, null) — C# 3-arg overload takes non-nullable AttackStatus, so call the deeper overload it delegates to with status=null.
        target.GetController().OnAttack(target.IsPvpTarget(attacker) && !target.IsEnemy(attacker) ? target : attacker, null,
            Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE.REGULAR, target.GetLifeStats().GetMaxHp(), true,
            Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.LOG.REGULAR, null, Aion.GameServer.SkillEngine.Model.HopType.DAMAGE);
        return true;
    }
}
