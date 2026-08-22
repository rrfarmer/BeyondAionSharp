using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Registers a <see cref="DataManager"/> carrying the tables that used to be compiled C#.
/// </summary>
/// <remarks>
/// <b>This is the price of moving the tables into data, and it is worth naming.</b> While a table was
/// a dictionary literal, any test could read it with no setup at all. Now it is a file, and a test
/// that asserts on it has to load it.
/// <para>
/// A harness-built <c>DataManager</c> already carries these, so this fills in only for the tests that
/// read a table without building a world. It is deliberately additive: if an instance is already
/// registered it leaves it alone rather than replacing something a harness set up.
/// </para>
/// </remarks>
internal static class StaticTableFixture
{
    private static readonly object Gate = new();

    /// <summary>Loads the static tables into a DataManager, unless one is already registered.</summary>
    internal static void EnsureLoaded()
    {
        lock (Gate)
        {
            if (DataManager.GetRegisteredInstance() is not null)
            {
                return;
            }

            StaticData staticData = (StaticData)RuntimeHelpers.GetUninitializedObject(typeof(StaticData));
            Set(staticData, nameof(StaticData.GuardAnswerDataDh),
                Load<GuardAnswerData>("guard_answers", "guard_answers.xml"));
            Set(staticData, nameof(StaticData.DeathSpawnTableDh),
                Load<PatternTableData>("pattern_tables", "death_spawns.xml"));
            Set(staticData, nameof(StaticData.WakeIdleTableDh),
                Load<PatternTableData>("pattern_tables", "wake_idle_patterns.xml"));

            ConstructorInfo constructor = typeof(DataManager).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, binder: null,
                [typeof(StaticData)], modifiers: null)!;
            DataManager.RegisterInstance((DataManager)constructor.Invoke([staticData]));
        }
    }

    private static T Load<T>(params string[] parts)
        where T : class
    {
        string path = Path.Combine(
            new[] { BossAiHarness.RepoRoot(), "game-server", "data", "static_data" }
                .Concat(parts).ToArray());
        using FileStream stream = File.OpenRead(path);
        T holder = (T)new XmlSerializer(typeof(T)).Deserialize(stream)!;
        typeof(T).GetMethod("AfterUnmarshal")?.Invoke(holder, [null!]);
        return holder;
    }

    private static void Set(StaticData staticData, string property, object value) =>
        typeof(StaticData).GetProperty(property)!.SetValue(staticData, value);
}
