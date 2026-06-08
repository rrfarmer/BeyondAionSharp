using System;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.condition;

/// <summary>
/// Java parity: skillengine/condition/ChainCondition (ATracer, kecimis, Neon).
/// </summary>
public class ChainCondition : Condition
{
    [XmlAttribute("selfcount")]
    public int selfCount = 1;

    [XmlAttribute("precount")]
    public int preCount = 1;

    [XmlAttribute("category")]
    public string? category;

    [XmlAttribute("precategory")]
    public string? preCategory;

    [XmlAttribute("time")]
    public int time;

    public override bool Validate(Skill env)
    {
        if (env.GetEffector() is Player player)
        {
            ChainSkills chain = player.GetChainSkills();
            ChainSkill currentSkill = chain.GetCurrentChainSkill();

            if (ShouldReset(chain, env))
                chain.ResetChain();

            if (preCategory != null)
            {
                if (currentSkill.GetCategory().Equals(preCategory))
                {
                    if (currentSkill.GetUseCount() < preCount) // preCategory skill must have been activated x times
                        return false;
                }
                else if (!chain.GetPreviousChainSkill().GetCategory().Equals(preCategory)) // previously activated skill must match
                {
                    return false;
                }
            }
        }

        env.SetChainCategory(category);
        env.SetChainUsageDuration(time);
        return true;
    }

    private bool ShouldReset(ChainSkills chain, Skill env)
    {
        ChainSkill currentSkill = chain.GetCurrentChainSkill();
        if (currentSkill.GetCategory().Length != 0)
        {
            if (chain.IsChainExpired()) // check max allowed use time
                return true;

            if (preCategory == null && category!.Contains("_1TH")) // first skill of a chain
            {
                if (!currentSkill.GetCategory().Equals(category)) // other skill
                    return true;
                if (currentSkill.GetUseCount() == selfCount) // same skill
                    return true;
                int maxActiveDuration = time > 0 ? time : env.GetCooldown() * 100; // template cooldown is seconds * 10...
                if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > currentSkill.GetLastUseTime() + maxActiveDuration)
                    return true;
            }
        }

        return false;
    }

    /// <summary>Number of allowed skill activations of this chain skill.</summary>
    public int GetAllowedActivations()
    {
        return selfCount;
    }

    public string? GetCategory()
    {
        return category;
    }

    public int GetTime()
    {
        return time;
    }
}
