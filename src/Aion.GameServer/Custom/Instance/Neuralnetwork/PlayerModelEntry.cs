using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Skillengine.Effects;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Custom.Instance.Neuralnetwork;

/// <summary>Java parity: custom/instance/neuralnetwork/PlayerModelEntry (Jo) implements Persistable. java.sql.Timestamp→DateTimeOffset (new Timestamp(currentTimeMillis())→FromUnixTimeMilliseconds(UtcNow.ToUnixTimeMilliseconds()) — ms precision); instanceof Player→is Player; stream().mapToDouble(Double::doubleValue).toArray()→List.ToArray(); skillSet.stream().mapToDouble(...).toArray()→Select(...).ToArray(); PersistentState→IPersistable.PersistentState. Creature/skillengine/PositionUtil red-tolerated.</summary>
public class PlayerModelEntry : IPersistable
{
    private IPersistable.PersistentState persistentState;

    private DateTimeOffset timestamp;
    private float timeCDdone;
    private int skillID; // used for output (binary array)
    private int playerID; // used for selection
    private int playerClassID;
    private float playerHPpercentage, playerMPpercentage; // used for input

    private bool playerIsRooted, playerIsSilenced, playerIsBound, playerIsStunned, playerIsAetherhold; // used for input
    private int playerBuffCount; // used for input
    private int playerDebuffCount; // used for input
    private bool playerIsShielded; // used for input

    private float targetHPpercentage, targetMPpercentage; // used for input
    private bool targetFocusesPlayer; // used for input
    private float distance; // used for input
    private bool targetIsRooted, targetIsSilenced, targetIsBound, targetIsStunned, targetIsAetherhold; // used for input
    private int targetBuffCount, targetDebuffCount; // used for input
    private bool targetIsShielded; // used for input

    // live constructor
    public PlayerModelEntry(Creature playerOrBoss, int skillID, Creature target)
    {
        timestamp = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        this.skillID = skillID;
        playerID = playerOrBoss.GetObjectId();
        playerClassID = playerOrBoss is Player ? ((Player)playerOrBoss).GetPlayerClass().GetClassId() : -1;
        playerHPpercentage = playerOrBoss.GetLifeStats().GetHpPercentage();
        playerMPpercentage = playerOrBoss.GetLifeStats().GetMpPercentage();

        playerIsRooted = playerOrBoss.GetEffectController().IsAbnormalSet(AbnormalState.ROOT);
        playerIsSilenced = playerOrBoss.GetEffectController().IsAbnormalSet(AbnormalState.SILENCE);
        playerIsBound = playerOrBoss.GetEffectController().IsAbnormalSet(AbnormalState.BIND);
        playerIsStunned = playerOrBoss.GetEffectController().IsAbnormalSet(AbnormalState.ANY_STUN);
        playerIsAetherhold = playerOrBoss.GetEffectController().IsAbnormalSet(AbnormalState.OPENAERIAL);
        playerIsShielded = playerOrBoss.GetEffectController().IsUnderNormalShield();

        playerBuffCount = 0;
        playerDebuffCount = 0;
        foreach (Effect e in playerOrBoss.GetEffectController().GetAbnormalEffects())
        {
            if ((e.GetSkillTemplate() != null && e.GetSkillTemplate().GetSubType() == SkillSubType.BUFF) || // buff skills
                (e.GetSkill() != null && e.GetSkill().GetItemObjectId() != 0)) // buff items
                playerBuffCount++;
            else // debuffs
                playerDebuffCount++;
        }
        if (target != null)
        {
            if (target.GetLifeStats().GetMaxHp() > 0)
                targetHPpercentage = target.GetLifeStats().GetHpPercentage();
            else
                targetHPpercentage = 0;
            if (target.GetLifeStats().GetMaxMp() > 0)
                targetMPpercentage = target.GetLifeStats().GetMpPercentage();
            else
                targetMPpercentage = 0;
            targetFocusesPlayer = target.GetTarget() == playerOrBoss;
            distance = (float)PositionUtil.GetDistance(playerOrBoss, target);
            targetIsRooted = target.GetEffectController().IsAbnormalSet(AbnormalState.ROOT);
            targetIsSilenced = target.GetEffectController().IsAbnormalSet(AbnormalState.SILENCE);
            targetIsBound = target.GetEffectController().IsAbnormalSet(AbnormalState.BIND);
            targetIsStunned = target.GetEffectController().IsAbnormalSet(AbnormalState.ANY_STUN);
            targetIsAetherhold = target.GetEffectController().IsAbnormalSet(AbnormalState.OPENAERIAL);
            targetIsShielded = target.GetEffectController().IsUnderNormalShield();

            targetBuffCount = 0;
            targetDebuffCount = 0;
            foreach (Effect e in target.GetEffectController().GetAbnormalEffects())
                if ((e.GetSkillTemplate() != null && e.GetSkillTemplate().GetSubType() == SkillSubType.BUFF) || // buff skills
                    (e.GetSkill() != null && e.GetSkill().GetItemObjectId() != 0)) // buff items
                    targetBuffCount++;
                else // debuffs
                    targetDebuffCount++;
        }
        else
        {
            targetBuffCount = targetDebuffCount = -1;
            targetHPpercentage = distance = targetMPpercentage = -1;
            targetFocusesPlayer = targetIsRooted = targetIsSilenced = targetIsBound = targetIsStunned = targetIsAetherhold = targetIsShielded = false;
        }
        SetPersistentState(IPersistable.PersistentState.NEW);
    }

    // constructor from persistence
    public PlayerModelEntry(int playerID, DateTimeOffset timestamp, int skillID, int playerClassID, float playerHPpercentage, float playerMPpercentage,
        bool playerIsRooted, bool playerIsSilenced, bool playerIsBound, bool playerIsStunned, bool playerIsAetherhold, int playerBuffCount,
        int playerDebuffCount, bool playerIsShielded, float targetHPpercentage, float targetMPpercentage, bool targetFocusesPlayer, float distance,
        bool targetIsRooted, bool targetIsSilenced, bool targetIsBound, bool targetIsStunned, bool targetIsAetherhold, int targetBuffCount,
        int targetDebuffCount, bool targetIsShielded)
    {
        this.timestamp = timestamp;
        this.skillID = skillID;
        this.playerID = playerID;
        this.playerClassID = playerClassID;
        this.playerHPpercentage = playerHPpercentage;
        this.playerMPpercentage = playerMPpercentage;
        this.playerIsRooted = playerIsRooted;
        this.playerIsSilenced = playerIsSilenced;
        this.playerIsBound = playerIsBound;
        this.playerIsStunned = playerIsStunned;
        this.playerIsAetherhold = playerIsAetherhold;
        this.playerBuffCount = playerBuffCount;
        this.playerDebuffCount = playerDebuffCount;
        this.playerIsShielded = playerIsShielded;
        this.targetHPpercentage = targetHPpercentage;
        this.targetMPpercentage = targetMPpercentage;
        this.targetFocusesPlayer = targetFocusesPlayer;
        this.distance = distance;
        this.targetIsRooted = targetIsRooted;
        this.targetIsSilenced = targetIsSilenced;
        this.targetIsBound = targetIsBound;
        this.targetIsStunned = targetIsStunned;
        this.targetIsAetherhold = targetIsAetherhold;
        this.targetBuffCount = targetBuffCount;
        this.targetDebuffCount = targetDebuffCount;
        this.targetIsShielded = targetIsShielded;
        SetPersistentState(IPersistable.PersistentState.UPDATED);
    }

    public double[] ToStateInputArray(List<int> skillSet, int previousSkillID)
    {
        List<double> input = new List<double>();
        input.Add((double)playerHPpercentage);
        input.Add((double)playerMPpercentage);
        input.Add((double)(playerIsRooted ? 1 : 0));
        input.Add((double)(playerIsSilenced ? 1 : 0));
        input.Add((double)(playerIsBound ? 1 : 0));
        input.Add((double)(playerIsStunned ? 1 : 0));
        input.Add((double)(playerIsAetherhold ? 1 : 0));
        input.Add((double)playerBuffCount);
        input.Add((double)playerDebuffCount);
        input.Add((double)(playerIsShielded ? 1 : 0));

        input.Add((double)targetHPpercentage);
        input.Add((double)targetMPpercentage);
        input.Add((double)(targetFocusesPlayer ? 1 : 0));
        input.Add((double)distance);
        input.Add((double)(targetIsRooted ? 1 : 0));
        input.Add((double)(targetIsSilenced ? 1 : 0));
        input.Add((double)(targetIsBound ? 1 : 0));
        input.Add((double)(targetIsStunned ? 1 : 0));
        input.Add((double)(targetIsAetherhold ? 1 : 0));
        input.Add((double)targetBuffCount);
        input.Add((double)targetDebuffCount);
        input.Add((double)(targetIsShielded ? 1 : 0));

        foreach (int skillID in skillSet)
            input.Add((double)(skillID == previousSkillID ? 1 : 0));

        return input.ToArray();
    }

    public double[] ToActionOutputArray(List<int> skillSet)
    {
        return skillSet.Select(skillId => (double)(skillId == this.skillID ? 1 : 0)).ToArray();
    }

    public DateTimeOffset GetTimestamp()
    {
        return timestamp;
    }

    public float GetTimeCDdone()
    {
        return timeCDdone;
    }

    public int GetSkillID()
    {
        return skillID;
    }

    public int GetPlayerID()
    {
        return playerID;
    }

    public int GetPlayerClassID()
    {
        return playerClassID;
    }

    public float GetPlayerHPpercentage()
    {
        return playerHPpercentage;
    }

    public float GetPlayerMPpercentage()
    {
        return playerMPpercentage;
    }

    public bool IsPlayerRooted()
    {
        return playerIsRooted;
    }

    public bool IsPlayerSilenced()
    {
        return playerIsSilenced;
    }

    public bool IsPlayerBound()
    {
        return playerIsBound;
    }

    public bool IsPlayerStunned()
    {
        return playerIsStunned;
    }

    public bool IsPlayerAetherhold()
    {
        return playerIsAetherhold;
    }

    public int GetPlayerBuffCount()
    {
        return playerBuffCount;
    }

    public int GetPlayerDebuffCount()
    {
        return playerDebuffCount;
    }

    public bool IsPlayerIsShielded()
    {
        return playerIsShielded;
    }

    public float GetTargetHPpercentage()
    {
        return targetHPpercentage;
    }

    public float GetTargetMPpercentage()
    {
        return targetMPpercentage;
    }

    public bool IsTargetFocusesPlayer()
    {
        return targetFocusesPlayer;
    }

    public float GetDistance()
    {
        return distance;
    }

    public bool IsTargetRooted()
    {
        return targetIsRooted;
    }

    public bool IsTargetSilenced()
    {
        return targetIsSilenced;
    }

    public bool IsTargetBound()
    {
        return targetIsBound;
    }

    public bool IsTargetStunned()
    {
        return targetIsStunned;
    }

    public bool IsTargetAetherhold()
    {
        return targetIsAetherhold;
    }

    public int GetTargetBuffCount()
    {
        return targetBuffCount;
    }

    public int GetTargetDebuffCount()
    {
        return targetDebuffCount;
    }

    public bool IsTargetIsShielded()
    {
        return targetIsShielded;
    }

    public IPersistable.PersistentState GetPersistentState()
    {
        return persistentState;
    }

    public void SetPersistentState(IPersistable.PersistentState state)
    {
        persistentState = state;
    }
}
