using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.Commons.Scripting;
using Aion.Commons.Scripting.ClassListener;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.World;

namespace Aion.GameServer.Instance;

/// <summary>Java parity: instance/InstanceEngine (ATracer) implements GameEngine. Map&lt;Integer,Class&lt;? extends IInstanceHandler&gt;&gt;→Dictionary&lt;int,Type&gt;; getAnnotation(InstanceID.class)→GetCustomAttribute&lt;InstanceID&gt;(); getDeclaredConstructor(X).newInstance(arg)→Activator.CreateInstance; slf4j→ILogger. GameEngine async-interface satisfaction left red-tolerated (per QuestEngine precedent). ScriptManager red-tolerated (commons compiler pillar).</summary>
public class InstanceEngine : GameEngine
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(InstanceEngine));
    private readonly Dictionary<int, Type> instanceHandlers = new Dictionary<int, Type>();

    public void Init()
    {
        ScriptManager scriptManager = new ScriptManager();
        AggregatedClassListener acl = new AggregatedClassListener();
        acl.AddClassListener(new OnClassLoadUnloadListener());
        acl.AddClassListener(new InstanceHandlerClassListener());
        scriptManager.SetGlobalClassListener(acl);
        scriptManager.Load(InstanceConfig.HANDLER_DIRECTORY);
        log.LogInformation("Loaded " + instanceHandlers.Count + " instance handlers.");
    }

    public IInstanceHandler GetNewInstanceHandler(WorldMapInstance instance)
    {
        Type handlerClass = instanceHandlers.GetValueOrDefault(instance.GetMapId());
        IInstanceHandler instanceHandler = null;
        if (handlerClass != null)
        {
            try
            {
                instanceHandler = (IInstanceHandler)Activator.CreateInstance(handlerClass, instance);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Can't instantiate instance handler for map " + instance.GetMapId() + " (instanceId: " + instance.GetInstanceId() + ')');
            }
        }

        return instanceHandler != null ? instanceHandler : new GeneralInstanceHandler(instance);
    }

    internal void AddInstanceHandlerClass(Type handler)
    {
        InstanceID idAnnotation = handler.GetCustomAttribute<InstanceID>();
        if (idAnnotation != null)
        {
            instanceHandlers[idAnnotation.Value] = handler;
        }
    }

    public static InstanceEngine GetInstance()
    {
        return SingletonHolder.instance;
    }

    private static class SingletonHolder
    {
        internal static readonly InstanceEngine instance = new InstanceEngine();
    }
}
