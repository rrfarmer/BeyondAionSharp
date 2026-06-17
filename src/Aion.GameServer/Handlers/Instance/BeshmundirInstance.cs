using System.Threading;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/BeshmundirInstance (Gigi, nrg, oslo0322, xTz) : GeneralInstanceHandler. @InstanceID(300170000). AtomicInteger macunbello/kills→int+Interlocked; onEnterInstance sets race; getExpMultiplier 3f; onDie boss webs (Temadaro macunbello tiers + applyEffectDirectly, door states, movies); onPlayMovieEnd; onInstanceCreate door 535. 1:1.</summary>
[InstanceID(300170000)]
public class BeshmundirInstance : GeneralInstanceHandler
{
    private int macunbello;
    private int kills;
    private Race? instanceRace;

    public BeshmundirInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnEnterInstance(Player player)
    {
        if (instanceRace == null)
            instanceRace = player.GetRace();
    }

    public override float GetExpMultiplier()
    {
        return 3f;
    }

    public override void OnDie(Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 216583: // Brutal Soulwatcher (Difficult)
            case 216587: // Brutal Soulwatcher (Normal)
                Spawn(799518, 936.0029f, 441.51712f, 220.5029f, (byte)28);
                break;
            case 216584: // Brutal Soulwatcher (Difficult)
            case 216588: // Brutal Soulwatcher (Normal)
                Spawn(799519, 791.0439f, 439.79608f, 220.3506f, (byte)28);
                break;
            case 216585: // Brutal Soulwatcher (Difficult)
            case 216589: // Brutal Soulwatcher (Normal)
                Spawn(799520, 820.70624f, 278.828f, 220.19385f, (byte)55);
                break;
            case 216586: // Temadaro (Difficult)
            case 216590: // Temadaro (Normal)
                int killedCount = Interlocked.Exchange(ref macunbello, 0);
                if (killedCount < 12)
                {
                    Npc npcMacunbello = (Npc)Spawn(216735, 981.015015f, 134.373001f, 241.755005f, (byte)30); // strongest macunbello
                    Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(19046, npcMacunbello, npcMacunbello);
                }
                else if (killedCount < 14)
                {
                    Npc npcMacunbello = (Npc)Spawn(216734, 981.015015f, 134.373001f, 241.755005f, (byte)30); // 2nd strongest macunbello
                    Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(19047, npcMacunbello, npcMacunbello);
                }
                else if (killedCount < 21)
                {
                    Npc npcMacunbello = (Npc)Spawn(216737, 981.015015f, 134.373001f, 241.755005f, (byte)30); // 2nd weakest macunbello
                    Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(19048, npcMacunbello, npcMacunbello);
                }
                else
                {
                    Spawn(216245, 981.015015f, 134.373001f, 241.755005f, (byte)30); // weakest macunbello
                }
                SendPacket(new SM_QUEST_ACTION(0, 0));
                instance.SetDoorState(467, true);
                break;
            case 799342:
                SendPacket(new SM_PLAY_MOVIE(false, 0, 0, 447, true));
                break;
            case 216157:
            case 216238:
                instance.SetDoorState(470, true);
                Spawn(216159, 1357.0598f, 388.6637f, 249.26372f, (byte)90);
                break;
            case 216165:
            case 216246:
                instance.SetDoorState(473, true);
                break;
            case 216739:
            case 216740:
                int killedTotal = Interlocked.Increment(ref kills);
                if (killedTotal < 10)
                {
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDCatacombs_NmdSpecter_Spawn());
                }
                else if (killedTotal == 10)
                {
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDCatacombs_NmdSpecter_Start());
                    Spawn(216158, 1356.5719f, 147.76418f, 246.27373f, (byte)91);
                }
                break;
            case 216158:
            case 216239:
                instance.SetDoorState(471, true);
                break;
            case 700608:
                if (instanceRace == Race.ASMODIANS)
                {
                    Spawn(799342, 1357.1f, 76.044f, 248.595f, (byte)0);
                }
                break;
            case 216263:
            case 216182:
                // this is a safety Mechanism
                // super boss
                Spawn(216183, 558.306f, 1369.02f, 224.795f, (byte)70);
                // gate
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDCatacombs_BigOrb_Spawn());
                Spawn(730275, 1611.1266f, 1604.6935f, 310.39972f, (byte)17, 426);
                break;
            case 216250: // Dorakiki the Bold
            case 216169:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDCatacombs_NmdShulack_Rufukin());
                Spawn(216527, 1161.859985f, 1213.859985f, 284.057007f, (byte)110); // Lupukin: cat trader
                break;
            case 216206:
            case 216207:
            case 216208:
            case 216209:
            case 216210:
            case 216211:
            case 216212:
            case 216213:
                switch (Interlocked.Increment(ref macunbello))
                {
                    case 12: SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDCatacombs_NmdLich_weakness1()); break;
                    case 14: SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDCatacombs_NmdLich_weakness2()); break;
                    case 21: SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDCatacombs_NmdLich_weakness3()); break;
                }
                break;
        }
    }

    private void SendPacket(AionServerPacket packet)
    {
        PacketSendUtility.BroadcastToMap(instance, packet);
    }

    public override void OnPlayMovieEnd(Player player, int movieId)
    {
        if (movieId == 443)
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_IDCatacombs_BigOrb_Spawn());
    }

    public override void OnInstanceCreate()
    {
        instance.SetDoorState(535, true);
    }
}
