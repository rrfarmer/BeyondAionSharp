using System.Collections.Generic;

namespace Aion.GameServer.Model.GameObjects.Players.Motion;

/// <summary>Java parity: model/gameobjects/player/motion/MotionList.</summary>
public class MotionList
{
    private Aion.GameServer.Model.GameObjects.Players.Player owner;
    // Java parity: LinkedHashMap — insertion-ordered.
    private Dictionary<int, Motion> activeMotions;
    private Dictionary<int, Motion> motions;

    public MotionList(Aion.GameServer.Model.GameObjects.Players.Player owner)
    {
        this.owner = owner;
    }

    /// <summary>the activeMotions</summary>
    public IDictionary<int, Motion> GetActiveMotions()
    {
        if (activeMotions == null)
            return new Dictionary<int, Motion>();
        return activeMotions;
    }

    /// <summary>the motions</summary>
    public IDictionary<int, Motion> GetMotions()
    {
        if (motions == null)
            return new Dictionary<int, Motion>();
        return motions;
    }

    public void Add(Motion motion, bool persist)
    {
        if (motions == null)
            motions = new Dictionary<int, Motion>();
        if (motions.ContainsKey(motion.GetId()) && motion.GetExpireTime() == 0)
        {
            Remove(motion.GetId());
        }
        motions[motion.GetId()] = motion;
        if (motion.IsActive())
        {
            if (activeMotions == null)
                activeMotions = new Dictionary<int, Motion>();
            int key = Motion.motionType[motion.GetId()];
            activeMotions.TryGetValue(key, out Motion old);
            activeMotions[key] = motion;
            if (old != null)
            {
                old.SetActive(false);
                Aion.GameServer.Dao.MotionDAO.UpdateMotion(owner.GetObjectId(), old);
            }
        }
        if (persist)
        {
            Aion.GameServer.Taskmanager.Tasks.ExpireTimerTask.GetInstance().RegisterExpirable(motion, owner);
            Aion.GameServer.Dao.MotionDAO.StoreMotion(owner.GetObjectId(), motion);
        }
    }

    public bool Remove(int motionId)
    {
        if (!motions.TryGetValue(motionId, out Motion motion))
            motion = null;
        else
            motions.Remove(motionId);
        if (motion != null)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmMotion((short)motionId));
            Aion.GameServer.Dao.MotionDAO.DeleteMotion(owner.GetObjectId(), motionId);
            if (motion.IsActive())
            {
                activeMotions.Remove(Motion.motionType[motionId]);
                return true;
            }
        }
        return false;
    }

    public void SetActive(int motionId, int motionType)
    {
        if (motionId != 0)
        {
            if (!motions.TryGetValue(motionId, out Motion motion))
                motion = null;
            if (motion == null || motion.IsActive())
                return;
            if (activeMotions == null)
                activeMotions = new Dictionary<int, Motion>();
            activeMotions.TryGetValue(motionType, out Motion old);
            activeMotions[motionType] = motion;
            if (old != null)
            {
                old.SetActive(false);
                Aion.GameServer.Dao.MotionDAO.UpdateMotion(owner.GetObjectId(), old);
            }
            motion.SetActive(true);
            Aion.GameServer.Dao.MotionDAO.UpdateMotion(owner.GetObjectId(), motion);
        }
        else if (activeMotions != null)
        {
            if (!activeMotions.TryGetValue(motionType, out Motion old))
                return; // TODO packet hack??
            activeMotions.Remove(motionType);
            old.SetActive(false);
            Aion.GameServer.Dao.MotionDAO.UpdateMotion(owner.GetObjectId(), old);
        }
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmMotion((short)motionId, (byte)motionType));
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmMotion(owner.GetObjectId(), activeMotions), true);
    }
}
