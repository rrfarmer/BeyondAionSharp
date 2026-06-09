using Aion.GameServer.Model.Templates.Assemblednpc;

namespace Aion.GameServer.Model.Assemblednpc;

/// <summary>Java parity: model/assemblednpc/AssembledNpcPart.</summary>
public class AssembledNpcPart
{
    private int? @object;
    private AssembledNpcTemplate.AssembledNpcPartTemplate template;

    public AssembledNpcPart(int? @object, AssembledNpcTemplate.AssembledNpcPartTemplate template)
    {
        this.@object = @object;
        this.template = template;
    }

    public int? GetObject()
    {
        return @object;
    }

    public AssembledNpcTemplate.AssembledNpcPartTemplate GetAssembledNpcPartTemplate()
    {
        return template;
    }

    public int GetNpcId()
    {
        return template.GetNpcId();
    }

    public int GetStaticId()
    {
        return template.GetStaticId();
    }
}
