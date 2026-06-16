using System.Xml.Serialization;

namespace Aion.GameServer.Dataholders.LoadingUtils;

/// <summary>
/// Faithful per-holder XML load path for the JAXB-modelled data holders
/// (e.g. <c>BindPointData</c>, <c>ChestData</c>, <c>MotionData</c>).
///
/// Java loads the merged <c>static_data.xml</c> cache once via JAXB into the <c>StaticData</c> root,
/// then invokes each holder's <c>afterUnmarshal</c> callback. This helper provides the equivalent for a
/// single <c>[XmlRoot]</c> holder type loaded from its own feature XML file: deserialize with
/// <see cref="XmlSerializer"/>, then invoke the holder's <c>AfterUnmarshal(object)</c> (the JAXB callback
/// XmlSerializer does not call automatically).
///
/// PILOT SCOPE: only works for holders whose XML-bound members are PUBLIC fields/properties. JAXB read
/// private fields via @XmlAccessorType(FIELD); XmlSerializer only binds public members. Holders that still
/// use private [Xml*] fields must first expose those members publicly (see BindPointData/BindPointTemplate).
/// </summary>
public static class JaxbHolderLoader
{
    /// <summary>
    /// Deserialize <typeparamref name="T"/> from <paramref name="xmlFilePath"/> and invoke its
    /// <c>AfterUnmarshal(object)</c> callback if present, mirroring JAXB's unmarshal listener.
    /// </summary>
    public static T LoadFromFile<T>(string xmlFilePath) where T : class
    {
        if (!File.Exists(xmlFilePath))
            throw new FileNotFoundException($"Static data file not found: {xmlFilePath}", xmlFilePath);

        var serializer = new XmlSerializer(typeof(T));
        using var stream = File.OpenRead(xmlFilePath);
        var holder = (T)serializer.Deserialize(stream)!;
        InvokeAfterUnmarshal(holder);
        return holder;
    }

    private static void InvokeAfterUnmarshal(object holder)
    {
        // Java parity: javax.xml.bind Unmarshaller.Listener.afterUnmarshal(target, parent).
        var method = holder.GetType().GetMethod("AfterUnmarshal", new[] { typeof(object) });
        method?.Invoke(holder, new object?[] { null });
    }
}
