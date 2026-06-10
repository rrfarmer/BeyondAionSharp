using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.Commons.Scripting.Classlistener;
using Aion.GameServer.Instance.Handlers;

namespace Aion.GameServer.Instance;

/// <summary>Java parity: instance/InstanceHandlerClassListener (ATracer) implements ClassListener. Class&lt;?&gt;[]→Type[]; getName()→FullName; isAssignableFrom→typeof(IInstanceHandler).IsAssignableFrom; Modifier.isAbstract/isInterface/isPublic→Type.IsAbstract/IsInterface/IsPublic; log.isDebugEnabled→IsEnabled(LogLevel.Debug).</summary>
public class InstanceHandlerClassListener : ClassListener
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(InstanceHandlerClassListener));

    public void PostLoad(Type[] classes)
    {
        foreach (Type c in classes)
        {
            if (log.IsEnabled(LogLevel.Debug))
                log.LogDebug("Load class " + c.FullName);

            if (!IsValidClass(c))
                continue;

            if (typeof(IInstanceHandler).IsAssignableFrom(c))
                InstanceEngine.GetInstance().AddInstanceHandlerClass(c);
        }
    }

    public void PreUnload(Type[] classes)
    {
        if (log.IsEnabled(LogLevel.Debug))
        {
            foreach (Type c in classes)
                log.LogDebug("Unload class " + c.FullName);
        }
    }

    public bool IsValidClass(Type clazz)
    {
        // Java parity: Modifier.isAbstract/isInterface/isPublic(clazz.getModifiers()) → Type flags.
        if (clazz.IsAbstract || clazz.IsInterface)
            return false;

        if (!clazz.IsPublic)
            return false;

        return true;
    }
}
