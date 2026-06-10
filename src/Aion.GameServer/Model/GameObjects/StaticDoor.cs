using System.Collections.Generic;
using Aion.GameServer.Controllers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Staticdoor;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/StaticDoor (MrPoke, Rolandas) : StaticObject. EnumSet&lt;StaticDoorState&gt;→HashSet (ISet); StaticDoorState.GetFlag extension + static SetStates; getObjectTemplate narrowing→new; hex 0x9/0xA verbatim. StaticObjectController/GeoService/SM_EMOTION red-tolerated.</summary>
public class StaticDoor : StaticObject
{
    private HashSet<StaticDoorState> states;
    private bool isLocked = true;

    public StaticDoor(StaticObjectController controller, SpawnTemplate spawnTemplate, StaticDoorTemplate objectTemplate, int instanceId)
        : base(controller, spawnTemplate, objectTemplate)
    {
        states = new HashSet<StaticDoorState>();
        StaticDoorStateExtensions.SetStates(GetObjectTemplate().GetState(), states);
        if (objectTemplate.GetKeyId() < 2)
        {
            isLocked = false;
        }
    }

    public bool IsLocked()
    {
        return isLocked;
    }

    public void SetLocked(bool isLocked)
    {
        this.isLocked = isLocked;
    }

    /// <returns>the open state from states set</returns>
    public bool IsOpen()
    {
        return states.Contains(StaticDoorState.OPENED);
    }

    public HashSet<StaticDoorState> GetStates()
    {
        return states;
    }

    /// <param name="open">the open state to set</param>
    public void SetOpen(bool open)
    {
        EmotionType emotion;
        int packetState; // not important IMO, similar to internal state
        if (open)
        {
            emotion = EmotionType.OPEN_DOOR;
            states.Remove(StaticDoorState.CLICKABLE);
            states.Add(StaticDoorState.OPENED); // 1001
            packetState = 0x9;
            GeoService.GetInstance().SetDoorState(GetWorldId(), GetInstanceId(), GetSpawn().GetStaticId(), true);
        }
        else
        {
            emotion = EmotionType.CLOSE_DOOR;
            if ((GetObjectTemplate().GetState() & StaticDoorState.CLICKABLE.GetFlag()) == StaticDoorState.CLICKABLE.GetFlag())
                states.Add(StaticDoorState.CLICKABLE);
            states.Remove(StaticDoorState.OPENED); // 1010
            packetState = 0xA;
            GeoService.GetInstance().SetDoorState(GetWorldId(), GetInstanceId(), this.GetSpawn().GetStaticId(), false);
        }
        // int stateFlags = StaticDoorState.getFlags(states);
        PacketSendUtility.BroadcastPacket(this, new SM_EMOTION(this.GetSpawn().GetStaticId(), emotion, packetState));
    }

    public void ChangeState(bool open, int state)
    {
        state = state & 0xF;
        StaticDoorStateExtensions.SetStates(state, states);
        EmotionType emotion = open ? EmotionType.OPEN_DOOR : EmotionType.CLOSE_DOOR;
        PacketSendUtility.BroadcastPacket(this, new SM_EMOTION(this.GetSpawn().GetStaticId(), emotion, state));
    }

    public new StaticDoorTemplate GetObjectTemplate()
    {
        return (StaticDoorTemplate)base.GetObjectTemplate();
    }
}
