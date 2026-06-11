using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.Commons.Scripting;
using Aion.Commons.Scripting.ClassListener;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npc;

namespace Aion.GameServer.Ai;

/// <summary>
/// Java parity: ai/AIEngine (ATracer) : GameEngine. AI handler registry + factory. Twin of InstanceEngine/ZoneService (Java-faithful
/// Init()). Map&lt;String, Class&lt;? extends AbstractAI&lt;?&gt;&gt;&gt; -> Dictionary&lt;string, Type&gt;; getAnnotation -> GetCustomAttribute&lt;AIName&gt;;
/// putIfAbsent -> !TryAdd; reflective Constructor/ParameterizedType/TypeVariable introspection -> System.Reflection (ConstructorInfo,
/// BaseType, IsGenericType, GetGenericArguments, GetGenericParameterConstraints). Stream collect/removeAll -> LINQ/ExceptWith.
/// GameServerError red-tolerated where its deps are unported.
/// </summary>
public class AIEngine : GameEngine
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(AIEngine));
    private readonly ScriptManager scriptManager = new ScriptManager();
    private readonly Dictionary<string, Type> aiHandlers = new Dictionary<string, Type>();

    private AIEngine()
    {
    }

    public void Init()
    {
        AggregatedClassListener acl = new AggregatedClassListener();
        acl.AddClassListener(new OnClassLoadUnloadListener());
        acl.AddClassListener(new AIHandlerClassListener());
        scriptManager.SetGlobalClassListener(acl);
        scriptManager.Load(AIConfig.HANDLER_DIRECTORY);
        ValidateScripts();
        log.LogInformation("Loaded " + aiHandlers.Count + " AI handlers.");
    }

    public void Reload()
    {
        scriptManager.Shutdown();
        aiHandlers.Clear();
        Init();
    }

    public void RegisterAI(Type aiClass)
    {
        AIName nameAnnotation = aiClass.GetCustomAttribute<AIName>();
        if (nameAnnotation != null)
        {
            if (!aiHandlers.TryAdd(nameAnnotation.Value, aiClass))
            {
                Type presentClass = aiHandlers[nameAnnotation.Value];
                throw new ArgumentException("Duplicate AIs with name " + nameAnnotation.Value + " (" + aiClass + ", " + presentClass + ")");
            }
        }
    }

    public AbstractAI NewAI<T>(string name, T owner) where T : Creature
    {
        AbstractAI aiInstance;
        if (name == null)
        {
            aiInstance = new DummyAI<T>(owner);
        }
        else
        {
            Type aiClass = aiHandlers.GetValueOrDefault(name);
            if (aiClass == null)
                throw new ArgumentException("No AI found for name " + name);
            ConstructorInfo constructor = FindConstructor(aiClass, owner.GetType(), false);
            if (constructor == null)
                throw new ArgumentException(aiClass + " cannot be instantiated with " + owner.GetType().Name + " as the owner");
            try
            {
                aiInstance = (AbstractAI)constructor.Invoke(new object[] { owner });
            }
            catch (Exception e)
            {
                throw new ArgumentException("Could not instantiate AI for class " + aiClass + " (owner: " + owner + ")", e);
            }
        }
        if (AIConfig.ONCREATE_DEBUG)
            aiInstance.SetLogging(true);
        return aiInstance;
    }

    private ConstructorInfo FindConstructor(Type aiClass, Type searchParamType, bool isSuperType)
    {
        foreach (ConstructorInfo constructor in aiClass.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            ParameterInfo[] parameters = constructor.GetParameters();
            if (parameters.Length == 1)
            {
                Type constructorParamType = parameters[0].ParameterType;
                if (isSuperType)
                {
                    if (searchParamType.IsAssignableFrom(constructorParamType))
                        return constructor;
                }
                else if (constructorParamType.IsAssignableFrom(searchParamType))
                {
                    return constructor;
                }
            }
        }
        return null;
    }

    private void ValidateScripts()
    {
        foreach (Type aiClass in aiHandlers.Values)
        {
            Type ownerType = FindDefaultOwnerType(aiClass);
            if (ownerType == null)
                throw new GameServerError("Faulty AI handler: " + aiClass + " is missing generic owner type info");
            if (FindConstructor(aiClass, ownerType, true) == null)
                throw new GameServerError(
                    "Faulty AI handler: " + aiClass + " is missing constructor taking owner of type " + ownerType + " as the only argument");
        }
        ISet<string> npcAINames = DataManager.NPC_DATA.GetNpcData().Select(t => t.GetAiName()).Where(n => n != null).ToHashSet();
        npcAINames.ExceptWith(aiHandlers.Keys);
        if (npcAINames.Count != 0)
            throw new GameServerError("No AIs could be found for the following npc_template AI names: " + string.Join(", ", npcAINames));
    }

    private Type FindDefaultOwnerType(Type aiClass)
    {
        Type currentClass = aiClass;
        while (currentClass.BaseType != typeof(AbstractAI))
        {
            Type genericSuperClass = currentClass.BaseType;
            if (genericSuperClass != null && genericSuperClass.IsGenericType)
            {
                Type[] actualTypeArguments = genericSuperClass.GetGenericArguments();
                if (actualTypeArguments.Length == 1)
                {
                    Type type = actualTypeArguments[0];
                    if (type.IsGenericParameter)
                    {
                        Type[] bounds = type.GetGenericParameterConstraints();
                        if (bounds.Length > 0)
                            type = bounds[0];
                    }
                    if (!type.IsGenericParameter && typeof(Creature).IsAssignableFrom(type))
                        return type;
                }
            }
            if (genericSuperClass == null)
                break;
            currentClass = genericSuperClass;
        }
        return null;
    }

    public static AIEngine GetInstance()
    {
        return SingletonHolder.instance;
    }

    private static class SingletonHolder
    {
        internal static readonly AIEngine instance = new AIEngine();
    }

    private sealed class DummyAI<T> : AITemplate<T> where T : Creature
    {
        public DummyAI(T owner) : base(owner)
        {
        }
    }
}
