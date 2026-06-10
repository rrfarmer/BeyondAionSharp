using System;
using Aion.Commons.Scripting.Classlistener;
using Aion.GameServer.World.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.World.Zone.Handler;

/// <summary>Java parity: world/zone/handler/ZoneHandlerClassListener (MrPoke) : ClassListener. Class&lt;?&gt;[]→Type[]; ZoneHandler.class.isAssignableFrom→typeof(IZoneHandler).IsAssignableFrom; Modifier.isAbstract/isInterface/isPublic→IsAbstract/IsInterface/IsPublic; getName→FullName. ZoneService red-tolerated (logger uses InstanceHandlerClassListener.class per Java).</summary>
public class ZoneHandlerClassListener : ClassListener
{
    private static readonly ILogger log = NullLogger.Instance;

    public void PostLoad(Type[] classes)
    {
        foreach (Type c in classes)
        {
            if (log.IsEnabled(LogLevel.Debug))
                log.LogDebug("Load class " + c.FullName);

            if (!IsValidClass(c))
                continue;

            if (typeof(IZoneHandler).IsAssignableFrom(c))
            {
                ZoneService.GetInstance().AddZoneHandlerClass(c);
            }
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
        if (clazz.IsAbstract || clazz.IsInterface)
            return false;

        if (!clazz.IsPublic)
            return false;

        return true;
    }
}
