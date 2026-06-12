using System;
using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.World;
using static Aion.GameServer.Services.Panesterra.Ahserion.PanesterraFaction;

namespace Aion.GameServer.Services.Panesterra.Ahserion;

/// <summary>Java parity: services/panesterra/ahserion/PanesterraTeam (Yeats, Estrayl). A Panesterra faction team: faction-specific origin/start WorldPositions (set in ctor switch), member roster, move-to-origin/start teleports, eliminated flag. switch-arrow->switch statement / switch expression; switch-on-Race; static-import enum->using static. NOTE: removeTeamMember preserves Java List&lt;Integer&gt;.remove(int) = remove-by-INDEX (int-overload pitfall) via RemoveAt. WorldPosition/Race red-tolerated.</summary>
public class PanesterraTeam
{
    private static readonly WorldPosition ELYOS_ORIGIN_POS = new WorldPosition(110070000, 503.567f, 375.164f, 126.790f, (byte)30);
    private static readonly WorldPosition ASMO_ORIGIN_POS = new WorldPosition(120080000, 429.001f, 250.508f, 93.129f, (byte)60);

    private readonly List<int> teamMembers = new List<int>();
    private readonly PanesterraFaction faction;
    private WorldPosition originPosition;
    private WorldPosition startPosition;
    private bool isEliminated;

    public PanesterraTeam(PanesterraFaction faction)
    {
        this.faction = faction;
        switch (faction)
        {
            case BELUS:
                originPosition = new WorldPosition(400020000, 1024.172f, 1063.969f, 1530.3f, (byte)90);
                startPosition = new WorldPosition(400030000, 287.727f, 291.105f, 680.106f, (byte)15);
                break;
            case IVY_TEMPLE:
                startPosition = new WorldPosition(400020000, 550.663f, 552.074f, 1484.714f, (byte)15);
                break;
            case HIGHLAND_TEMPLE:
                startPosition = new WorldPosition(400020000, 551.551f, 1496.771f, 1484.714f, (byte)105);
                break;
            case ALPINE_TEMPLE:
                startPosition = new WorldPosition(400020000, 1494.988f, 1495.968f, 1484.714f, (byte)72);
                break;
            case GRANDWEIR_TEMPLE:
                startPosition = new WorldPosition(400020000, 1495.438f, 551.718f, 1484.714f, (byte)45);
                break;
            case ASPIDA:
                originPosition = new WorldPosition(400040000, 1024.172f, 1063.969f, 1530.3f, (byte)90);
                startPosition = new WorldPosition(400030000, 288.272f, 731.896f, 680.117f, (byte)105);
                break;
            case NOERREN_TEMPLE:
                startPosition = new WorldPosition(400040000, 550.663f, 552.074f, 1484.714f, (byte)15);
                break;
            case BOREALIS_TEMPLE:
                startPosition = new WorldPosition(400040000, 551.551f, 1496.771f, 1484.714f, (byte)105);
                break;
            case MYRKREN_TEMPLE:
                startPosition = new WorldPosition(400040000, 1494.988f, 1495.968f, 1484.714f, (byte)72);
                break;
            case GLUMVEILEN_TEMPLE:
                startPosition = new WorldPosition(400040000, 1495.438f, 551.718f, 1484.714f, (byte)45);
                break;
            case ATANATOS:
                originPosition = new WorldPosition(110070000, 503.567f, 375.164f, 126.790f, (byte)30); // TODO: Change to fortress pos
                startPosition = new WorldPosition(400030000, 728.675f, 735.638f, 680.099f, (byte)75);
                break;
            case MEMORIA_TEMPLE:
                startPosition = new WorldPosition(400050000, 550.663f, 552.074f, 1484.714f, (byte)15);
                break;
            case SYBILLINE_TEMPLE:
                startPosition = new WorldPosition(400050000, 551.551f, 1496.771f, 1484.714f, (byte)105);
                break;
            case AUSTERITY_TEMPLE:
                startPosition = new WorldPosition(400050000, 1494.988f, 1495.968f, 1484.714f, (byte)72);
                break;
            case SERENITY_TEMPLE:
                startPosition = new WorldPosition(400050000, 1495.438f, 551.718f, 1484.714f, (byte)45);
                break;
            case DISILLON:
                originPosition = new WorldPosition(120080000, 429.001f, 250.508f, 93.129f, (byte)60); // TODO: Change to fortress pos
                startPosition = new WorldPosition(400030000, 730.642f, 293.440f, 680.118f, (byte)45);
                break;
            case NECROLUCE_TEMPLE:
                startPosition = new WorldPosition(400060000, 550.663f, 552.074f, 1484.714f, (byte)15);
                break;
            case ESMERAUDUS_TEMPLE:
                startPosition = new WorldPosition(400060000, 551.551f, 1496.771f, 1484.714f, (byte)105);
                break;
            case VOLTAIC_TEMPLE:
                startPosition = new WorldPosition(400060000, 1494.988f, 1495.968f, 1484.714f, (byte)72);
                break;
            case ILLUMINATUS_TEMPLE:
                startPosition = new WorldPosition(400060000, 1495.438f, 551.718f, 1484.714f, (byte)45);
                break;
        }
    }

    public void MoveTeamMembersToOriginPosition()
    {
        ForEachMember(player =>
        {
            if (player.GetWorldId() == 400030000)
                MovePlayerToOriginPosition(player);
        });
    }

    public void ForEachMember(Action<Player> consumer)
    {
        foreach (int playerId in teamMembers)
        {
            Player player = Aion.GameServer.World.World.GetInstance().GetPlayer(playerId);
            if (player != null)
                consumer(player);
        }
    }

    public void MovePlayerToOriginPosition(Player player)
    {
        WorldPosition targetPosition = faction switch
        {
            BELUS or ASPIDA or ATANATOS or DISILLON => originPosition,
            _ => player.GetRace() switch
            {
                Race.ELYOS => ELYOS_ORIGIN_POS,
                Race.ASMODIANS => ASMO_ORIGIN_POS,
                _ => null,
            },
        };
        if (targetPosition != null)
            TeleportService.TeleportTo(player, targetPosition);
    }

    public void MovePlayerToStartPosition(Player player)
    {
        TeleportService.TeleportTo(player, startPosition);
    }

    public void AddTeamMemberIfAbsent(int playerId)
    {
        if (teamMembers.Contains(playerId))
            return;
        teamMembers.Add(playerId);
    }

    public bool IsTeamMember(int playerId)
    {
        return teamMembers.Contains(playerId);
    }

    public void RemoveTeamMember(int playerId)
    {
        if (teamMembers.Contains(playerId))
            teamMembers.RemoveAt(playerId); // Java List<Integer>.remove(int) resolves to remove-by-index (int-overload pitfall) - preserved literally
    }

    public bool IsEliminated()
    {
        return isEliminated;
    }

    public void SetIsEliminated(bool value)
    {
        isEliminated = value;
    }

    public WorldPosition GetStartPosition()
    {
        return startPosition;
    }

    public int GetMemberCount()
    {
        return teamMembers.Count;
    }

    public PanesterraFaction GetFaction()
    {
        return faction;
    }
}
