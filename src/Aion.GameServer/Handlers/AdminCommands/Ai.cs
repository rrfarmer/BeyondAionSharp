using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Ai (ATracer, Neon).</summary>
public class Ai : AdminCommand
{
    public Ai()
        : base("ai", "Modifies and shows AI details.")
    {
        SetSyntaxInfo(
            "<info> - Show AI info for your target.",
            "<set> <aiName> - Changes the AI of your target.",
            "<state> <stateName> [substateName] - Changes the AI state.",
            "<event> <eventName> - Fires the AI event for the given name.",
            "<event2> <eventName> <creatureObjId> - Fires the creature AI event for the given name and creature.",
            "<events> - Shows last AI events for your target.",
            "<log> - Toggles AI logging for your target on and off.",
            "<createlog|eventlog|movelog> - Toggles logging on and off.",
            "<marker> [text] - Prints a marker with the optional text in log."
        );
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        if (paramsArr[0].Equals("createlog", StringComparison.OrdinalIgnoreCase))
        {
            AIConfig.ONCREATE_DEBUG = !AIConfig.ONCREATE_DEBUG;
            SendInfo(admin, "New createlog value: " + AIConfig.ONCREATE_DEBUG);
        }
        else if (paramsArr[0].Equals("eventlog", StringComparison.OrdinalIgnoreCase))
        {
            AIConfig.EVENT_DEBUG = !AIConfig.EVENT_DEBUG;
            SendInfo(admin, "New eventlog value: " + AIConfig.EVENT_DEBUG);
        }
        else if (paramsArr[0].Equals("movelog", StringComparison.OrdinalIgnoreCase))
        {
            AIConfig.MOVE_DEBUG = !AIConfig.MOVE_DEBUG;
            SendInfo(admin, "New movelog value: " + AIConfig.MOVE_DEBUG);
        }
        else if (paramsArr[0].Equals("marker", StringComparison.OrdinalIgnoreCase))
        {
            if (paramsArr.Length > 1)
                NullLoggerFactory.Instance.CreateLogger(typeof(AILogger).FullName).LogInformation("[AI] marker: " + string.Join(" ", paramsArr, 1, paramsArr.Length - 1));
            else
                NullLoggerFactory.Instance.CreateLogger(typeof(AILogger).FullName).LogInformation("[AI] marker");
        }
        else
        {
            VisibleObject target = admin.GetTarget();
            if (target == null || !(target is Creature))
            {
                PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
                return;
            }
            Creature npc = (Creature) target;

            if (paramsArr[0].Equals("info", StringComparison.OrdinalIgnoreCase))
            {
                SendInfo(admin,
                    "[AI info]\n\tName: " + npc.GetAi().GetName() + "\n\tState: " + npc.GetAi().GetState() + "\n\tSubstate: " + npc.GetAi().GetSubState());
            }
            else if (paramsArr[0].Equals("log", StringComparison.OrdinalIgnoreCase))
            {
                bool oldValue = npc.GetAi().IsLogging();
                npc.GetAi().SetLogging(!oldValue);
                SendInfo(admin, "New log value: " + !oldValue);
            }
            else if (paramsArr[0].Equals("events", StringComparison.OrdinalIgnoreCase))
            {
                AIEventLog eventLog = npc.GetAi().GetEventLog();
                if (eventLog == null || eventLog.Count == 0)
                {
                    SendInfo(admin, "No events logged" + (AIConfig.EVENT_DEBUG ? "" : " (enable event logging via eventlog parameter)"));
                }
                else
                {
                    foreach (AiEventType eventType in eventLog)
                    {
                        SendInfo(admin, "EVENT: " + eventType.ToString());
                    }
                }
            }
            else if (paramsArr.Length > 1)
            {
                string param1 = paramsArr[1];
                if (paramsArr[0].Equals("set", StringComparison.OrdinalIgnoreCase))
                {
                    string aiName = param1;
                    try
                    {
                        AbstractAI newAi = AIEngine.GetInstance().NewAI(aiName, npc);
                        try
                        {
                            FieldInfo aiField = npc.GetType().BaseType.GetField("ai", BindingFlags.NonPublic | BindingFlags.Instance);
                            World.World.GetInstance().Despawn(npc, ObjectDeleteAnimation.NONE);
                            aiField.SetValue(npc, newAi);
                            World.World.GetInstance().Spawn(npc); // properly init AI states
                        }
                        catch (Exception e) when (e is FieldAccessException || e is MemberAccessException)
                        {
                            NullLoggerFactory.Instance.CreateLogger(typeof(Ai).FullName).LogError(e, "");
                        }
                        if (npc.GetAi() == newAi)
                            SendInfo(admin, "Npc now has AI " + newAi.GetType().Name);
                        else
                            SendInfo(admin, "Error changing AI (see logs)");
                    }
                    catch (ArgumentException e)
                    {
                        SendInfo(admin, e.Message);
                    }
                }
                else if (paramsArr[0].Equals("event", StringComparison.OrdinalIgnoreCase))
                {
                    if (Enum.GetNames(typeof(AiEventType)).Contains(param1.ToUpper()))
                    {
                        AiEventType eventType = Enum.Parse<AiEventType>(param1.ToUpper());
                        npc.GetAi().OnGeneralEvent(eventType);
                    }
                    else
                    {
                        SendInfo(admin, "Found no event with that name");
                    }
                }
                else if (paramsArr[0].Equals("event2", StringComparison.OrdinalIgnoreCase))
                {
                    Creature creature = paramsArr.Length < 3 ? null : (Creature) World.World.GetInstance().FindVisibleObject(int.Parse(paramsArr[2]));
                    if (creature == null)
                        SendInfo(admin, "Please provide a valid creature object ID");
                    else
                    {
                        if (Enum.GetNames(typeof(AiEventType)).Contains(param1.ToUpper()))
                        {
                            AiEventType eventType = Enum.Parse<AiEventType>(param1.ToUpper());
                            npc.GetAi().OnCreatureEvent(eventType, creature);
                        }
                        else
                        {
                            SendInfo(admin, "Found no event with that name");
                        }
                    }
                }
                else if (paramsArr[0].Equals("state", StringComparison.OrdinalIgnoreCase))
                {
                    AIState? state = null;
                    try
                    {
                        // Java parity: AIState.valueOf(name) — matches enum constant name only (throws on unknown name).
                        if (!Enum.GetNames(typeof(AIState)).Contains(param1.ToUpper()))
                            throw new ArgumentException();
                        state = Enum.Parse<AIState>(param1.ToUpper());
                        npc.GetAi().SetStateIfNot(state.Value);
                        if (paramsArr.Length > 2)
                        {
                            if (!Enum.GetNames(typeof(AISubState)).Contains(paramsArr[2]))
                                throw new ArgumentException();
                            AISubState substate = Enum.Parse<AISubState>(paramsArr[2]);
                            npc.GetAi().SetSubStateIfNot(substate);
                        }
                    }
                    catch (ArgumentException)
                    {
                        SendInfo(admin, "Found no " + (state == null ? "state" : "substate") + " with that name");
                    }
                }
            }
            else
            {
                SendInfo(admin);
            }
        }
    }
}
