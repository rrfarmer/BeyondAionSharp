using System;
using System.Drawing;
using System.Linq;
using System.Text;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/State (Rolandas). Views and adjusts your target's creature states.</summary>
public class State : AdminCommand
{
    public State()
        : base("state", "Views and adjusts your target's creature states.")
    {
        SetSyntaxInfo(
            " - Shows your target's creature states.",
            "<state> - Sets given creature state(s) by name or ID, replacing existing states.",
            "add <state> - Sets given creature state(s) by name or ID.",
            "remove <state> - Removes given creature state(s) by name or ID. Use -1 to remove all states.",
            "list - Shows possible state names and ID. Add ID values together to add or remove multiple states at once.");
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        VisibleObject target = admin.GetTarget();
        if (target == null)
        {
            SendInfo(admin);
            return;
        }
        if (!(target is Creature creature))
        {
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
            return;
        }

        if (paramsArr.Length == 0)
        {
            SendInfo(admin, creature.GetName() + "'s state: " + GetStateDescription(creature.GetState()) + "\nSee " + ChatUtil.Color(GetAliasWithPrefix() + " help", Color.White) + " for more options.");
        }
        else if ("list".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            SendInfo(admin, "Known states:\n\t" + string.Join("\n\t", Enum.GetValues<CreatureState>().Select(c => c.ToString() + " (" + c.GetId() + ')')));
        }
        else
        {
            int stateIndex = "add".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase) || "remove".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            if (paramsArr.Length <= stateIndex)
            {
                SendInfo(admin, "Please provide a state name or ID.");
                return;
            }
            int stateId;
            if (TryParseEnumName(paramsArr[stateIndex].ToUpper(), out CreatureState parsedState))
            {
                stateId = parsedState.GetId();
            }
            else
            {
                stateId = ParseInt(paramsArr[stateIndex]);
                if (stateId < 0 || stateId > 0xFFFF)
                {
                    SendInfo(admin, "Out of range state ID.");
                    return;
                }
            }
            int newState;
            if (stateIndex == 0)
                newState = stateId & 0xFFFF;
            else if ("add".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
                newState = (creature.GetState() | stateId) & 0xFFFF;
            else
                newState = (creature.GetState() & ~stateId) & 0xFFFF;

            creature.SetState(newState);

            if (target is Player player)
            {
                player.GetController().OnChangedPlayerAttributes();
            }
            else
            {
                creature.ClearKnownlist();
                creature.UpdateKnownlist();
            }
            ThreadPoolManager.GetInstance().Schedule(_ => { admin.SetTarget(target); return System.Threading.Tasks.ValueTask.CompletedTask; }, 200L);

            SendInfo(admin, creature.GetName() + "'s state changed to " + GetStateDescription(creature.GetState()));
        }
    }

    private string GetStateDescription(int state)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 1; i <= (state & 0xFFFF); i *= 2)
        {
            if ((state & i) == i)
            {
                if (sb.Length != 0)
                    sb.Append(" + ");
                sb.Append(FindStateName(i, "UNK")).Append(" (").Append(i).Append(')');
            }
        }
        return state + (sb.Length == 0 ? "" : " = " + sb.ToString());
    }

    private string FindStateName(int creatureStateId, string defaultName)
    {
        foreach (CreatureState s in Enum.GetValues<CreatureState>())
        {
            if (s.GetId() == creatureStateId)
                return s.ToString();
        }
        return defaultName;
    }
}
