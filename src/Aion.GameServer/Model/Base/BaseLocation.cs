using Aion.GameServer.Model.Templates.Base;

namespace Aion.GameServer.Model.Base;

/// <summary>Java parity: model/base/BaseLocation (Source).</summary>
public class BaseLocation
{
    protected BaseTemplate template;
    protected BaseType type;
    protected BaseOccupier occupier;

    public BaseLocation(BaseTemplate template)
    {
        this.template = template;
        this.type = template.GetType_();
        this.occupier = template.GetDefaultOccupier();
    }

    public int GetId()
    {
        return template.GetId();
    }

    public int GetWorldId()
    {
        return template.GetWorldId();
    }

    // Java parity: getType() — renamed GetType_ (GetType collides with object.GetType()).
    public BaseType GetType_()
    {
        return type;
    }

    public BaseOccupier GetOccupier()
    {
        return occupier;
    }

    public void SetOccupier(BaseOccupier occupier)
    {
        this.occupier = occupier;
    }

    public BaseTemplate GetTemplate()
    {
        return template;
    }
}
