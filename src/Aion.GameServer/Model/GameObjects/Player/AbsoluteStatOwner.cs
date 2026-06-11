namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>Java parity: model/gameobjects/player/AbsoluteStatOwner implements StatOwner.</summary>
public class AbsoluteStatOwner : Aion.GameServer.Model.Stats.Calc.IStatOwner
{
    internal Player target;
    internal Aion.GameServer.Model.Templates.Stats.ModifiersTemplate template;
    internal bool isActive = false;

    public AbsoluteStatOwner(Player player, int templateId)
    {
        this.target = player;
        SetTemplate(templateId);
    }

    public bool IsActive()
    {
        return isActive;
    }

    public void SetTemplate(int templateId)
    {
        if (isActive)
            Cancel();
        this.template = Aion.GameServer.Dataholders.DataManager.ABSOLUTE_STATS_DATA.GetTemplate(templateId);
    }

    public void Apply()
    {
        if (template == null)
            return;
        target.GetGameStats().AddEffect(this, template.GetModifiers());
        isActive = true;
    }

    public void Cancel()
    {
        if (template == null)
            return;
        target.GetGameStats().EndEffect(this);
        isActive = false;
    }
}
