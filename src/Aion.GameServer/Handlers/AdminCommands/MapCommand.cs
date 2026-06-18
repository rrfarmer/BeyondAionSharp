using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/MapCommand (Rolandas, Neon).</summary>
public class MapCommand : AdminCommand
{
    public MapCommand()
        : base("map", "Offers different functions for the current map instance.")
    {
        SetSyntaxInfo(
            "freeze - Freezes all NPCs on this map instance.",
            "unfreeze - Unfreezes all NPCs on this map instance."
        );
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        if ("freeze".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            List<Npc> npcs = admin.GetPosition().GetWorldMapInstance().GetNpcs();
            npcs.ForEach(npc => npc.GetAi().OnGeneralEvent(AiEventType.FREEZE));
            SendInfo(admin, "World map is frozen!");
            long walkerCount = npcs.Count(o => o.GetAi().GetState() == AIState.WALKING);
            SendInfo(admin, "There are " + walkerCount + " walkers remaining on map " + admin.GetPosition().GetWorldMapInstance().GetMapId());
        }
        else if ("unfreeze".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            admin.GetPosition().GetWorldMapInstance().ForEachNpc(npc => npc.GetAi().OnGeneralEvent(AiEventType.UNFREEZE));
            SendInfo(admin, "World map is unfrozen!");
        }
    }
}
