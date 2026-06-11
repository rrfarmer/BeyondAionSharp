using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Aion.Commons.Scripting;

// Note: the ClassListener interface lives in the (same-named) child namespace
// Aion.Commons.Scripting.ClassListener, hence the ClassListener.ClassListener qualification below.

/// <summary>
/// Java parity: commons/scripting/ScriptManager (SoulKeeper) — the runtime script compiler/loader every engine
/// (AI/Instance/Quest/Chat/Zone) drives to discover and register its handlers.
///
/// Infrastructure divergence (gameplay-faithful / infra-idiomatic principle): in the C# port the handlers are
/// statically compiled into the assembly rather than compiled from .java sources at runtime, so there is no
/// JavaCompiler/ScriptContext pipeline. Load() instead discovers candidate handler types via reflection over the
/// loaded assemblies and hands them to the global <see cref="ClassListener"/> — which filters by base type/attribute
/// exactly as it does in Java — faithfully reproducing the Java <c>load() -&gt; ClassListener.postLoad()</c> contract
/// without a runtime compiler. The source directories are accepted for call-site parity but not needed for discovery.
/// </summary>
public class ScriptManager
{
    private ClassListener.ClassListener globalClassListener;
    private readonly List<Type[]> loadedContexts = new List<Type[]>();

    /// <summary>Java parity: setGlobalClassListener.</summary>
    public void SetGlobalClassListener(ClassListener.ClassListener classListener)
    {
        globalClassListener = classListener;
    }

    /// <summary>Java parity: load(File... sourceFileRootDirectories).</summary>
    public void Load(params FileInfo[] sourceFileRootDirectories)
    {
        LoadInternal();
    }

    /// <summary>Java parity: load(...) called with string directory paths (the common C# config form).</summary>
    public void Load(params string[] sourceFileRootDirectories)
    {
        LoadInternal();
    }

    private void LoadInternal()
    {
        if (globalClassListener == null)
            return;

        Type[] classes = DiscoverHandlerTypes();
        loadedContexts.Add(classes);
        globalClassListener.PostLoad(classes);
    }

    private static Type[] DiscoverHandlerTypes()
    {
        List<Type> types = new List<Type>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
                continue;

            Type[] assemblyTypes;
            try
            {
                assemblyTypes = assembly.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException e)
            {
                assemblyTypes = e.Types.Where(t => t != null).ToArray();
            }

            foreach (Type t in assemblyTypes)
            {
                // Only concrete, instantiable classes are candidate handler scripts (the ClassListener filters further).
                if (t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters)
                    types.Add(t);
            }
        }
        return types.ToArray();
    }

    /// <summary>Java parity: shutdown — preUnload all loaded contexts.</summary>
    public void Shutdown()
    {
        if (globalClassListener != null)
        {
            foreach (Type[] context in loadedContexts)
                globalClassListener.PreUnload(context);
        }
        loadedContexts.Clear();
    }

    /// <summary>Java parity: reload.</summary>
    public void Reload()
    {
        Shutdown();
    }
}
