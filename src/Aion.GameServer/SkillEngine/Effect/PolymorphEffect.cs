using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/PolymorphEffect (ATracer, Cheatkiller) : TransformEffect. super.startEffect→base.StartEffect; DataManager.NPC_DATA.getNpcTemplate(model); transformModel.setTribe(template.getTribe()) / setTribe(null). NpcTemplate/transformModel red-tolerated.</summary>
[XmlType("PolymorphEffect")]
public class PolymorphEffect : TransformEffect
{
    public override void StartEffect(Effect effect)
    {
        base.StartEffect(effect);
        if (model > 0)
        {
            Creature effected = effect.GetEffected();
            NpcTemplate template = DataManager.NPC_DATA.GetNpcTemplate(model);
            if (template != null)
                effected.GetTransformModel().SetTribe(template.GetTribe());
        }
    }

    public override void EndEffect(Effect effect)
    {
        base.EndEffect(effect);
        effect.GetEffected().GetTransformModel().SetTribe(null);
    }
}
