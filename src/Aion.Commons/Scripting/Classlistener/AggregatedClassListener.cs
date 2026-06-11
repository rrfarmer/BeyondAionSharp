using System;
using System.Collections.Generic;

namespace Aion.Commons.Scripting.ClassListener;

/// <summary>
/// Java parity: commons/scripting/classlistener/AggregatedClassListener (SoulKeeper).
/// ClassListener that aggregates a collection of ClassListeners.
/// Please note that "shutdown" listeners will be executed in reverse order.
/// </summary>
public class AggregatedClassListener : ClassListener
{
    private readonly List<ClassListener> classListeners;

    public AggregatedClassListener()
    {
        classListeners = new List<ClassListener>();
    }

    public AggregatedClassListener(List<ClassListener> classListeners)
    {
        this.classListeners = classListeners;
    }

    public List<ClassListener> GetClassListeners()
    {
        return classListeners;
    }

    public void AddClassListener(ClassListener cl)
    {
        classListeners.Add(cl);
    }

    public void PostLoad(Type[] classes)
    {
        foreach (ClassListener cl in classListeners)
        {
            cl.PostLoad(classes);
        }
    }

    public void PreUnload(Type[] classes)
    {
        for (int i = classListeners.Count - 1; i >= 0; i--)
        {
            classListeners[i].PreUnload(classes);
        }
    }
}
