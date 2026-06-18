using System;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/UseSkill (Source, kecimis, Estrayl, Neon). Use (or let a target use) any skill, even those not in skill list.</summary>
public class UseSkill : AdminCommand
{
    public UseSkill()
        : base("useskill", "Use (or let a target use) any skill, even those not in skill list.")
    {
        SetSyntaxInfo(
            "<id> [lvl] [f] - Uses the skill with the specified skill level on your target (f = force use).",
            "<me|self|target> <id> [lvl] [f] - Lets your target use the skill on you, itself or its target (f = force use).");
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        try
        {
            string targetMode = paramsArr[0].ToLower();
            int i = 0;
            switch (targetMode)
            {
                case "me":
                case "self":
                case "target":
                    i++;
                    break;
                default:
                    targetMode = null;
                    break;
            }
            SkillTemplate template = DataManager.SKILL_DATA.GetSkillTemplate(int.Parse(paramsArr[i++]));
            if (template != null)
            {
                int skillLevel = paramsArr.Length > i && IsNumber(paramsArr[i]) ? int.Parse(paramsArr[i++]) : template.GetLvl();
                bool forceUse = paramsArr.Length > i && paramsArr[i].Equals("f");
                if (DoUseSkill(admin, template, skillLevel, targetMode, forceUse))
                    SendInfo(admin, "Used skill: " + template.GetL10n());
                else
                    SendInfo(admin, "Could not use skill (" + (forceUse ? "missing preconditions" : "add parameter 'f' to force use") + ").");
            }
            else
            {
                SendInfo(admin, "Invalid skill id.");
            }
        }
        catch (FormatException)
        {
            SendInfo(admin, "Invalid skill id or level.");
        }
    }

    private bool DoUseSkill(Player player, SkillTemplate template, int skillLevel, string targetMode, bool forceUse)
    {
        Creature effector;
        VisibleObject target;
        if (targetMode != null)
        {
            if (!(player.GetTarget() is Creature creatureTarget))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
                return false;
            }
            effector = creatureTarget;
            target = GetTarget(player, targetMode);
        }
        else
        {
            effector = player;
            target = player.GetTarget();
        }

        Skill skill = SkillEngine.SkillEngine.GetInstance().GetSkill(effector, template.GetSkillId(), skillLevel, target);
        if (skill != null)
            return forceUse ? skill.UseWithoutPropSkill() : skill.UseNoAnimationSkill();

        return false;
    }

    private VisibleObject GetTarget(Player player, string targetMode)
    {
        switch (targetMode)
        {
            case "me":
                return player;
            case "self":
                return player.GetTarget();
            case "target":
                return player.GetTarget() == null ? null : player.GetTarget().GetTarget();
            default:
                return null;
        }
    }

    // Java parity: org.apache.commons.lang3.math.NumberUtils.isNumber(String).
    private static bool IsNumber(string str)
    {
        if (string.IsNullOrEmpty(str))
            return false;
        char[] chars = str.ToCharArray();
        int sz = chars.Length;
        bool hasExp = false;
        bool hasDecPoint = false;
        bool allowSigns = false;
        bool foundDigit = false;
        int start = (chars[0] == '-' || chars[0] == '+') ? 1 : 0;
        if (sz > start + 1 && chars[start] == '0' && (chars[start + 1] == 'x' || chars[start + 1] == 'X'))
        {
            int i = start + 2;
            if (i == sz)
                return false;
            for (; i < chars.Length; i++)
            {
                if ((chars[i] < '0' || chars[i] > '9') && (chars[i] < 'a' || chars[i] > 'f') && (chars[i] < 'A' || chars[i] > 'F'))
                    return false;
            }
            return true;
        }
        sz--;
        int idx = start;
        while (idx < sz || (idx < sz + 1 && allowSigns && !foundDigit))
        {
            if (chars[idx] >= '0' && chars[idx] <= '9')
            {
                foundDigit = true;
                allowSigns = false;
            }
            else if (chars[idx] == '.')
            {
                if (hasDecPoint || hasExp)
                    return false;
                hasDecPoint = true;
            }
            else if (chars[idx] == 'e' || chars[idx] == 'E')
            {
                if (hasExp)
                    return false;
                if (!foundDigit)
                    return false;
                hasExp = true;
                allowSigns = true;
            }
            else if (chars[idx] == '+' || chars[idx] == '-')
            {
                if (!allowSigns)
                    return false;
                allowSigns = false;
                foundDigit = false;
            }
            else
            {
                return false;
            }
            idx++;
        }
        if (idx < chars.Length)
        {
            if (chars[idx] >= '0' && chars[idx] <= '9')
                return true;
            if (chars[idx] == 'e' || chars[idx] == 'E')
                return false;
            if (chars[idx] == '.')
            {
                if (hasDecPoint || hasExp)
                    return false;
                return foundDigit;
            }
            if (!allowSigns && (chars[idx] == 'd' || chars[idx] == 'D' || chars[idx] == 'f' || chars[idx] == 'F'))
                return foundDigit;
            if (chars[idx] == 'l' || chars[idx] == 'L')
                return foundDigit && !hasExp && !hasDecPoint;
            return false;
        }
        return !allowSigns && foundDigit;
    }
}
