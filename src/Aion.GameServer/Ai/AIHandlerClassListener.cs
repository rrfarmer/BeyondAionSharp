using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.Commons.Scripting.ClassListener;

namespace Aion.GameServer.Ai;

/// <summary>
/// Java parity: ai/AIHandlerClassListener (ATracer) : ClassListener. Registers concrete AbstractAI subclasses with AIEngine on script
/// load. Class&lt;?&gt;[] -> Type[]; Modifier.isAbstract/isInterface/isPublic -> Type.IsAbstract/IsInterface/IsPublic;
/// AbstractAI.class.isAssignableFrom -> typeof(AbstractAI).IsAssignableFrom; getName -> FullName. AIEngine.RegisterAI red-tolerated.
/// </summary>
public class AIHandlerClassListener : ClassListener
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(AIHandlerClassListener));

    public void PostLoad(Type[] classes)
    {
        foreach (Type c in classes)
        {
            log.LogDebug("Load class " + c.FullName);

            if (!IsValidClass(c))
                continue;

            if (typeof(AbstractAI).IsAssignableFrom(c))
                AIEngine.GetInstance().RegisterAI(c);
        }
    }

    public void PreUnload(Type[] classes)
    {
        foreach (Type c in classes)
            log.LogDebug("Unload class " + c.FullName);
    }

    public bool IsValidClass(Type clazz)
    {
        if (clazz.IsAbstract || clazz.IsInterface)
            return false;

        if (!clazz.IsPublic)
            return false;

        return true;
    }
}
