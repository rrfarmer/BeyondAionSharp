using System.IO;
using Aion.Commons.Configs;
using Aion.Commons.Configuration;

namespace Aion.Commons.Tests;

/// <summary>
/// Java parity: commons/configuration/ConfigurableProcessor + Property + transformers. Proves that an operator
/// .properties override actually replaces the [Property] default (the hard-contract gap that was previously TODO'd
/// in Config.cs), and that the type coercion / sentinel / placeholder semantics match Java.
/// </summary>
public class ConfigurableProcessorParityTests
{
    private static class Holder
    {
        [Property(key: "test.bool", defaultValue: "false")]
        public static bool Flag;

        [Property(key: "test.int", defaultValue: "7")]
        public static int Number;

        [Property(key: "test.long", defaultValue: "0")]
        public static long Big;

        [Property(key: "test.float", defaultValue: "1.5")]
        public static float Ratio;

        [Property(key: "test.str", defaultValue: "hello")]
        public static string Text = "";

        [Property(key: "test.mode", defaultValue: "FILE")]
        public static Source Mode;

        [Property(key: "test.arr", defaultValue: "1,2,3")]
        public static int[] Numbers = System.Array.Empty<int>();

        // sentinel: absent key + DO_NOT_OVERWRITE -> keeps initializer
        [Property(key: "test.untouched")]
        public static int Untouched = 999;

        [Property(key: "test.list", defaultValue: "a, b, c")]
        public static System.Collections.Generic.List<string> Words
            = new System.Collections.Generic.List<string>();

        [Property(key: "test.set", defaultValue: "x, y")]
        public static System.Collections.Generic.ISet<string> Tags
            = new System.Collections.Generic.HashSet<string>();

        public enum Source { FILE, URL }
    }

    [Fact]
    public void CollectionTransformer_BindsListAndSet_FromDefaultsAndOverrides()
    {
        // Defaults from the annotation.
        ConfigurableProcessor.Process(new JavaProperties(), typeof(Holder));
        Assert.Equal(new System.Collections.Generic.List<string> { "a", "b", "c" }, Holder.Words);
        Assert.Equal(new System.Collections.Generic.HashSet<string> { "x", "y" }, Holder.Tags);

        // Overrides: List preserves order + quoted comma; Set de-duplicates (HashSet parity).
        var props = new JavaProperties();
        props.SetProperty("test.list", "one, \"two,three\", four");
        props.SetProperty("test.set", "p, q, p, r");
        ConfigurableProcessor.Process(props, typeof(Holder));

        Assert.Equal(new System.Collections.Generic.List<string> { "one", "two,three", "four" }, Holder.Words);
        Assert.Equal(new System.Collections.Generic.HashSet<string> { "p", "q", "r" }, Holder.Tags);

        // Empty value -> empty collection (never null), matching Java.
        var empty = new JavaProperties();
        empty.SetProperty("test.list", "");
        empty.SetProperty("test.set", "");
        ConfigurableProcessor.Process(empty, typeof(Holder));
        Assert.Empty(Holder.Words);
        Assert.Empty(Holder.Tags);
    }

    [Fact]
    public void Defaults_AreParsedFromAnnotation_WhenKeyAbsent()
    {
        var props = new JavaProperties();
        ConfigurableProcessor.Process(props, typeof(Holder));

        Assert.False(Holder.Flag);
        Assert.Equal(7, Holder.Number);
        Assert.Equal(1.5f, Holder.Ratio);
        Assert.Equal("hello", Holder.Text);
        Assert.Equal(Holder.Source.FILE, Holder.Mode);
        Assert.Equal(new[] { 1, 2, 3 }, Holder.Numbers);
        Assert.Equal(999, Holder.Untouched); // sentinel: initializer preserved
    }

    [Fact]
    public void PropertiesOverride_ActuallyOverridesDefault()
    {
        var props = new JavaProperties();
        props.SetProperty("test.bool", "1");        // boolean coercion 1 -> true
        props.SetProperty("test.int", "42");
        props.SetProperty("test.long", "0xFF");     // Java Long.decode hex
        props.SetProperty("test.float", "3.25");
        props.SetProperty("test.str", "overridden");
        props.SetProperty("test.mode", "URL");
        props.SetProperty("test.arr", "10, 20 ,30");
        props.SetProperty("test.untouched", "5");

        ConfigurableProcessor.Process(props, typeof(Holder));

        Assert.True(Holder.Flag);
        Assert.Equal(42, Holder.Number);
        Assert.Equal(255L, Holder.Big);
        Assert.Equal(3.25f, Holder.Ratio);
        Assert.Equal("overridden", Holder.Text);
        Assert.Equal(Holder.Source.URL, Holder.Mode);
        Assert.Equal(new[] { 10, 20, 30 }, Holder.Numbers);
        Assert.Equal(5, Holder.Untouched); // explicit key overrides even the sentinel
    }

    [Fact]
    public void CommonsConfig_OverrideFromLoadedProperties()
    {
        // baseline = annotated defaults
        ConfigurableProcessor.Process(new JavaProperties(), typeof(CommonsConfig));
        Assert.False(CommonsConfig.RUNNABLESTATS_ENABLE);
        Assert.True(CommonsConfig.SCRIPT_COMPILER_CACHING);

        var props = new JavaProperties();
        props.SetProperty("commons.runnablestats.enable", "true");
        props.SetProperty("commons.script_compiler.caching.enable", "false");
        ConfigurableProcessor.Process(props, typeof(CommonsConfig));

        Assert.True(CommonsConfig.RUNNABLESTATS_ENABLE);
        Assert.False(CommonsConfig.SCRIPT_COMPILER_CACHING);

        // restore defaults so the static holder doesn't leak into other tests
        ConfigurableProcessor.Process(new JavaProperties(), typeof(CommonsConfig));
    }

    [Fact]
    public void JavaProperties_Load_HonorsCommentsSeparatorsAndEscapes()
    {
        const string content =
            "# comment line\n" +
            "! bang comment\n" +
            "a.key = value one\n" +
            "b.key:value two\n" +
            "c.key three\n" +              // whitespace separator
            "d.key = a\\tb\n" +            // tab escape
            "e.key = line1\\\n" +          // line continuation
            "    line2\n";
        var props = new JavaProperties();
        props.Load(new StringReader(content));

        Assert.Equal("value one", props.GetProperty("a.key"));
        Assert.Equal("value two", props.GetProperty("b.key"));
        Assert.Equal("three", props.GetProperty("c.key"));
        Assert.Equal("a\tb", props.GetProperty("d.key"));
        Assert.Equal("line1line2", props.GetProperty("e.key"));
    }

    [Fact]
    public void GetValue_LiteralEmptyQuotes_BecomeEmptyString_AndPlaceholdersSubstitute()
    {
        var props = new JavaProperties();
        props.SetProperty("base", "world");
        props.SetProperty("test.str", "${base}!");
        props.SetProperty("empty.str", "\"\"");

        ConfigurableProcessor.Process(props, typeof(Holder));
        Assert.Equal("world!", Holder.Text);
    }

    [Fact]
    public void Process_ReportsUnusedKeys()
    {
        var props = new JavaProperties();
        props.SetProperty("test.int", "1");
        props.SetProperty("totally.unknown", "x");
        var unused = ConfigurableProcessor.Process(props, typeof(Holder));
        Assert.Contains("totally.unknown", unused);
        Assert.DoesNotContain("test.int", unused);
    }
}
