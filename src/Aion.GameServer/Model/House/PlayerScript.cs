using System.Text;
using Aion.GameServer.Commons.Utils;

namespace Aion.GameServer.Model.House;

/// <summary>
/// Java parity: model/house/PlayerScript. Java record → C# positional record. Java text block → C# raw string literal;
/// getBytes(StandardCharsets.UTF_16LE) → Encoding.Unicode.GetBytes (UTF-16LE, no BOM).
/// </summary>
public record PlayerScript(int Id, byte[] CompressedBytes, int UncompressedSize)
{
    // mitigates https://appsec.space/posts/aion-housing-exploit/
    public static readonly PlayerScript LUA_SANDBOX_FIX;

    static PlayerScript()
    {
        byte[] script = Encoding.Unicode.GetBytes(
"""
<?xml version="1.0" encoding="UTF-16" ?>
<lboxes>
	<lbox>
		<id>101</id>
		<name><![CDATA[Lua Fix]]></name>
		<desc><![CDATA[Secures the Lua environment against malicious actors]]></desc>
		<script><![CDATA[
_G.debug = nil
_G.dofile = nil
_G.io = nil
_G.load = nil
_G.loadfile = nil
_G.loadstring = nil
_G.package = nil
_G.require = nil
]]></script>
		<icon>1</icon>
	</lbox>
</lboxes>
""");
        LUA_SANDBOX_FIX = new PlayerScript(0, CompressUtil.Compress(script), script.Length);
    }

    public bool HasData()
    {
        return CompressedBytes != null && CompressedBytes.Length > 0;
    }
}
