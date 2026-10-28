namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>
/// Java parity: skillengine/effect/StatdownEffect (ATracer).
/// </summary>
public class StatdownEffect : BufEffect
{
    public override void ApplyEffect(Aion.GameServer.SkillEngine.Model.Effect effect)
    {
        if (!Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpc17860DefenseEffect.IsPreparedForOfflineFixture(effect))
        {
            base.ApplyEffect(effect);
            return;
        }
        if (Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcStatSpellDrain.TryScheduleKasikaDefenseForOfflineFixture(effect,this,
            ()=>ApplyPrepared(effect))) return;
        ApplyPrepared(effect);
    }

    private void ApplyPrepared(Aion.GameServer.SkillEngine.Model.Effect effect)
    {
        // The controller registers before StartEffect. A rejected prepared
        // owner must not leave a registered effect or partial stat functions.
        try { base.ApplyEffect(effect); }
        catch { effect.EndEffect(false); throw; }
    }

    public override void StartEffect(Aion.GameServer.SkillEngine.Model.Effect effect)
    {
        base.StartEffect(effect);
        effect.GetEffected().GetLifeStats().UpdateCurrentStats();
    }

    // TODO bosses are resistent to this?
}
