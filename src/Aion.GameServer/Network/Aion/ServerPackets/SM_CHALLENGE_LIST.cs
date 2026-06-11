using System;
using System.Collections.Generic;
using Aion.GameServer.Model.Challenge;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Challenge;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_CHALLENGE_LIST (ViAl). Legion/town challenge task list (action 2) or individual task quests (action 7). currentTimeMillis()/1000 -> DateTimeOffset; ChallengeTask/ChallengeQuest/ChallengeType red-tolerated.</summary>
public class SM_CHALLENGE_LIST : AionServerPacket
{
    private int action;
    private int ownerId;
    private ChallengeType ownerType;
    private List<ChallengeTask> tasks;
    private ChallengeTask task;

    public SM_CHALLENGE_LIST(int action, int ownerId, ChallengeType ownerType, List<ChallengeTask> tasks)
    {
        this.action = action;
        this.ownerId = ownerId;
        this.ownerType = ownerType;
        this.tasks = tasks;
    }

    public SM_CHALLENGE_LIST(int action, int ownerId, ChallengeType ownerType, ChallengeTask task)
    {
        this.action = action;
        this.ownerId = ownerId;
        this.ownerType = ownerType;
        this.task = task;
    }

    protected override void WriteImpl(AionConnection con)
    {
        Player player = con.GetActivePlayer();
        WriteC(action);
        WriteD(ownerId); // legionId or townId
        WriteC(ownerType.GetId()); // 1 for legion, 2 for town
        WriteD(player.GetObjectId());
        switch (action)
        {
            case 2: // send challenge tasks list
                WriteD((int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000));
                WriteH(tasks.Count);
                foreach (ChallengeTask task in tasks)
                {
                    WriteD(32); // unk
                    WriteD(task.GetTaskId());
                    WriteC(1); // unk
                    WriteC(21); // unk
                    WriteC(0); // unk
                    WriteD(task.GetCompleteTimeEpochSeconds());
                }
                break;
            case 7: // send individual challenge task info
                WriteD(32); // unk
                WriteD(task.GetTaskId());
                WriteH(task.GetQuestsCount());
                foreach (ChallengeQuest quest in task.GetQuests().Values)
                {
                    WriteD(quest.GetQuestId());
                    WriteH(quest.GetMaxRepeats());
                    WriteD(quest.GetScorePerQuest());
                    WriteH(quest.GetCompleteCount()); // unk
                }
                break;
        }
    }
}
