using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Templates.Windstreams;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.Services;
using Aion.GameServer.Services.ConquerorAndProtectorSystem;
using Aion.GameServer.Services.Event;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Rift;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_LEVEL_READY (-Nemesiss-, Kwazar). Client signals the map is loaded; spawns the player and fires the full enter-world sequence. Many services red-tolerated.</summary>
public class CM_LEVEL_READY : AionClientPacket
{
    public CM_LEVEL_READY(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();

        if (activePlayer.GetActiveHouse() != null)
            SendPacket(new SM_HOUSE_OBJECTS(activePlayer.GetActiveHouse().GetRegistry().GetSpawnedObjects()));
        if (activePlayer.IsInInstance())
        {
            SendPacket(new SM_INSTANCE_COUNT_INFO(activePlayer.GetWorldId(), activePlayer.GetInstanceId()));
        }
        SendPacket(new SM_PLAYER_INFO(activePlayer));
        activePlayer.GetController().StartProtectionActiveTask();
        SendPacket(new SM_ACCOUNT_PROPERTIES());
        SendPacket(new SM_MOTION(activePlayer.GetObjectId(), activePlayer.GetMotions().GetActiveMotions()));

        WindstreamTemplate template = DataManager.WINDSTREAM_DATA.GetStreamTemplate(activePlayer.GetPosition().GetMapId());
        if (template != null)
            foreach (Location2D location in template.GetLocations().GetLocation())
            {
                SendPacket(new SM_WINDSTREAM_ANNOUNCE(location.GetFlyPathType().GetId(), template.GetMapId(), location.GetId(), location.GetState()));
            }

        // Spawn player into the world.
        World.GetInstance().Spawn(activePlayer);

        if (activePlayer.IsInFlyState(FlyState.FLYING)) // notify client if we are still flying (client always ends flying after teleport)
            activePlayer.GetFlyController().StartFly(true, true);

        // SM_SHIELD_EFFECT, SM_ABYSS_ARTIFACT_INFO3
        if (activePlayer.IsInSiegeWorld())
        {
            SiegeService.GetInstance().OnEnterSiegeWorld(activePlayer);
        }

        // SM_CONQUEROR_PROTECTOR
        ConquerorAndProtectorService.GetInstance().OnEnterMap(activePlayer);

        // SM_RIFT_ANNOUNCE
        RiftInformer.SendRiftsInfo(activePlayer);

        // SM_UPGRADE_ARCADE
        if (EventsConfig.ENABLE_EVENT_ARCADE)
            SendPacket(new SM_UPGRADE_ARCADE(true));

        // SM_NEARBY_QUESTS
        activePlayer.GetController().UpdateNearbyQuests();

        // SM_QUEST_REPEAT
        activePlayer.GetController().UpdateRepeatableQuests();

        // Loading weather for the player's region
        WeatherService.GetInstance().LoadWeather(activePlayer);

        QuestEngine.GetInstance().OnEnterWorld(activePlayer);

        activePlayer.GetController().OnEnterWorld();
        InstanceService.OnEnterInstance(activePlayer);
        activePlayer.GetEffectController().UpdatePlayerEffectIcons(null);
        SendPacket(SM_CUBE_UPDATE.CubeSize(StorageType.CUBE, activePlayer));

        Pet pet = activePlayer.GetPet();
        if (pet != null && !pet.IsSpawned())
            World.GetInstance().Spawn(pet);
        activePlayer.SetPortAnimation(ArrivalAnimation.NONE);

        TownService.GetInstance().OnEnterWorld(activePlayer);
        EventService.GetInstance().OnEnterMap(activePlayer);

        var team = activePlayer.GetCurrentTeam();
        if (team != null)
            ThreadPoolManager.GetInstance().Schedule(() => team.SendBrands(activePlayer), 100); // delayed to fix brands when returning from studios/houses
    }
}
