using Aion.GameServer.Questengine.Handlers;

namespace Aion.GameServer.Questengine.Handlers.Template;

/// <summary>Java parity: questEngine/handlers/template/AbstractTemplateQuestHandler. Base AbstractQuestHandler red until ported (god-class pillar).</summary>
public abstract class AbstractTemplateQuestHandler : AbstractQuestHandler
{
    protected AbstractTemplateQuestHandler(int questId) : base(questId)
    {
    }
}
