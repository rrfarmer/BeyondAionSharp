using System;
using System.Reflection;
using Aion.Commons.Scripting.Metadata;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.Commons.Scripting.Classlistener;

/// <summary>
/// Java parity: commons/scripting/classlistener/OnClassLoadUnloadListener (SoulKeeper).
/// getDeclaredMethods()→GetMethods(DeclaredOnly|Static|Instance|Public|NonPublic); Modifier.isStatic→IsStatic;
/// getAnnotation(annotationClass)→GetCustomAttribute(annotationType); m.invoke(null)→m.Invoke(null,null);
/// IllegalAccessException→MethodAccessException; InvocationTargetException→TargetInvocationException.
/// setAccessible is unnecessary in C# (NonPublic binding flag already permits invocation).
/// </summary>
public class OnClassLoadUnloadListener : ClassListener
{
    private static readonly ILogger log = NullLogger.Instance;

    public void PostLoad(Type[] classes)
    {
        foreach (Type c in classes)
        {
            DoMethodInvoke(c.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), typeof(OnClassLoad));
        }
    }

    public void PreUnload(Type[] classes)
    {
        foreach (Type c in classes)
        {
            DoMethodInvoke(c.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), typeof(OnClassUnload));
        }
    }

    /// <summary>
    /// Actually invokes method where given annotation class is present. Only static methods can be invoked
    /// </summary>
    /// <param name="methods">Methods to scan for annotations</param>
    /// <param name="annotationClass">class of annotation to search for</param>
    protected void DoMethodInvoke(MethodInfo[] methods, Type annotationClass)
    {
        foreach (MethodInfo m in methods)
        {
            if (!m.IsStatic)
                continue;

            if (m.GetCustomAttribute(annotationClass) != null)
            {
                try
                {
                    m.Invoke(null, null);
                }
                catch (MethodAccessException e)
                {
                    log.LogError(e, "Can't access method " + m.Name + " of class " + m.DeclaringType.FullName);
                }
                catch (TargetInvocationException e)
                {
                    log.LogError(e, "Can't invoke method " + m.Name + " of class " + m.DeclaringType.FullName);
                }
            }
        }
    }
}
