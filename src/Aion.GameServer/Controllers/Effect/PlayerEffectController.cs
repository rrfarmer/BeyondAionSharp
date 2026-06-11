using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Event;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;
using ForceType = Aion.GameServer.Skillengine.Model.Effect.ForceType;

namespace Aion.GameServer.Controllers.Effect;

/// <summary>Java parity: controllers/effect/PlayerEffectController (ATracer) extends EffectController. Player-specific effect handling: duel-condition debuff guard, icon + group/alliance updates on add/clear/removeAll, removeNonStorableEffectsForLogout, addSavedEffect (restore persisted buffs, deity-avatar logout timing), cumulative-resist duration tracking, keep-buffs-on-die. EnumMap->Dictionary; synchronized->lock; computeIfAbsent->TryGetValue-or-add; covariant override GetOwner->Player; stream.filter.forEach->LINQ; Collections.singletonList->new List; currentTimeMillis->UtcNow. EffectController/Effect/SkillTargetSlot/CumulativeResist red-tolerated.</summary>
public class PlayerEffectController : EffectController
{
    private readonly Dictionary<CumulativeResistType, CumulativeResist> cumulativeResistInfo = new Dictionary<CumulativeResistType, CumulativeResist>();
    private bool keepBuffsOnDie;

    public PlayerEffectController(Creature owner)
        : base(owner)
    {
    }

    public void SetKeepBuffsOnDie(bool keepBuffsOnDie)
    {
        this.keepBuffsOnDie = keepBuffsOnDie;
    }

    public override void AddEffect(Effect effect)
    {
        if (CheckDuelCondition(effect) && !effect.IsForcedEffect())
            return;
        base.AddEffect(effect);
        UpdatePlayerIconsAndGroup(effect);
    }

    public override void ClearEffect(Effect effect, bool broadcast)
    {
        base.ClearEffect(effect, broadcast);
        if (broadcast)
            UpdatePlayerIconsAndGroup(effect);
    }

    public override Player GetOwner()
    {
        return (Player)base.GetOwner();
    }

    public override void RemoveAllEffects(bool logout)
    {
        base.RemoveAllEffects(logout);
        if (!logout)
            UpdatePlayerIconsAndGroup(null);
    }

    /// <summary>
    /// Removes non-storable effects and their conditional effects (like Aethertech buffs)
    /// </summary>
    public void RemoveNonStorableEffectsForLogout()
    {
        foreach (Effect e in GetAllEffects().Where(e => !e.CanSaveOnLogout()).ToList())
            e.EndEffect(false);
    }

    private void UpdatePlayerIconsAndGroup(Effect effect)
    {
        if (effect == null || !effect.IsPassive())
        {
            UpdatePlayerEffectIcons(effect);
            int slot = effect == null ? SkillTargetSlot.FULLSLOTS : effect.GetTargetSlot().GetId();
            if (GetOwner().IsInGroup())
            {
                PlayerGroupService.UpdateGroup(GetOwner(), GroupEvent.MOVEMENT);
                PlayerGroupService.UpdateGroupEffects(GetOwner(), slot);
            }
            else if (GetOwner().IsInAlliance())
            {
                PlayerAllianceService.UpdateAlliance(GetOwner(), PlayerAllianceEvent.MOVEMENT);
                PlayerAllianceService.UpdateAllianceEffects(GetOwner(), slot);
            }
        }
    }

    public void UpdatePlayerEffectIcons(Effect effect)
    {
        int slot = effect != null ? effect.GetTargetSlot().GetId() : SkillTargetSlot.FULLSLOTS;
        ICollection<Effect> effects = GetAbnormalEffectsToShow();
        PacketSendUtility.SendPacket(GetOwner(), new SM_ABNORMAL_STATE(effects, GetAbnormals(), slot));
    }

    /// <summary>
    /// Effect of DEBUFF should not be added if duel ended (friendly unit)
    /// </summary>
    private bool CheckDuelCondition(Effect effect)
    {
        return effect.GetTargetSlot() == SkillTargetSlot.DEBUFF && effect.GetEffector() is Player player && !GetOwner().Equals(player) && !GetOwner().IsEnemy(player);
    }

    public void AddSavedEffect(int skillId, int skillLvl, int remainingTime, long endTime, ForceType forceType)
    {
        if (EventService.GetInstance().IsInactiveEventForceType(forceType))
            return;
        SkillTemplate template = DataManager.SKILL_DATA.GetSkillTemplate(skillId);

        if (remainingTime <= 0)
            return;
        if (CustomConfig.ABYSSXFORM_LOGOUT && template.IsDeityAvatar())
        {
            if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= endTime)
                return;
            else
                remainingTime = (int)(endTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        Effect effect = new Effect(GetOwner(), GetOwner(), template, skillLvl, remainingTime, forceType);
        Put(effect);
        effect.AddAllEffectToSucess();
        effect.StartEffect();

        if (effect.GetSkillTemplate().GetTargetSlot() != SkillTargetSlot.NOSHOW)
            PacketSendUtility.SendPacket(GetOwner(), new SM_ABNORMAL_STATE(new List<Effect> { effect }, GetAbnormals(), SkillTargetSlot.FULLSLOTS));
    }

    public override void RemoveAllEffects()
    {
        base.RemoveAllEffects();
        lock (cumulativeResistInfo)
        {
            cumulativeResistInfo.Clear();
        }
    }

    protected override bool CanRemoveOnDie(Effect effect)
    {
        if (!base.CanRemoveOnDie(effect))
            return false;
        if (keepBuffsOnDie)
            return effect.GetTargetSlot() == SkillTargetSlot.DEBUFF;
        return true;
    }

    public long CalculateAndApplyCumulativeResistDuration(CumulativeResistType type, long duration)
    {
        lock (cumulativeResistInfo)
        {
            if (!cumulativeResistInfo.TryGetValue(type, out CumulativeResist cumulativeResist))
            {
                cumulativeResist = new CumulativeResist();
                cumulativeResistInfo[type] = cumulativeResist;
            }
            long cooldownAfterProc = duration + cumulativeResist.GetCooldownTimeOffset(type);
            long finalDuration = (long)(duration * cumulativeResist.GetDurationMultiplier());
            cumulativeResist.TryIncrementLevel(cooldownAfterProc);
            return finalDuration;
        }
    }

    public int GetCumulativeResistance(CumulativeResistType type)
    {
        lock (cumulativeResistInfo)
        {
            CumulativeResist cumulativeResist = cumulativeResistInfo.GetValueOrDefault(type);
            return cumulativeResist == null ? 0 : cumulativeResist.GetResistance();
        }
    }
}
