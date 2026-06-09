using Aion.GameServer.Model.Templates.Base;

namespace Aion.GameServer.Model.Base;

/// <summary>Java parity: model/base/StainedBaseLocation (Estrayl).</summary>
public class StainedBaseLocation : BaseLocation
{
    private readonly BaseColorType color;

    public StainedBaseLocation(BaseTemplate template)
        : base(template)
    {
        this.color = template.GetColor();
    }

    public BaseColorType GetColor()
    {
        return color;
    }
}
