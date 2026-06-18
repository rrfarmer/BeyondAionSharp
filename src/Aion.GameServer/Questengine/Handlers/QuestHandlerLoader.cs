using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.Commons.Scripting.ClassListener;

namespace Aion.GameServer.QuestEngine.Handlers;

/// <summary>Java parity: questEngine/handlers/QuestHandlerLoader (MrPoke) implements ClassListener. On postLoad, instantiates and registers every valid (public, concrete) AbstractQuestHandler subclass; on preUnload clears the Aion.GameServer.QuestEngine.QuestEngine. Reflection: Class[]->Type[], getName->FullName, isAssignableFrom->IsAssignableFrom, getDeclaredConstructor().newInstance()->Activator.CreateInstance, Modifier.isX->Type.IsX; RuntimeException->Exception; isDebugEnabled->IsEnabled(LogLevel.Debug). ClassListener/AbstractQuestHandler red-tolerated.</summary>
public class QuestHandlerLoader : ClassListener
{
    private static readonly ILogger logger = NullLoggerFactory.Instance.CreateLogger(nameof(QuestHandlerLoader));

    public QuestHandlerLoader()
    {
    }

    public void PostLoad(Type[] classes)
    {
        foreach (Type c in classes)
        {
            if (c == null)
                continue;
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Load class " + c.FullName);

            if (!IsValidClass(c))
                continue;

            if (typeof(AbstractQuestHandler).IsAssignableFrom(c))
            {
                try
                {
                    Type tmp = c;
                    Aion.GameServer.QuestEngine.QuestEngine.GetInstance().AddQuestHandler((AbstractQuestHandler)Activator.CreateInstance(tmp));
                }
                catch (Exception e)
                {
                    throw new Exception("Failed to load quest handler class: " + c.FullName, e);
                }
            }
        }
    }

    public void PreUnload(Type[] classes)
    {
        if (logger.IsEnabled(LogLevel.Debug))
            foreach (Type c in classes)
                // debug messages
                logger.LogDebug("Unload class " + c.FullName);

        Aion.GameServer.QuestEngine.QuestEngine.GetInstance().Clear();
    }

    public bool IsValidClass(Type clazz)
    {
        if (clazz.IsAbstract || clazz.IsInterface)
            return false;

        if (!clazz.IsPublic)
            return false;

        // Java parity (effective): Java's scriptManager.load() only compiles the data/handlers/quest SCRIPTS, which
        // always expose a public no-arg ctor (Java instantiates via getDeclaredConstructor().newInstance()). The
        // data-driven TEMPLATE handlers (CraftingRewards/MonsterHunt/ItemCollecting/... : AbstractTemplateQuestHandler)
        // have parameterized ctors and are registered separately via their *Data loaders
        // (e.g. CraftingRewardsData -> questEngine.AddQuestHandler(new CraftingRewards(id, ...))). The C# ScriptManager
        // scans the whole assembly (not a per-directory compile), so it also sees those template handlers; skip any
        // AbstractQuestHandler subclass without a public parameterless ctor so we auto-register exactly the script set
        // Java does (the templates come in through the data path, not reflection).
        if (typeof(AbstractQuestHandler).IsAssignableFrom(clazz) && clazz.GetConstructor(Type.EmptyTypes) == null)
            return false;

        return true;
    }
}
