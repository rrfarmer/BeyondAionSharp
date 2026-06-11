using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Ai;

/// <summary>Java parity: ai/AI (@author ATracer).</summary>
public interface AI
{
    void OnCreatureEvent(AiEventType @event, Creature creature);

    void OnCustomEvent(int eventId, params object[] args);

    void OnGeneralEvent(AiEventType @event);

    /// <summary>If already handled dialog return true.</summary>
    bool OnDialogSelect(Player.Player player, int dialogActionId, int questId, int extendedRewardIndex);

    void Think();

    bool CanThink();

    AiState GetState();

    AiSubState GetSubState();

    string GetName();

    /// <summary>Ask AI instance for the answer to the specified question. Returns the answer, true or false.</summary>
    bool Ask(AIQuestion question);

    bool IsLogging();

    /// <summary>Returns the effectively received damage. <paramref name="effect"/> may be null.</summary>
    float ModifyDamage(Creature attacker, float damage, Effect effect);

    /// <summary>Returns the effective damage output of this creature.</summary>
    float ModifyOwnerDamage(float damage, Creature effected, Effect effect);

    /// <summary>Used to manipulate any game stat of the owner.</summary>
    void ModifyOwnerStat(Stat2 stat);

    Aion.GameServer.Model.Templates.Items.ItemAttackType ModifyAttackType(Aion.GameServer.Model.Templates.Items.ItemAttackType type);

    int ModifyAggroRange(int value);

    int ModifyAggroAngle(int value);

    void OnStartUseSkill(SkillTemplate skillTemplate, int skillLevel);

    void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel);

    void OnEffectApplied(Effect effect);

    void OnEffectEnd(Effect effect);

    Aion.GameServer.Model.Animations.AttackHandAnimation ModifyAttackHandAnimation(Aion.GameServer.Model.Animations.AttackHandAnimation attackHandAnimation);

    Aion.GameServer.Model.Animations.AttackTypeAnimation GetAttackTypeAnimation(Creature target);

    int ModifyInitialSkillDelay(int delay);

    bool IsDestinationReached();
}
