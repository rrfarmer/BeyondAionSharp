using Aion.GameServer.Ai;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/ResurrectAI (@author ATracer).</summary>
[AIName("resurrect")]
public class ResurrectAI : NpcAI
{
    private static readonly ILogger log = NullLogger.Instance;

    public ResurrectAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        BindPointTemplate bindPointTemplate = DataManager.BIND_POINT_DATA.GetBindPointTemplate(GetNpcId());
        Race race = player.GetRace();
        if (bindPointTemplate == null)
        {
            log.LogInformation("There is no bind point template for npc: " + GetNpcId());
            return;
        }

        if (player.GetBindPoint() != null && player.GetBindPoint().GetMapId() == GetPosition().GetMapId()
            && PositionUtil.GetDistance(player.GetBindPoint().GetX(), player.GetBindPoint().GetY(), player.GetBindPoint().GetZ(), GetPosition().GetX(),
                GetPosition().GetY(), GetPosition().GetZ()) < 20)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_ALREADY_REGISTER_THIS_RESURRECT_POINT());
            return;
        }

        WorldType worldType = player.GetWorldType();
        if (!CustomConfig.ENABLE_CROSS_FACTION_BINDING && !GetTribe().Equals(TribeClass.FIELD_OBJECT_ALL))
        {
            if ((!GetRace().Equals(Race.NONE) && !GetRace().Equals(race))
                || (race.Equals(Race.ASMODIANS) && GetTribe().Equals(TribeClass.FIELD_OBJECT_LIGHT))
                || (race.Equals(Race.ELYOS) && GetTribe().Equals(TribeClass.FIELD_OBJECT_DARK)))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_BINDSTONE_CANNOT_FOR_INVALID_RIGHT(player.GetOppositeRace().ToString()));
                return;
            }
        }
        if (worldType == WorldType.PRISON)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_REGISTER_RESURRECT_POINT_FAR_FROM_NPC());
            return;
        }
        BindHere(player, bindPointTemplate);
    }

    private void BindHere(Player player, BindPointTemplate bindPointTemplate)
    {
        AIActions.AddRequest(this, player, SM_QUESTION_WINDOW.STR_ASK_REGISTER_RESURRECT_POINT, new ResurrectRequest(bindPointTemplate), bindPointTemplate.GetPrice());
    }

    private sealed class ResurrectRequest : AIRequest
    {
        private readonly BindPointTemplate bindPointTemplate;

        public ResurrectRequest(BindPointTemplate bindPointTemplate)
        {
            this.bindPointTemplate = bindPointTemplate;
        }

        public override void AcceptRequest(Creature requester, Player responder, int requestId)
        {
            // check if this both creatures are in same world
            if (responder.GetWorldId() == requester.GetWorldId())
            {
                // check enough kinah
                if (responder.GetInventory().GetKinah() < bindPointTemplate.GetPrice())
                {
                    PacketSendUtility.SendPacket(responder, SM_SYSTEM_MESSAGE.STR_CANNOT_REGISTER_RESURRECT_POINT_NOT_ENOUGH_FEE());
                    return;
                }
                else if (PositionUtil.GetDistance(requester, responder) > 5)
                {
                    PacketSendUtility.SendPacket(responder, SM_SYSTEM_MESSAGE.STR_CANNOT_REGISTER_RESURRECT_POINT_FAR_FROM_NPC());
                    return;
                }

                BindPointPosition old = responder.GetBindPoint();
                BindPointPosition bpp = new BindPointPosition(requester.GetWorldId(), responder.GetX(), responder.GetY(), responder.GetZ(),
                    responder.GetHeading());
                bpp.SetPersistentState(old == null ? IPersistable.PersistentState.NEW : IPersistable.PersistentState.UPDATE_REQUIRED);
                responder.SetBindPoint(bpp);
                if (PlayerBindPointDAO.Store(responder))
                {
                    responder.GetInventory().DecreaseKinah(bindPointTemplate.GetPrice());
                    TeleportService.SendObeliskBindPoint(responder);
                    PacketSendUtility.BroadcastPacket(responder, new SM_ACTION_ANIMATION(responder.GetObjectId(), ActionAnimation.BIND_KISK), true);
                    PacketSendUtility.SendPacket(responder, SM_SYSTEM_MESSAGE.STR_DEATH_REGISTER_RESURRECT_POINT());
                }
                else
                {
                    // if any errors happen, left that player with old bind point
                    responder.SetBindPoint(old);
                }
            }
        }
    }
}
