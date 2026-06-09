using System;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates.Siegelocation;
using Aion.GameServer.Services;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Model.Siege;

/// <summary>Java parity: model/siege/ArtifactLocation.</summary>
public class ArtifactLocation : SiegeLocation
{
    private ArtifactStatus? status;
    private long lastArtifactActivation;

    public ArtifactLocation(SiegeLocationTemplate template)
        : base(template)
    {
        // Artifacts Always Vulnerable
        SetVulnerable(true);
    }

    public override int GetNextState()
    {
        return STATE_VULNERABLE;
    }

    public long GetLastActivation()
    {
        return lastArtifactActivation;
    }

    public void SetInitialDelay(long capturedTime)
    {
        long cd = GetTemplate().GetActivation().GetCd();
        lastArtifactActivation = cd > 900000 ? capturedTime - cd + 900000 : capturedTime;
    }

    public void SetLastActivation(long lastActivation)
    {
        lastArtifactActivation = lastActivation;
    }

    public int GetCoolDown()
    {
        long cd = GetTemplate().GetActivation().GetCd();
        long millisSinceLastActivation = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastArtifactActivation;
        if (millisSinceLastActivation > cd)
            return 0;
        else
            return (int) ((cd - millisSinceLastActivation) / 1000);
    }

    public string GetL10n()
    {
        ArtifactActivation activation = GetTemplate().GetActivation();
        SkillTemplate skillTemplate = DataManager.SKILL_DATA.GetSkillTemplate(activation.GetSkillId());
        return skillTemplate.GetL10n();
    }

    public bool IsStandAlone()
    {
        return !SiegeService.GetInstance().GetFortresses().ContainsKey(GetLocationId());
    }

    public FortressLocation GetOwningFortress()
    {
        return SiegeService.GetInstance().GetFortress(GetLocationId());
    }

    public ArtifactStatus GetStatus()
    {
        return status != null ? status.Value : ArtifactStatus.IDLE;
    }

    public void SetStatus(ArtifactStatus status)
    {
        this.status = status;
    }
}
