using Aion.GameServer.Model.GameObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Ai;

/// <summary>Java parity: ai/AILogger (ATracer).</summary>
public class AILogger
{
    private static readonly ILogger log = NullLogger.Instance;

    // Java parity: info(AbstractAI<? extends Creature> ai, String) — methods used live on the non-generic base.
    public static void Info(AbstractAI ai, string message)
    {
        if (ai.IsLogging())
        {
            log.LogInformation("[AI] " + ai.GetOwner().GetObjectId() + " - " + message);
        }
    }

    public static void Moveinfo(Creature owner, string message)
    {
        if (Aion.GameServer.Configs.Main.AIConfig.MOVE_DEBUG && owner.GetAi().IsLogging())
        {
            log.LogInformation("[AI] " + owner.GetObjectId() + " - " + message);
        }
    }
}
