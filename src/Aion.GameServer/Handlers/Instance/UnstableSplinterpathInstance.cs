using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/abyss/UnstableSplinterpathInstance (zhkchi, vlog, Luzien, Cheatkiller) : GeneralInstanceHandler. @InstanceID(300600000). 1:1.</summary>
[InstanceID(300600000)]
public class UnstableSplinterpathInstance : GeneralInstanceHandler
{
    private int destroyedFragments;
    private int killedPazuzuWorms;

    public UnstableSplinterpathInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnSpawn(VisibleObject @object)
    {
        if (@object is Npc npc)
        {
            switch (npc.GetNpcId())
            {
                case 219563: SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdDH_Wakeup()); break;
                case 219555: SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdD_Wakeup()); break;
            }
        }
    }

    public override void OnDie(Npc npc)
    {
        base.OnDie(npc);
        int npcId = npc.GetNpcId();
        switch (npcId)
        {
            case 219554: // Unstable Pazuzu
                SpawnPazuzuFragment();
                SpawnPazuzuTreasureBoxes();
                break;
            case 219553: // Unstable Kaluva the Fourth
                SpawnKaluvaFragment();
                SpawnKaluvaTreasureBoxes();
                break;
            case 219551: // Unstable Rukril
            case 219552: // Unstable Ebonsoul
                if (GetNpc(npcId == 219552 ? 219551 : 219552) == null)
                {
                    SpawnDayshadeFragment();
                    SpawnDayshadeTreasureBoxes();
                }
                else
                {
                    SendMsg(npcId == 219551 ? SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdC_Light_Die() : SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdC_Dark_Die());
                    ThreadPoolManager.GetInstance().Schedule(_ =>
                    {
                        if (GetNpc(npcId == 219552 ? 219551 : 219552) != null)
                        {
                            switch (npcId)
                            {
                                case 219551: Spawn(219552, 447.1937f, 683.72217f, 433.1805f, (byte)108); break; // rukril
                                case 219552: Spawn(219551, 455.5502f, 702.09485f, 433.13727f, (byte)108); break; // ebonsoul
                            }
                        }
                        return ValueTask.CompletedTask;
                    }, 60000);
                }
                npc.GetController().Delete();
                break;
            case 283204: // Piece of Splendor
                Npc ebonsoul = GetNpc(219552);
                if (ebonsoul != null && !ebonsoul.IsDead() && PositionUtil.IsInRange(npc, ebonsoul, 5))
                {
                    ebonsoul.GetEffectController().RemoveEffect(19159);
                    DeleteAliveNpcs(281907);
                    break;
                }
                npc.GetController().Delete();
                break;
            case 283205: // Piece of Midnight
                Npc rukril = GetNpc(219551);
                if (rukril != null && !rukril.IsDead() && PositionUtil.IsInRange(npc, rukril, 5))
                {
                    rukril.GetEffectController().RemoveEffect(19266);
                    DeleteAliveNpcs(281908);
                    break;
                }
                npc.GetController().Delete();
                break;
            case 219555: // Durable Yamennes Blindsight
            case 219563: // Unstable Yamennes Painflare
                SpawnYamennesTreasureBoxes(npcId == 219555 ? 701579 : 701580);
                DeleteAliveNpcs(219586); // Summoned Unstable Ametgolem
                Spawn(730317, 328.476f, 762.585f, 197.479f, (byte)90); // Exit
                instance.ForEachPlayer(p => Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(19283, p, p));
                break;
            case 701588: // Huge Aether Fragment
                destroyedFragments++;
                OnFragmentKill();
                npc.GetController().Delete();
                break;
            case 283206: // Luminous Waterworm
                if (++killedPazuzuWorms == 4)
                {
                    killedPazuzuWorms = 0;
                    Npc pazuzu = GetNpc(219554);
                    if (pazuzu != null && !pazuzu.IsDead())
                    {
                        pazuzu.GetEffectController().RemoveEffect(19145);
                        pazuzu.GetEffectController().RemoveEffect(19291);
                    }
                }
                break;
            case 219567: // Spawn Gate
            case 219579: // Spawn Gate
            case 219580: // Spawn Gate
                DeleteSummons();
                break;
        }
    }

    public override void OnInstanceDestroy()
    {
        destroyedFragments = 0;
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        int bossId = npc.GetNpcId() switch // Artifact of Protection
        {
            700957 => 219563, // Unstable Yamennes Painflare (Hard Mode)
            701589 => 219555, // Durable Yamennes Blindsight (Easy Mode)
            _ => 0,
        };
        if (bossId != 0)
        {
            SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdDH_Wakeup());
            Spawn(bossId, 329.70886f, 733.8744f, 197.60938f, (byte)0);
            npc.GetController().Delete();
        }
    }

    private void SpawnPazuzuFragment()
    {
        Spawn(701588, 669.576f, 335.135f, 465.895f, (byte)0);
    }

    private void SpawnPazuzuTreasureBoxes()
    {
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdC_BoxSpawn());
        Spawn(701575, 649.24286f, 361.33755f, 466.0427f, (byte)33); // Abyssal Treasure Box
        Spawn(701576, 651.53204f, 357.085f, 466.1315f, (byte)66); // Genesis Treasure Box
        Spawn(701576, 647.00446f, 357.2484f, 465.8960f, (byte)0); // Genesis Treasure Box
        Spawn(701576, 653.8384f, 360.39508f, 466.4391f, (byte)100); // Genesis Treasure Box
    }

    private void SpawnKaluvaFragment()
    {
        Spawn(701588, 633.7498f, 557.8822f, 424.99347f, (byte)6);
    }

    private void SpawnKaluvaTreasureBoxes()
    {
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdC_BoxSpawn());
        Spawn(701576, 601.2931f, 584.66705f, 422.9955f, (byte)6); // Genesis Treasure Box
        Spawn(701576, 597.2156f, 583.95416f, 423.3474f, (byte)66); // Genesis Treasure Box
        Spawn(701576, 602.9586f, 589.2678f, 422.8296f, (byte)100); // Genesis Treasure Box
        Spawn(701577, 598.82776f, 588.25946f, 422.7739f, (byte)113); // Abyssal Treasure Box
    }

    private void SpawnDayshadeFragment()
    {
        Spawn(701588, 452.89706f, 692.36084f, 433.96838f, (byte)6);
    }

    private void SpawnDayshadeTreasureBoxes()
    {
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdC_BoxSpawn());
        Spawn(701576, 408.10938f, 650.9015f, 439.28332f, (byte)66); // Genesis Treasure Box
        Spawn(701576, 402.40375f, 655.55237f, 439.26288f, (byte)33); // Genesis Treasure Box
        Spawn(701576, 406.74445f, 655.5914f, 439.2548f, (byte)100); // Genesis Treasure Box
        Spawn(701578, 404.891f, 650.2943f, 439.2548f, (byte)130); // Abyssal Treasure Box
    }

    private void SpawnYamennesTreasureBoxes(int npcId)
    {
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdC_BoxSpawn());
        Spawn(701576, 326.978f, 729.8414f, 197.7078f, (byte)16); // Genesis Treasure Box
        Spawn(701576, 326.5296f, 735.13324f, 197.6681f, (byte)66); // Genesis Treasure Box
        Spawn(701576, 329.8462f, 738.41095f, 197.7329f, (byte)3); // Genesis Treasure Box
        Spawn(npcId, 330.891f, 733.2943f, 197.6404f, (byte)113); // Abyssal Treasure Box
    }

    private void DeleteSummons()
    {
        if (instance.GetNpcs(219567, 219579, 219580).All(c => c.IsDead()))
            DeleteAliveNpcs(219565, 219566); // Summoned Unstable Orkanimum, Summoned Unstable Lapilima
    }

    private void OnFragmentKill()
    {
        switch (destroyedFragments)
        {
            case 1: SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_Artifact_Die_01()); break;
            case 2: SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_Artifact_Die_02()); break;
            case 3:
                DeleteAliveNpcs(701589); // Artifact of Protection (Easy Mode)
                Spawn(700957, 326.1821f, 766.9640f, 202.1832f, (byte)100, 79); // Artifact of Protection (Hard Mode)
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_Artifact_Die_03());
                break;
        }
    }
}
