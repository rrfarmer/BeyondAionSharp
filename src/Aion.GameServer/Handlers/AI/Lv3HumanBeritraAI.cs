using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/drakenspire/Lv3HumanBeritraAI (Estrayl).</summary>
[AIName("drakenspire_lv3_human_beritra")]
public class Lv3HumanBeritraAI : Lv2HumanBeritraAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(40);

    public Lv3HumanBeritraAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleLastSealBlasted()
    {
        GetOwner().QueueSkill(20842, 56);
    }

    /// <summary>Can only happen if the fight takes longer than 11 minutes or HP reaches below 36%.</summary>
    protected override void HandleThirdPhaseStarted()
    {
        if (GetEffectController().FindBySkillId(21611) != null)
            SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(21611, GetOwner(), GetOwner());
        if (GetEffectController().FindBySkillId(21610) != null)
            SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(21610, GetOwner(), GetOwner());
        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDSEAL_VRITRA_HUMAN_03());
        GetLifeStats().SetCurrentHp(GetLifeStats().GetMaxHp());
        GetOwner().GetGameStats().SetFightStartingTime();
        ResetBlastedSeal();
    }

    protected override void HandleAttack(Creature creature)
    {
        hpPhases.TryEnterNextPhase(this);
        base.HandleAttack(creature);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 20842)
        {
            // Retail would apply this through activate_skillarea
            GetPosition().GetWorldMapInstance().ForEachPlayer(p => SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(20842, GetOwner(), p));
            HandleTransformation();
        }
    }

    private void HandleTransformation()
    {
        foreach (Npc npc in GetPosition().GetWorldMapInstance().GetNpcs(702695, 702696))
            npc.GetController().Die(); // Collapse ceiling and ambient light
        Spawn(236247, 123.055f, 519.190f, 1750.3414f, (sbyte)0); // Dragon Form
        AIActions.DeleteOwner(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        HandleLastSealBlasted(); // Using the same logic here
    }
}
