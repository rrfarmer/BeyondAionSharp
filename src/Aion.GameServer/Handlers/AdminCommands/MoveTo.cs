using System;
using System.Text.RegularExpressions;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/MoveTo (Neon).</summary>
public class MoveTo : AdminCommand
{
    public MoveTo()
        : base("moveto", "Moves you to any location.")
    {
        SetSyntaxInfo(
            "<x> <y> [z] - Moves you to the specified coordinates on the current map (also supports pasted xml attributes like x=\"1422.7744\" y=\"1250.0612\" z=\"569.47\").",
            "<map name|ID> <x> <y> [z] - Moves you to the specified position (map names need underscores instead of spaces).",
            "<position link> - Moves you to the position of the chat link.",
            "<player name> - Moves you to the position of the player.",
            "<npc name|ID> - Moves you to the position of the npc.");
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        string errorMsg = null;

        if (paramsArr.Length >= 1)
        {
            WorldPosition pos;
            if (paramsArr.Length == 1)
                pos = ChatUtil.GetPosition(paramsArr[0]);
            else
                pos = ParseWorldPosition(admin, paramsArr);
            if (pos != null)
            {
                pos.SetH(admin.GetHeading());
                DoMoveTo(admin, pos, "Teleported to " + WorldMapTypeExtensions.GetWorld(pos.GetMapId()) + "\nX:" + pos.GetX() + " Y:" + pos.GetY() + " Z:" + pos.GetZ());
                return;
            }
            else if (paramsArr.Length > 1 || paramsArr[0].StartsWith("[pos:"))
                errorMsg = "Invalid map position or missing/deactivated geo.";
        }

        if (paramsArr.Length == 1 && !IsDigits(paramsArr[0]))
        {
            Player player = World.World.GetInstance().GetPlayer(Util.ConvertName(paramsArr[0]));
            if (player != null && !player.Equals(admin))
            {
                DoMoveTo(admin, player.GetPosition(), "Teleported to " + ChatUtil.Name(player) + ".");
                return;
            }
            else if (errorMsg == null || player != null)
                errorMsg = "Invalid player name or player is offline.";
        }

        if (paramsArr.Length >= 1)
        {
            int npcId = GetNpcId(admin, paramsArr);
            if (npcId > 0)
            {
                SendInfo(admin, "Teleported to " + ChatUtil.Path(npcId, true) + ".");
                TeleportService.TeleportToNpc(admin, npcId);
                return;
            }
            else if (errorMsg == null)
                errorMsg = "Could not find the specified npc.";
        }

        SendInfo(admin, errorMsg);
    }

    private WorldPosition ParseWorldPosition(Player admin, string[] paramsArr)
    {
        int coordIndex = 0;
        int mapId;
        bool isMapNameOrId = Regex.IsMatch(paramsArr[0], "^([a-zA-Z_]+|[1-9][0-9]{8,})$");
        if (isMapNameOrId)
        {
            mapId = int.TryParse(paramsArr[0], out var r) ? r : 0;
            if (mapId == 0)
                mapId = WorldMapTypeExtensions.GetMapId(paramsArr[0]);
            coordIndex = 1;
        }
        else
        {
            mapId = admin.GetPosition().GetMapId();
        }
        float? x = null, y = null, z = null;
        Regex p = new Regex("^((?<type>x|y|z)(=|:)\"?)?(?<coord>[0-9]+(\\.[0-9]+)?f?)\"?,?$", RegexOptions.IgnoreCase);
        int maxIndex = Math.Min(paramsArr.Length, coordIndex + 3);
        for (int i = coordIndex; i < maxIndex; i++)
        {
            Match m = p.Match(paramsArr[i]);
            if (m.Success)
            {
                float coord = float.TryParse(m.Groups["coord"].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var c) ? c : 0f;
                string type = m.Groups["type"].Success ? m.Groups["type"].Value : null;
                if ("x".Equals(type, StringComparison.OrdinalIgnoreCase) || (x == null && type == null))
                    x = coord;
                else if ("y".Equals(type, StringComparison.OrdinalIgnoreCase) || (y == null && type == null))
                    y = coord;
                else if ("z".Equals(type, StringComparison.OrdinalIgnoreCase) || (z == null && type == null))
                    z = coord;
            }
            else
            {
                return null;
            }
        }
        return x == null || y == null ? null : ChatUtil.ParsedCoordsToWorldPosition(mapId, x.Value, y.Value, z, null);
    }

    private void DoMoveTo(Player admin, WorldPosition pos, string message)
    {
        SendInfo(admin, message); // msg before teleport, otherwise client could ignore it
        if (pos.IsInstanceMap() && pos.GetWorldMapInstance().GetParent().GetMainWorldMapInstance() == pos.GetWorldMapInstance())
        {
            // currently instance type maps have a main world map instance (default) which have no spawns, so we create a new instance instead
            WorldMapInstance instance = InstanceService.GetOrRegisterInstance(pos.GetMapId(), admin);
            pos = World.World.GetInstance().CreatePosition(pos.GetMapId(), pos.GetX(), pos.GetY(), pos.GetZ(), pos.GetHeading(), instance.GetInstanceId());
        }
        TeleportService.TeleportTo(admin, pos);
    }

    private int GetNpcId(Player admin, params string[] paramsArr)
    {
        if (IsDigits(paramsArr[0]))
        {
            int npcId = int.TryParse(paramsArr[0], out var r) ? r : 0;
            if (npcId > 0 && DataManager.SPAWNS_DATA.GetFirstSpawnByNpcId(admin.GetWorldId(), npcId) != null)
                return npcId;
        }
        else
        {
            string npcName = string.Join(" ", paramsArr).ToLower();
            foreach (NpcTemplate template in DataManager.NPC_DATA.GetNpcData())
            {
                if (template.GetName().ToLower().Equals(npcName))
                {
                    if (DataManager.SPAWNS_DATA.GetFirstSpawnByNpcId(admin.GetWorldId(), template.GetTemplateId()) != null)
                        return template.GetTemplateId();
                }
            }
        }
        return 0;
    }

    // Java parity: org.apache.commons.lang3.math.NumberUtils.isDigits(String) — true if non-empty and all ASCII digits.
    private static bool IsDigits(string str)
    {
        if (string.IsNullOrEmpty(str))
            return false;
        foreach (char c in str)
            if (!char.IsDigit(c))
                return false;
        return true;
    }
}
