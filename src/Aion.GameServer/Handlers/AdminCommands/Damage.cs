using System.Text.RegularExpressions;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using TYPE = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE;
using LOG = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.LOG;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Damage (Source).</summary>
public class Damage : AdminCommand
{
    public Damage()
        : base("damage")
    {
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length > 2)
            Info(admin, null);

        VisibleObject target = admin.GetTarget();
        if (target == null)
            PacketSendUtility.SendMessage(admin, "No target selected");
        else if (target is Creature)
        {
            Creature creature = (Creature)target;
            int dmg;
            string damageType = "hp";
            bool isPercent = false;
            if (paramsArr[0].Equals("mp", System.StringComparison.OrdinalIgnoreCase))
            {
                damageType = "mp";
            }
            else if (paramsArr[0].Equals("fp", System.StringComparison.OrdinalIgnoreCase))
            {
                damageType = "fp";
            }
            else if (paramsArr[0].Equals("dp", System.StringComparison.OrdinalIgnoreCase))
            {
                damageType = "dp";
            }

            if (damageType.Equals("fp", System.StringComparison.OrdinalIgnoreCase) || damageType.Equals("dp", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!(creature is Player))
                {
                    Info(admin, null);
                    return;
                }
            }

            if (!damageType.Equals("hp", System.StringComparison.OrdinalIgnoreCase) && paramsArr.Length != 2)
            {
                Info(admin, null);
                return;
            }

            try
            {
                string percent = paramsArr[0];
                if (!damageType.Equals("hp", System.StringComparison.OrdinalIgnoreCase))
                    percent = paramsArr[1];
                Match result = Regex.Match(percent, "([^%]+)%");

                if (result.Success)
                {
                    dmg = int.Parse(result.Groups[1].Value);
                    isPercent = true;
                }
                else if (damageType.Equals("hp", System.StringComparison.OrdinalIgnoreCase))
                    dmg = int.Parse(paramsArr[0]);
                else
                    dmg = int.Parse(paramsArr[1]);

                if (dmg <= 100)
                    isPercent = true;

                switch (damageType)
                {
                    case "hp":
                        if (isPercent)
                            dmg = (int)(dmg / 100f * creature.GetLifeStats().GetMaxHp());
                        // Java: onAttack(creature, dmg, null) — C# 3-arg overload takes non-nullable AttackStatus, so call the deeper overload it delegates to with status=null.
                        creature.GetController().OnAttack(creature, null, TYPE.REGULAR, dmg, true, LOG.REGULAR, null, Aion.GameServer.SkillEngine.Model.HopType.DAMAGE);
                        break;
                    case "mp":
                        if (isPercent)
                            dmg = (int)(dmg / 100f * creature.GetLifeStats().GetMaxMp());
                        creature.GetLifeStats().ReduceMp(TYPE.DAMAGE_MP, dmg, 0, LOG.MPATTACK);
                        break;
                    case "fp":
                        if (isPercent)
                            dmg = (int)(dmg / 100f * ((Player)creature).GetLifeStats().GetMaxFp());
                        ((Player)creature).GetLifeStats().ReduceFp(TYPE.FP_DAMAGE, dmg, 0, LOG.FPATTACK);
                        break;
                    case "dp":
                        if (isPercent)
                            dmg = (int)(dmg / 100f * 4000f);
                        if (dmg > ((Player)creature).GetCommonData().GetDp())
                            dmg = ((Player)creature).GetCommonData().GetDp();
                        ((Player)creature).GetCommonData().SetDp(((Player)creature).GetCommonData().GetDp() - dmg);
                        break;
                }
            }
            catch (System.Exception)
            {
                Info(admin, null);
            }
        }
    }

    private void Info(Player player, string message)
    {
        PacketSendUtility.SendMessage(player, "syntax //damage (mp/fp/dp) <dmg | dmg%>" + "\n<dmg> must be a number."
            + "\n(mp/fp/dp) is optional, leave out to use HP damage" + "\nin case of fp/dp, target must be player!");
    }
}
