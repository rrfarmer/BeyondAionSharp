using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Templates.Walker;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>
/// Java parity: data/handlers/admincommands/FixPath (Rolandas, Neon). Fixes Z-coordinates for npc walk routes
/// using your client (not internal geo data). The Java getZ() schedules a fixed-rate poll then blocks on
/// waitTask.get(5, SECONDS): TimeoutException = abort, CancellationException (the poll cancels itself once it
/// captured the new client Z) = success. The C# ThreadPoolManager fixed-rate seam returns a ScheduledTask whose
/// Completion task finishes once the poll self-cancels; we await that with a 5s timeout and distinguish the two
/// outcomes by ScheduledTask.IsCancelled (gameplay-faithful-infra-idiomatic: the await/cancel seam is infra, the
/// observable behavior — same poll period, same 5s timeout/abort vs. self-cancel/apply — is preserved 1:1).
/// </summary>
public class FixPath : AdminCommand
{
    private static volatile Player runner = null;
    private static bool oldInvul = false;

    public FixPath()
        : base("fixpath", "Fixes Z-coordinates for npc walk routes using your client (not internal geo data).")
    {
        SetSyntaxInfo(
            "[z-offset] - Gathers new Z-coordinates for the route of your target (default: uses old Z values as a base, optional: adds the offset to old values).",
            "<route id> [z-offset] - Gathers new Z-coordinates for the specified route (default: uses old Z values as a base, optional: adds the offset to old values).",
            "<cancel> - Cancels route fixing.");
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0 && !(admin.GetTarget() is Npc))
        {
            SendInfo(admin);
            return;
        }

        if (runner != null)
        {
            if (!admin.Equals(runner))
            {
                SendInfo(admin, "Someone is already running this command!");
            }
            else if ("cancel".Equals(paramsArr[0]))
            {
                SendInfo(admin, "Canceled path fixing.");
                Stop();
            }
            else
            {
                SendInfo(admin);
            }
            return;
        }

        int map = 0;
        float zOffset = 0.1f;
        WalkerTemplate t = null;

        if (paramsArr.Length > 0 && (t = DataManager.WALKER_DATA.GetWalkerTemplate(paramsArr[0])) != null)
        {
            if (paramsArr.Length > 1)
            {
                if (!TryParseFloat(paramsArr[1], out zOffset))
                {
                    SendInfo(admin, "Invalid Z offset.");
                    return;
                }
            }
            map = admin.GetWorldId();
            SendInfo(admin, "Make sure you are on the correct map. If not use <cancel>!");
        }
        else
        {
            if (admin.GetTarget() is Npc targetNpc)
            {
                if (paramsArr.Length > 0)
                {
                    if (!TryParseFloat(paramsArr[0], out zOffset))
                    {
                        SendInfo(admin, "Invalid Z offset.");
                        return;
                    }
                }
                map = targetNpc.GetWorldId();
                t = DataManager.WALKER_DATA.GetWalkerTemplate(targetNpc.GetSpawn().GetWalkerId());
            }
            if (t == null)
            {
                SendInfo(admin, "Couldn't find route id.");
                return;
            }
        }

        runner = admin;
        oldInvul = admin.IsInvulnerable();
        WalkerTemplate template = t;
        int worldId = map;
        float zOff = zOffset;

        ThreadPoolManager.GetInstance().Execute(() => // run in a new thread to avoid blocking everything
        {
            admin.SetCustomState(CustomPlayerState.INVULNERABLE);
            RouteStep start = template.GetRouteStep(0);
            TeleportService.TeleportTo(admin, new WorldPosition(worldId, start.GetX(), start.GetY(), start.GetZ() + zOff, admin.GetHeading()));
            float zDelta = GetZ(admin) - start.GetZ() + zOff;

            List<float> corrections = new List<float>(template.GetRouteSteps().Count);
            try
            {
                int maxNonWalkBackStep = template.GetLoopType() == WalkerTemplate.LoopType.WALK_BACK ? (template.GetRouteSteps().Count + 1) / 2
                    : template.GetRouteSteps().Count;
                foreach (RouteStep step in template.GetRouteSteps())
                {
                    if (runner == null) // on cancel
                        return;
                    if (step.GetStepIndex() > maxNonWalkBackStep) // walk back steps are added in afterUnmarshal, not in templates
                        break;
                    SendInfo(admin, "Teleporting to step " + step.GetStepIndex() + "...");
                    TeleportService.TeleportTo(admin,
                        new WorldPosition(admin.GetWorldId(), step.GetX(), step.GetY(), step.GetZ() + zDelta, admin.GetHeading()));
                    corrections.Add(GetZ(admin));
                }

                SendInfo(admin, "Saving and applying corrections...");

                WalkerData data = new WalkerData();
                WalkerTemplate newTemplate = new WalkerTemplate(template.GetRouteId());
                List<RouteStep> newSteps = new List<RouteStep>();

                for (int stepIndex = 0; stepIndex < corrections.Count; stepIndex++)
                {
                    float fixedZ = corrections[stepIndex];
                    RouteStep oldStep = template.GetRouteStep(stepIndex);
                    newSteps.Add(new RouteStep(oldStep.GetX(), oldStep.GetY(), fixedZ, oldStep.GetRestTime()));
                    oldStep.SetZ(fixedZ); // live fixing
                    if (template.GetLoopType() == WalkerTemplate.LoopType.WALK_BACK && stepIndex > 0 && stepIndex < maxNonWalkBackStep) // live fixing of walk back steps
                        template.GetRouteStep(template.GetRouteSteps().Count - stepIndex).SetZ(fixedZ);
                }

                newTemplate.SetRouteSteps(newSteps);
                newTemplate.SetLoopType(template.GetLoopType());
                newTemplate.SetPool(template.GetPool());
                newTemplate.SetType(template.GetType_());
                newTemplate.SetRows(template.GetRows());
                data.AddTemplate(newTemplate);
                data.SaveData(template.GetRouteId());

                SendInfo(admin, "Finished path fixing.");
            }
            finally
            {
                Stop();
            }
        });
    }

    /// <summary>
    /// Returns the Z coordinate of the player after he stopped moving, or 0 on timeout.
    /// </summary>
    private float GetZ(Player admin)
    {
        SendInfo(admin, "Waiting to get Z...");
        float[] lastZ = { admin.GetZ() };
        ScheduledTask[] waitTask = { null };
        waitTask[0] = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            float z = admin.GetZ();
            if (admin.GetMoveController().GetMovementMask() == MovementMask.IMMEDIATE && z != lastZ[0] && admin.IsSpawned()
                && !admin.IsInState(CreatureState.DEAD))
            {
                waitTask[0].Cancel(true);
                lastZ[0] = z;
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(250));
        // Java parity: waitTask.get(5, SECONDS) — TimeoutException = abort, CancellationException (the poll cancels
        // itself once it captured the new client Z) = success. The poll's self-cancel completes the task; if it does
        // not complete within 5s it timed out.
        bool completed = waitTask[0].Completion.Wait(TimeSpan.FromSeconds(5));
        if (!completed)
        {
            waitTask[0].Cancel(true);
            SendInfo(admin, "Aborted path fixing due to timeout.");
            Stop();
            return 0;
        }
        return lastZ[0];
    }

    private void Stop()
    {
        if (runner != null)
        {
            if (runner.IsOnline())
            {
                if (!oldInvul && runner.IsInvulnerable())
                    runner.UnsetCustomState(CustomPlayerState.INVULNERABLE);
            }
            runner = null;
        }
    }
}
