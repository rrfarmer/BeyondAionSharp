using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/drakenspire/Lv2HumanBeritraAI (Estrayl).</summary>
[AIName("drakenspire_lv2_human_beritra")]
public class Lv2HumanBeritraAI : Lv1HumanBeritraAI
{
    private readonly AtomicInteger sealsBlasted = new AtomicInteger();

    public Lv2HumanBeritraAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleFightStarted()
    {
        // Don't do anything here.
    }

    protected virtual void HandleThirdPhaseStarted()
    {
        if (GetEffectController().FindBySkillId(21612) != null)
            SpawnFactionHelpers(GetAttackingPlayerRace() == Race.ELYOS ? new List<int> { 209734, 209735, 209735 } : new List<int> { 209799, 209800, 209800 });
    }

    protected virtual void HandleLastSealBlasted()
    {
        // Don't do anything here.
    }

    public override void OnEffectApplied(Effect effect)
    {
        if (effect.GetSkillId() == 21624) // Dragon Lord Seal
        {
            switch (sealsBlasted.IncrementAndGet())
            {
                case 1:
                    PacketSendUtility.BroadcastMessage(GetOwner(), 1501272); // You insects think you have a chance against me?
                    SpawnPustules();
                    break;
                case 2:
                    PacketSendUtility.BroadcastMessage(GetOwner(), 1501271); // Fregion! You left me to clean up your mess...
                    SpawnPustules();
                    break;
                case 3:
                    HandleLastSealBlasted();
                    break;
            }
        }
        base.OnEffectApplied(effect);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 21609 && skillLevel == 58)
            HandleThirdPhaseStarted();
        base.OnEndUseSkill(skillTemplate, skillLevel);
    }

    private void SpawnPustules()
    {
        Spawn(855446, 135.192f, 502.221f, 1749.448f, (sbyte)0); // Drakenspire Pustule
        Spawn(855446, 167.133f, 502.114f, 1749.448f, (sbyte)30); // Drakenspire Pustule
        Spawn(855446, 161.501f, 535.842f, 1749.367f, (sbyte)90); // Drakenspire Pustule
    }

    protected void ResetBlastedSeal()
    {
        sealsBlasted.Set(0);
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        ResetBlastedSeal();
    }
}
