using System.Text.RegularExpressions;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using TYPE = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE;
using LOG = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.LOG;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Heal (Mrakobes, Loxo).</summary>
public class Heal : AdminCommand
{
    public Heal()
        : base("heal", "Restores HP, MP, DP, flight time and energy of repose.")
    {
        SetSyntaxInfo(
            " - Heals your targets HP, MP and removes soul sickness.",
            "<dp> - Heals your targets DP.",
            "<fp> - Heals your targets flight time.",
            "<repose> - Heals your targets energy of repose.",
            "<number> - Heals your targets HP by given amount.",
            "<number%> - Heals your targets HP by given percentage.");
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        VisibleObject target = player.GetTarget();
        if (!(target is Creature))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
            return;
        }

        Creature creature = (Creature)target;

        if (paramsArr.Length == 0)
        {
            creature.GetLifeStats().IncreaseHp(TYPE.HP, creature.GetLifeStats().GetMaxHp());
            creature.GetLifeStats().IncreaseMp(TYPE.HEAL_MP, creature.GetLifeStats().GetMaxMp(), 0, LOG.MPHEAL);
            creature.GetEffectController().RemoveByDispelSlotType(DispelSlotType.SPECIAL2);
            if (!player.Equals(creature))
                SendInfo(player, creature.GetName() + " has been refreshed.");
        }
        else if (paramsArr[0].Equals("dp", System.StringComparison.OrdinalIgnoreCase) && creature is Player)
        {
            Player targetPlayer = (Player)creature;
            targetPlayer.GetCommonData().SetDp(targetPlayer.GetGameStats().GetMaxDp().GetCurrent());
            if (!player.Equals(creature))
                SendInfo(player, targetPlayer.GetName() + "'s DP have been fully refreshed.");
        }
        else if (paramsArr[0].Equals("fp", System.StringComparison.OrdinalIgnoreCase) && creature is Player)
        {
            Player targetPlayer = (Player)creature;
            targetPlayer.GetLifeStats().SetCurrentFp(targetPlayer.GetLifeStats().GetMaxFp());
            if (!player.Equals(creature))
                SendInfo(player, targetPlayer.GetName() + "'s flight time has been fully refreshed.");
        }
        else if (paramsArr[0].Equals("repose", System.StringComparison.OrdinalIgnoreCase) && creature is Player)
        {
            Player targetPlayer = (Player)creature;
            PlayerCommonData pcd = targetPlayer.GetCommonData();
            pcd.SetCurrentReposeEnergy(pcd.GetMaxReposeEnergy());
            PacketSendUtility.SendPacket(targetPlayer,
                new SM_STATUPDATE_EXP(pcd.GetExpShown(), pcd.GetExpRecoverable(), pcd.GetExpNeed(), pcd.GetCurrentReposeEnergy(), pcd.GetMaxReposeEnergy()));
            if (!player.Equals(creature))
                SendInfo(player, targetPlayer.GetName() + "'s Energy of Repose has been fully refreshed.");
        }
        else
        {
            try
            {
                Match result = Regex.Match(paramsArr[0], "(.+)%");
                int value;

                if (result.Success)
                {
                    int hpPercent = int.Parse(result.Groups[1].Value);

                    if (hpPercent < 100)
                        value = (int)(hpPercent / 100f * creature.GetLifeStats().GetMaxHp());
                    else
                        value = creature.GetLifeStats().GetMaxHp();
                }
                else
                    value = int.Parse(paramsArr[0]);
                creature.GetLifeStats().IncreaseHp(TYPE.HP, value);
                if (!player.Equals(creature))
                    SendInfo(player, creature.GetName() + " has been healed by " + value + " health points!");
            }
            catch (System.Exception)
            {
                SendInfo(player);
            }
        }
    }
}
