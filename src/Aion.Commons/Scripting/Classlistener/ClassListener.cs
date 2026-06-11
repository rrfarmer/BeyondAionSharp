using System;

namespace Aion.Commons.Scripting.ClassListener;

/// <summary>
/// Java parity: commons/scripting/classlistener/ClassListener (SoulKeeper).
/// This interface implements listener that is called post class load/before class unload.
/// Class&lt;?&gt;[]→Type[].
/// </summary>
public interface ClassListener
{
    /// <summary>This method is invoked after classes were loaded.</summary>
    /// <param name="classes">all loaded classes by script context</param>
    void PostLoad(Type[] classes);

    /// <summary>This method is invoked before class unloading. As argument are passes all loaded classes</summary>
    /// <param name="classes">all loaded classes (they are going to be unloaded) by script context</param>
    void PreUnload(Type[] classes);
}
