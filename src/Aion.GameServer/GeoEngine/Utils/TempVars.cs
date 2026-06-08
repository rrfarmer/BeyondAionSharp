using System;
using System.Collections.Generic;
using System.Threading;
using Aion.GameServer.GeoEngine.Collision.Bih;
using Aion.GameServer.GeoEngine.Math;

namespace Aion.GameServer.GeoEngine.Utils;

/// <summary>
/// Temporary variables assigned to each thread. Engine classes may access these temp
/// variables with TempVars.Get(); all retrieved TempVars instances must be returned via
/// Release(). Ensures the instance is never used elsewhere in the meantime.
/// Java parity: geoEngine/utils/TempVars (jMonkeyEngine).
/// </summary>
public class TempVars
{
    /// <summary>Allow X instances of TempVars in a single thread.</summary>
    private const int STACK_SIZE = 5;

    /// <summary>
    /// Contains a stack of TempVars. Every time Get() is called a new entry is added and the
    /// index incremented; Release() checks the entry against the current instance then decrements.
    /// </summary>
    private sealed class TempVarsStack
    {
        public int Index;
        public readonly TempVars[] TempVars = new TempVars[STACK_SIZE];
    }

    /// <summary>
    /// ThreadLocal to store a TempVarsStack for each thread, so each thread has a single
    /// TempVarsStack used only in method calls in that thread.
    /// </summary>
    private static readonly ThreadLocal<TempVarsStack> VarsLocal = new(() => new TempVarsStack());

    /// <summary>This instance of TempVars has been retrieved but not released yet.</summary>
    private bool _isUsed;

    private TempVars()
    {
    }

    /// <summary>
    /// Acquire an instance of the TempVar class. You have to release the instance after use by
    /// calling Release(). If more than STACK_SIZE (5) instances are requested in a single thread
    /// an IndexOutOfRangeException is thrown.
    /// </summary>
    public static TempVars Get()
    {
        TempVarsStack stack = VarsLocal.Value!;

        TempVars instance = stack.TempVars[stack.Index];

        if (instance == null)
        {
            // Create new
            instance = new TempVars();

            // Put it in there
            stack.TempVars[stack.Index] = instance;
        }

        stack.Index++;

        instance._isUsed = true;

        return instance;
    }

    /// <summary>
    /// Releases this instance of TempVars. Must be released in the opposite order that they are
    /// retrieved, otherwise an exception is thrown.
    /// </summary>
    public void Release()
    {
        if (!_isUsed)
        {
            throw new InvalidOperationException("This instance of TempVars was already released!");
        }

        _isUsed = false;

        TempVarsStack stack = VarsLocal.Value!;

        // Return it to the stack
        stack.Index--;

        // Check if it is actually there
        if (stack.TempVars[stack.Index] != this)
        {
            throw new InvalidOperationException("An instance of TempVars has not been released in a called method!");
        }
    }

    /// <summary>General vectors.</summary>
    public readonly Vector3f vect1 = new Vector3f();
    public readonly Vector3f vect2 = new Vector3f();
    public readonly Vector3f vect3 = new Vector3f();
    public readonly Vector3f vect4 = new Vector3f();
    public readonly Vector3f vect5 = new Vector3f();
    public readonly Vector3f vect6 = new Vector3f();
    public readonly Matrix3f tempMat3 = new Matrix3f();

    /// <summary>BoundingBox ray collision</summary>
    public readonly float[] fWdU = new float[3];
    public readonly float[] fAWdU = new float[3];
    public readonly float[] fDdU = new float[3];
    public readonly float[] fADdU = new float[3];
    public readonly float[] fAWxDdU = new float[3];

    /// <summary>BIHTree</summary>
    public readonly byte[] bihSwapTmp = new byte[3];
    public readonly short[] bihSwapTmpShort = new short[3];
    public readonly List<BIHNode.BIHStackData> bihStack = new();
}
