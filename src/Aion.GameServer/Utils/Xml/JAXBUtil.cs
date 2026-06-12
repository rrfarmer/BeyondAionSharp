using System.IO;
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

    public static T Deserialize<T>(FileInfo file) => (T)Deserialize(file, typeof(T));

    // camelCase aliases for call sites transcribed literally from Java.
    public static object deserialize(FileInfo file, Type type) => Deserialize(file, type);
    public static T deserialize<T>(FileInfo file) => Deserialize<T>(file);
}
