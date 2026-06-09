using System.Text;

namespace Aion.GameServer.Dataholders.Loadingutils.Adapters;

/// <summary>
/// JAXB supports space separated int lists; this adapter works the same way for bytes. Java parity:
/// dataholders/loadingutils/adapters/SpaceSeparatedBytesAdapter (Neon). Java <c>extends XmlAdapter&lt;String,byte[]&gt;</c> —
/// C# has no XmlAdapter base (consumers use the [XmlIgnore]+Raw-string-property idiom delegating here); Java signed byte→sbyte.
/// </summary>
public class SpaceSeparatedBytesAdapter
{
    public string Marshal(sbyte[] v)
    {
        StringBuilder sb = new StringBuilder(v.Length * 3);
        for (int i = 0; i < v.Length; i++)
        {
            if (i > 0)
                sb.Append(' ');
            sb.Append(v[i]);
        }
        return sb.ToString();
    }

    public sbyte[] Unmarshal(string v)
    {
        string[] values = v.Split(' ');
        sbyte[] bytes = new sbyte[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            bytes[i] = sbyte.Parse(values[i]);
        }
        return bytes;
    }
}
