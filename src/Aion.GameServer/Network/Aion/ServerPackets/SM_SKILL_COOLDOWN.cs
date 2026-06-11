using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_SKILL_COOLDOWN (ATracer, nrg). Sends skill cooldown remaining/duration per skill (single, full player list keyed by cooldownId, or reset). Converges PlayerEnterWorldService. Comparator.comparingInt->Sort comparison; stream toMap->ToDictionary; Map.get null->TryGetValue; nested record Cooldown; currentTimeMillis->UtcNow. PlayerSkillEntry/SKILL_DATA/AionServerPacket red-tolerated.</summary>
public class SM_SKILL_COOLDOWN : AionServerPacket
{
    private readonly List<Cooldown> cooldowns = new List<Cooldown>();
    private readonly bool notify;

    public SM_SKILL_COOLDOWN(int skillId, long expirationTimeMillis)
    {
        cooldowns.Add(new Cooldown(skillId, expirationTimeMillis));
        notify = true;
    }

    public SM_SKILL_COOLDOWN(Player player, Dictionary<int, long> cooldownExpirationMillisByCooldownId, bool notify)
    {
        foreach (PlayerSkillEntry skill in player.GetSkillList().GetAllSkills())
        {
            int cooldownId = DataManager.SKILL_DATA.GetSkillTemplate(skill.GetSkillId()).GetCooldownId();
            if (cooldownExpirationMillisByCooldownId.TryGetValue(cooldownId, out long cooldownExpirationMillis))
                cooldowns.Add(new Cooldown(skill.GetSkillId(), cooldownExpirationMillis));
        }
        // The game plays the same icon cooldown animation for all skills that share a cooldownId and the last entry per cooldownId wins, so we sort by
        // animation duration to avoid animations that are shorter than the remaining cooldown time.
        cooldowns.Sort((a, b) => a.GetDurationMillis().CompareTo(b.GetDurationMillis()));
        this.notify = notify;
    }

    /// <summary>
    /// Creates a skill cooldown reset packet
    /// </summary>
    public SM_SKILL_COOLDOWN(Player player, ICollection<int> resettableCooldownIds)
        : this(player, resettableCooldownIds.ToDictionary(c => c, _ => 0L), true)
    {
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(cooldowns.Count);
        WriteC(notify ? 1 : 0); // 1 will trigger a notification sound and animation on all sent skills
        foreach (Cooldown cooldown in cooldowns)
        {
            WriteH(cooldown.skillId);
            WriteD(cooldown.GetRemainingSeconds());
            WriteD(cooldown.GetDurationMillis()); // 0 also seems to always work
        }
    }

    private record Cooldown(int skillId, long expirationTimeMillis)
    {
        public int GetRemainingSeconds()
        {
            return expirationTimeMillis == 0 ? 0 : (int)Math.Max(0, (expirationTimeMillis - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / 1000);
        }

        public int GetDurationMillis()
        {
            return DataManager.SKILL_DATA.GetSkillTemplate(skillId).GetCooldown() * 100;
        }
    }
}
