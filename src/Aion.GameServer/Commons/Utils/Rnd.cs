using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Commons.Utils;

/// <summary>
/// Random utilities.
/// Java parity: commons/utils/Rnd (Balancer, Neon).
/// Java backs each thread with its own split-off RandomGenerator; here we use a
/// thread-local <see cref="Random"/> so generation is thread-safe and independent per thread.
/// </summary>
public static class Rnd
{
    private static readonly ILogger Log = NullLogger.Instance;

    // not thread-safe in Java; here each thread gets its own seeded generator (parity intent: per-thread isolation)
    private static readonly ThreadLocal<Random> RndLocal = new(() => new Random());

    private static Random Generator => RndLocal.Value!;

    /// <summary>
    /// To compare this chance with a success rate, evaluate "<c>if (Chance() &lt; success rate)</c>" to determine a success or, alternatively
    /// "<c>if (Chance() &gt;= success rate)</c>" to determine a fail. This ensures that a success rate of 0 (0%) will always fail, and a success rate of
    /// 100.0 (100%) always succeeds.
    /// </summary>
    /// <returns>A random chance between 0.0f (inclusive) and 100.0f (exclusive)</returns>
    public static float Chance() => NextFloat(100f);

    /// <summary>
    /// Java parity: get(int minInclusive, int maxInclusive).
    /// </summary>
    /// <returns>A random number between minInclusive and maxInclusive</returns>
    public static int Get(int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive)
        {
            Log.LogWarning(new ArgumentException("max < min"), "");
            maxInclusive = minInclusive;
        }
        return minInclusive == maxInclusive ? minInclusive : (int)NextLong(minInclusive, maxInclusive + 1L);
    }

    /// <summary>
    /// Java parity: &lt;T&gt; get(List&lt;T&gt; list).
    /// </summary>
    /// <returns>A random element from the given list, default if it's empty</returns>
    public static T? Get<T>(IReadOnlyList<T> list) =>
        list.Count == 0 ? default : list.Count == 1 ? list[0] : list[NextInt(list.Count)];

    /// <summary>
    /// Java parity: &lt;T&gt; get(T[] array).
    /// </summary>
    /// <returns>A random element from the given array, default if it's empty</returns>
    public static T? Get<T>(T[] array) =>
        array.Length == 0 ? default : array.Length == 1 ? array[0] : array[NextInt(array.Length)];

    /// <summary>
    /// Java parity: get(int[] array).
    /// </summary>
    /// <returns>A random element from the given primitive int array (must not be empty)</returns>
    public static int Get(int[] array)
    {
        if (array.Length == 0)
            throw new ArgumentException("Cannot get random int from an empty array.");
        return array.Length == 1 ? array[0] : array[NextInt(array.Length)];
    }

    // Java parity: doubles()
    public static IEnumerable<double> Doubles()
    {
        while (true)
            yield return Generator.NextDouble();
    }

    // Java parity: doubles(double randomNumberOrigin, double randomNumberBound)
    public static IEnumerable<double> Doubles(double randomNumberOrigin, double randomNumberBound)
    {
        while (true)
            yield return NextDouble(randomNumberOrigin, randomNumberBound);
    }

    // Java parity: ints()
    public static IEnumerable<int> Ints()
    {
        while (true)
            yield return NextInt();
    }

    // Java parity: ints(int randomNumberOrigin, int randomNumberBound)
    public static IEnumerable<int> Ints(int randomNumberOrigin, int randomNumberBound)
    {
        while (true)
            yield return NextInt(randomNumberOrigin, randomNumberBound);
    }

    // Java parity: longs()
    public static IEnumerable<long> Longs()
    {
        while (true)
            yield return NextLong();
    }

    // Java parity: longs(long randomNumberOrigin, long randomNumberBound)
    public static IEnumerable<long> Longs(long randomNumberOrigin, long randomNumberBound)
    {
        while (true)
            yield return NextLong(randomNumberOrigin, randomNumberBound);
    }

    // Java parity: nextBytes(byte[] bytes)
    public static void NextBytes(byte[] bytes) => Generator.NextBytes(bytes);

    // Java parity: nextInt()
    public static int NextInt() => (int)Generator.NextInt64(int.MinValue, (long)int.MaxValue + 1L);

    // Java parity: nextInt(int bound)
    public static int NextInt(int bound) => Generator.Next(bound);

    // Java parity: nextInt(int origin, int bound)
    public static int NextInt(int origin, int bound) => Generator.Next(origin, bound);

    // Java parity: nextLong()
    public static long NextLong() => Generator.NextInt64();

    // Java parity: nextLong(long bound)
    public static long NextLong(long bound) => Generator.NextInt64(bound);

    // Java parity: nextLong(long origin, long bound)
    public static long NextLong(long origin, long bound) => Generator.NextInt64(origin, bound);

    // Java parity: nextFloat()
    public static float NextFloat() => Generator.NextSingle();

    // Java parity: nextFloat(float bound)
    public static float NextFloat(float bound) => Generator.NextSingle() * bound;

    // Java parity: nextFloat(float origin, float bound)
    public static float NextFloat(float origin, float bound) => origin + Generator.NextSingle() * (bound - origin);

    // Java parity: nextDouble()
    public static double NextDouble() => Generator.NextDouble();

    // Java parity: nextDouble(double bound)
    public static double NextDouble(double bound) => Generator.NextDouble() * bound;

    // Java parity: nextDouble(double origin, double bound)
    public static double NextDouble(double origin, double bound) => origin + Generator.NextDouble() * (bound - origin);

    // Java parity: nextBoolean()
    public static bool NextBoolean() => Generator.Next(2) != 0;
}
