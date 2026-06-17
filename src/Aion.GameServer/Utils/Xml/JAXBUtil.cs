using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Aion.GameServer.Utils.Xml;

namespace Aion.GameServer.Utils.Xml;

/// <summary>
/// Faithful-infra shim for Java commons JAXBUtil — JAXB (un)marshalling → C# XmlSerializer.
/// Java parity: utils/xml/JAXBUtil.deserialize(File, Class&lt;T&gt;).
/// </summary>
public static class JAXBUtil
{
    public static object Deserialize(FileInfo file, Type type)
    {
        var serializer = new XmlSerializer(type);
        using var stream = file.OpenRead();
        return serializer.Deserialize(stream)!;
    }

    // Java parity: deserialize(File, Class, String schema). This shim uses XmlSerializer which does not XSD-validate;
    // schemaPath is retained for call-site parity and ignored.
    public static object Deserialize(FileInfo file, Type type, string schemaPath) => Deserialize(file, type);

    public static T Deserialize<T>(FileInfo file) => (T)Deserialize(file, typeof(T));

    public static T Deserialize<T>(FileInfo file, string schemaPath) => (T)Deserialize(file, typeof(T), schemaPath);

    // Java parity: serialize(Object). JAXB marshalling → C# XmlSerializer to a UTF-8, formatted string.
    public static string Serialize(object obj) => Serialize(obj, null);

    // Java parity: serialize(Object, String schemaFile). XmlSerializer does not XSD-validate on output;
    // schemaFile is retained for call-site parity and ignored (mirrors the Deserialize shim above).
    public static string Serialize(object obj, string? schemaFile)
    {
        var serializer = new XmlSerializer(obj.GetType());
        var sb = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            Indent = true,                  // JAXB_FORMATTED_OUTPUT = true
            Encoding = new UTF8Encoding(false), // JAXB_ENCODING = UTF-8
        };
        using (var writer = XmlWriter.Create(sb, settings))
            serializer.Serialize(writer, obj);
        return sb.ToString();
    }

    // camelCase aliases for call sites transcribed literally from Java.
    public static object deserialize(FileInfo file, Type type) => Deserialize(file, type);
    public static T deserialize<T>(FileInfo file) => Deserialize<T>(file);
    public static string serialize(object obj) => Serialize(obj);
    public static string serialize(object obj, string? schemaFile) => Serialize(obj, schemaFile);
}
