using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.Commons.Utils;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.World;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_HOUSE_TELEPORT (Rolandas). Teleport via relationship crystal to own/friend/random-friend house. HousingService/TeleportService/InstanceService red-tolerated.</summary>
public class CM_HOUSE_TELEPORT : AionClientPacket
{
    private int actionId;
    private int playerId1;
    private int playerId2;

    public CM_HOUSE_TELEPORT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        actionId = ReadUC();
        playerId1 = ReadD(); // just why? without this field we wouldn't even have to check exploitations
        playerId2 = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player == null)
            return;
        if (playerId1 != player.GetObjectId())
        {
            AuditLogger.Log(player, "tried to teleport playerId " + playerId1 + " instead of himself");
            return;
        }

        VisibleObject target = player.GetTarget();
        if (!(target is Npc))
            return;
        Npc relationshipCrystal = (Npc)target;
        if (relationshipCrystal.GetNpcTemplateType() != NpcTemplateType.HOUSING || !relationshipCrystal.GetAi().GetName().Equals("friendportal"))
        {
            AuditLogger.Log(player, "tried to use house teleport without targeting a relationship crystal: " + target);
            return;
        }

        House house;
        switch (actionId)
        {
            case 1: // to own house
                house = player.GetActiveHouse();
                break;
            case 2: // to friends house
                if (playerId2 == 0)
                    return;
                List<House> friendsAccessibleHouses = FindFriendsAccessibleHouses(player);
                house = friendsAccessibleHouses.FirstOrDefault(h => h.GetOwnerId() == playerId2);
                break;
            case 3: // to random friend's house
                house = Rnd.Get(FindFriendsAccessibleHouses(player));
                if (house == null)
                {
                    SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_NO_RELATIONSHIP_RECENTLY());
                    return;
                }
                break;
            default:
                NullLoggerFactory.Instance.CreateLogger(GetType_().Name).LogWarning("Unhandled house teleport actionId " + actionId);
                return;
        }

        if (house == null)
            return;

        WorldMapInstance instance = InstanceService.GetOrCreateHouseInstance(house);
        TeleportService.TeleportTo(player, instance, house.GetX(), house.GetY(), house.GetZ(), house.GetTeleportHeading(),
            TeleportAnimation.FADE_OUT_BEAM);
    }

    private List<House> FindFriendsAccessibleHouses(Player player)
    {
        List<House> houses = new List<House>();
        foreach (Friend friend in player.GetFriendList())
            AddHouseIfAccessible(player, houses, friend.GetObjectId());
        Legion legion = player.GetLegion();
        if (legion != null)
        {
            foreach (int memberId in legion.GetMemberIds())
            {
                if (memberId != player.GetObjectId())
                    AddHouseIfAccessible(player, houses, memberId);
            }
        }
        return houses;
    }

    private void AddHouseIfAccessible(Player player, List<House> relationIds, int friendId)
    {
        House house = HousingService.GetInstance().FindActiveHouse(friendId);
        if (house != null && house.CanEnter(player))
            relationIds.Add(house);
    }
}
