using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Ai.Pattern;

/// <summary>
/// Turns the retail pattern tables' stored form back into the guards and actions a
/// <see cref="PatternAi"/> runs.
/// </summary>
/// <remarks>
/// <b>This is the half of the tables that used to be the C# compiler's job.</b> The extractor writes a
/// guard as a token — <c>hp_below:70</c>, <c>race:SeenRace:ELYOS</c> — and an action as a row of
/// numbers with a kind. An emitter used to paste those into C# source, so a token nobody could
/// translate was a build error. The data lives in XML now, so the translation happens here and the
/// failure has to be made just as loud: every unknown token throws
/// <see cref="PatternTableFormatException"/> naming the token, and the holder that loads a table
/// refuses the whole file rather than dropping a branch it could not read.
/// <para>
/// <b>Dropping a branch is the failure to fear, not throwing.</b> A boss missing one rung of its
/// rotation still fights, still looks alive, and is silently wrong — the same shape of bug the
/// extractor's all-or-nothing rule exists to prevent on the way in. This keeps that rule on the way
/// out.
/// </para>
/// <para>
/// Reflection would have written this in twenty lines and is deliberately not used: it would resolve
/// <c>When.AttackerRace</c> from a string at runtime, which turns a typo in the data into a
/// <c>MissingMethodException</c> at the moment a boss engages, and makes it impossible to see from the
/// source which names the tables may use.
/// </para>
/// </remarks>
public static class PatternTableLoader
{
    /// <summary>One stored action: a kind, up to three numbers, a role or place, and a position.</summary>
    public readonly record struct ActionRow(
        string Kind, string A1, string A2, string A3, string Place,
        string X, string Y, string Z, string Group);

    // ---- guards ----------------------------------------------------------------------------------

    /// <summary>Parameterless role tests: <c>who:</c>, <c>enemy:</c>, <c>flying:</c>, <c>state:</c>.</summary>
    private static readonly Dictionary<string, PatternCondition> Plain = new()
    {
        ["AttackedByPlayer"] = When.AttackedByPlayer,
        ["SpelledByPlayer"] = When.SpelledByPlayer,
        ["SeenIsPlayer"] = When.SeenIsPlayer,
        ["TalkerIsPlayer"] = When.TalkerIsPlayer,
        ["TargetIsPlayer"] = When.TargetIsPlayer,
        ["TargetIsNpc"] = When.TargetIsNpc,
        ["EventTargetIsPlayer"] = When.EventTargetIsPlayer,
        ["EventTargetIsNpc"] = When.EventTargetIsNpc,
        ["KilledByPlayer"] = When.KilledByPlayer,
        ["KilledByNpc"] = When.KilledByNpc,
        ["Enemy"] = When.Enemy,
        ["TargetIsEnemy"] = When.TargetIsEnemy,
        ["AttackerIsEnemy"] = When.AttackerIsEnemy,
        ["CasterIsEnemy"] = When.CasterIsEnemy,
        ["MessageParamIsEnemy"] = When.MessageParamIsEnemy,
        ["AttackerFlying"] = When.AttackerFlying,
        ["CasterFlying"] = When.CasterFlying,
        ["EventTargetFlying"] = When.EventTargetFlying,
        ["SeenFlying"] = When.SeenFlying,
        ["Fighting"] = When.Fighting,
        ["Idling"] = When.Idling,
        ["WalkingItsRoute"] = When.WalkingItsRoute,
        ["Casting"] = When.Casting,
        ["AtLastWaypoint"] = When.AtLastWaypoint,
    };

    /// <summary>Retail's npc states, as the extractor names them.</summary>
    private static readonly Dictionary<string, string> States = new()
    {
        ["fight"] = "Fighting",
        ["idle"] = "Idling",
        ["route"] = "WalkingItsRoute",
        ["wander"] = "WanderingAtRandom",
        ["point"] = "MovingToAPoint",
        ["casting"] = "Casting",
    };

    /// <summary>Builds one guard from its stored token.</summary>
    public static PatternCondition Guard(string token)
    {
        (string kind, string argument) = Split(token);
        switch (kind)
        {
            case "timer": return When.Timer(Int(argument, token));
            case "message": return When.Message(Int(argument, token));
            case "chance": return When.Chance(Int(argument, token));
            case "eventskill": return When.EventSkill(Int(argument, token));
            case "skillready": return When.SkillReady(Int(argument, token));
            case "hp_below": return When.HpBelow(Int(argument, token));
            case "set_flag_var": return When.FirstTime(Int(argument, token));
            case "unset_flag_var": return When.Consuming(Int(argument, token));
            case "set_world_flag_var": return When.FirstTimeInWorld(Int(argument, token));
            case "unset_world_flag_var": return When.ConsumingWorld(Int(argument, token));
            case "is_world_flag_var": return When.WorldFlagSet(Int(argument, token));

            // Retail's one-based index, carried verbatim; When.AtWaypoint does the conversion.
            case "waypoint": return When.AtWaypoint(Int(argument, token));
            case "last_waypoint": return When.AtLastWaypoint;

            case "hp_between":
            {
                (string low, string high) = Split(argument);
                return When.HpBetween(Int(low, token), Int(high, token));
            }

            // The parser already resolved these roles to their condition names.
            case "who":
            case "enemy": return Named(argument, token);
            case "flying": return Named(argument + "Flying", token);
            case "state": return Named(States.TryGetValue(argument, out string? s) ? s : argument, token);

            case "hp_of":
            {
                (string name, string percent) = Split(argument);
                int value = Int(percent, token);
                return name switch
                {
                    "TargetHpBelow" => When.TargetHpBelow(value),
                    "FriendHpBelow" => When.FriendHpBelow(value),
                    "MessageSenderHpBelow" => When.MessageSenderHpBelow(value),
                    _ => throw Unknown(token),
                };
            }

            case "within":
            {
                (string name, string metres) = Split(argument);
                int value = Int(metres, token);
                return name switch
                {
                    "TargetWithin" => When.TargetWithin(value),
                    "AttackerWithin" => When.AttackerWithin(value),
                    "CasterWithin" => When.CasterWithin(value),
                    "KillerWithin" => When.KillerWithin(value),
                    "MessageParamWithin" => When.MessageParamWithin(value),
                    "MessageSenderWithin" => When.MessageSenderWithin(value),
                    _ => throw Unknown(token),
                };
            }

            case "beyond":
            {
                (string name, string metres) = Split(argument);
                int value = Int(metres, token);
                return name switch
                {
                    "TargetBeyond" => When.TargetBeyond(value),
                    "AttackerBeyond" => When.AttackerBeyond(value),
                    "CasterBeyond" => When.CasterBeyond(value),
                    "EventTargetBeyond" => When.EventTargetBeyond(value),
                    _ => throw Unknown(token),
                };
            }

            case "race":
            {
                (string name, string race) = Split(argument);
                Race value = Enum.TryParse(race, out Race parsed) ? parsed : throw Unknown(token);
                return name switch
                {
                    "SeenRace" => When.SeenRace(value),
                    "TargetRace" => When.TargetRace(value),
                    "AttackerRace" => When.AttackerRace(value),
                    "CasterRace" => When.CasterRace(value),
                    "KillerRace" => When.KillerRace(value),
                    "TalkerRace" => When.TalkerRace(value),
                    "EventTargetRace" => When.EventTargetRace(value),
                    "MessageParamRace" => When.MessageParamRace(value),
                    "FriendRace" => When.FriendRace(value),

                    // Already emittable and already in the engine, and this switch could not answer
                    // it. Nothing in the tables reaches it today, so it cost nothing so far -- but an
                    // unreadable token refuses the whole file, so the first live one would have taken
                    // every pattern down with it rather than one branch.
                    "MessageSenderRace" => When.MessageSenderRace(value),
                    _ => throw Unknown(token),
                };
            }

            case "class":
            {
                (string name, string classes) = Split(argument);
                PlayerClass[] listed = classes.Split('+')
                    .Select(c => Enum.TryParse(c, out PlayerClass p) ? p : throw Unknown(token))
                    .ToArray();
                return name switch
                {
                    "SeenClass" => When.SeenClass(listed),
                    "AttackerClass" => When.AttackerClass(listed),
                    "CasterClass" => When.CasterClass(listed),
                    "EventTargetClass" => When.EventTargetClass(listed),
                    "TalkerClass" => When.TalkerClass(listed),
                    _ => throw Unknown(token),
                };
            }

            case "skillcategory":
            {
                SkillCategory category = Enum.TryParse(argument, out SkillCategory parsed)
                    ? parsed : throw Unknown(token);
                return When.EventSkillCategory(category);
            }

            case "abnormal":
            {
                (string name, string state) = Split(argument);
                AbnormalState value = Enum.TryParse(state, out AbnormalState parsed)
                    ? parsed : throw Unknown(token);
                return name switch
                {
                    "InAbnormalState" => When.InAbnormalState(value),
                    "TargetInAbnormalState" => When.TargetInAbnormalState(value),
                    "SeenInAbnormalState" => When.SeenInAbnormalState(value),
                    "AttackerInAbnormalState" => When.AttackerInAbnormalState(value),
                    "FriendInAbnormalState" => When.FriendInAbnormalState(value),
                    _ => throw Unknown(token),
                };
            }

            case "countbelow":
            case "countabove":
            {
                // Retail's `set_intvar_if_less_than` / `set_intvar_if_larger_than`: compare, and on a
                // pass put `setTo` in the counter.
                string[] parts = argument.Split(':');
                if (parts.Length != 3) throw Unknown(token);
                int counter = Int(parts[0], token);
                int comparand = Int(parts[1], token);
                int setTo = Int(parts[2], token);
                return kind == "countbelow"
                    ? When.CountBelow(counter, comparand, setTo)
                    : When.CountAbove(counter, comparand, setTo);
            }

            case "decrement":
            {
                // Retail's `decrease_intvar`, the always-passing variant. The extractor refuses the
                // other one rather than guess at it.
                string[] parts = argument.Split(':');
                if (parts.Length != 3) throw Unknown(token);
                return When.Decrement(Int(parts[0], token), Int(parts[1], token), Int(parts[2], token));
            }

            case "countby":
            {
                string[] parts = argument.Split(':');
                if (parts.Length != 5) throw Unknown(token);
                return When.CountingBy(Int(parts[0], token), Int(parts[1], token), Int(parts[2], token),
                    Int(parts[3], token), parts[4] == "1");
            }

            case "count":
            {
                string[] parts = argument.Split(':');
                if (parts.Length != 4) throw Unknown(token);
                return When.Counting(Int(parts[0], token), Int(parts[1], token), Int(parts[2], token),
                    parts[3] == "1");
            }

            default: throw Unknown(token);
        }
    }

    private static PatternCondition Named(string name, string token) =>
        Plain.TryGetValue(name, out PatternCondition? found) ? found : throw Unknown(token);

    // ---- actions ---------------------------------------------------------------------------------

    /// <summary>Builds one action from its stored row.</summary>
    public static PatternAction Action(in ActionRow row)
    {
        string kind = row.Kind;

        // Three kinds carry the resolved member name in the kind itself, which is how the extractor
        // records which role an action names.
        if (kind.StartsWith("hate_at:", StringComparison.Ordinal))
        {
            return HateAt(kind["hate_at:".Length..], Int(row.A1, kind));
        }

        if (kind.StartsWith("switch_to:", StringComparison.Ordinal))
        {
            return SwitchTo(kind["switch_to:".Length..], kind);
        }

        if (kind.StartsWith("flee_", StringComparison.Ordinal))
        {
            return FleeAs(kind["flee_".Length..], Int(row.A1, kind));
        }

        switch (kind)
        {
            case "spawn": return Spawn(row);
            case "skill": return Do.SkillOn(Target(row.Place, kind), Int(row.A1, kind));
            case "skill_now": return Do.SkillOnSelfNow(Int(row.A1, kind));
            case "skill_in_reach": return Do.SkillOnRankedInReach(Ranked(row.Place, kind), Int(row.A1, kind));
            case "skill_at": return SkillAt(row.Place, Int(row.A1, kind), kind);
            case "skill_at_now": return SkillAtNow(row.Place, Int(row.A1, kind), kind);
            case "arm": return Do.ArmTimer(Int(row.A1, kind), Int(row.A2, kind));
            case "despawn": return Do.Despawn(Int(row.A1, kind));
            case "despawn_self": return Do.DespawnSelf();
            case "switch_to": return Do.SwitchTarget(Ranked(row.Place, kind));
            case "switch": return Do.TargetMessageParam();
            case "hate": return Do.HateMessageParam(Int(row.A1, kind));
            case "attack": return Do.AttackMostHating();
            case "timer": return Do.SetIdleTimer(Int(row.A1, kind));
            case "waypoint": return Do.GotoWaypoint(Int(row.A1, kind));
            case "waypoint_run": return Do.GotoWaypointRunning(Int(row.A1, kind));
            case "next_waypoint": return Do.ContinueRoute();
            case "next_waypoint_run": return Do.ContinueRouteRunning();
            case "reset_hate": return Do.ResetHate();
            case "reset_hate_top": return Do.ResetHateExceptTop();
            case "nothing": return Do.Nothing();
            case "say": return Do.Say(Int(row.A1, kind), Int(row.A2, kind));
            case "sysmsg": return Do.SystemMessage(Int(row.A1, kind), Int(row.A2, kind));
            case "var": return Do.SetSpawnVariable(row.Place, Int(row.A1, kind), Int(row.A2, kind));
            case "broadcast": return Do.Broadcast(Int(row.A1, kind), Float(row.A2, kind));

            case "spawn_on_ranked":
                return Do.SpawnOnAttacker(Ranked(row.Place, kind), Int(row.A1, kind), Group(row.Group, kind),
                    Float(row.X, kind), (int)Float(row.Z, kind), Int(row.A2, kind), Float(row.Y, kind));

            case "spawn_each":
                return Do.SpawnOnEachTarget(Int(row.A1, kind), Group(row.Group, kind), Float(row.X, kind),
                    Int(row.A2, kind), Order(row.Place, kind), Float(row.Y, kind),
                    (int)Float(row.Z, kind), Int(row.A3, kind));

            case "spawn_on_target":
                return Do.SpawnOnTarget(Int(row.A1, kind), Group(row.Group, kind), Int(row.A2, kind),
                    Float(row.X, kind), Int(row.A3, kind), (int)Float(row.Z, kind), Float(row.Y, kind));

            case "spawn_near":
                return Do.SpawnNear(Int(row.A1, kind), Group(row.Group, kind), Int(row.A2, kind),
                    Float(row.X, kind), Int(row.A3, kind));

            default: throw Unknown(kind);
        }
    }

    /// <summary>
    /// <c>spawn</c>, which carries retail's placement in <c>place</c>.
    /// </summary>
    /// <remarks>
    /// <c>for_the_fight_</c> is retail's <c>despawn_at_attack_state</c>: the add goes when the fight
    /// does. It prefixes the placement rather than being a column of its own, so it is stripped here
    /// exactly as the emitter stripped it.
    /// </remarks>
    private static PatternAction Spawn(in ActionRow row)
    {
        string place = row.Place;
        bool forTheFight = place.StartsWith("for_the_fight_", StringComparison.Ordinal);
        if (forTheFight)
        {
            place = place["for_the_fight_".Length..];
        }

        int npc = Int(row.A1, row.Kind);
        int count = Int(row.A2, row.Kind);
        int live = Int(row.A3, row.Kind);
        int group = Group(row.Group, row.Kind);
        float x = Float(row.X, row.Kind);
        float y = Float(row.Y, row.Kind);
        float z = Float(row.Z, row.Kind);

        if (place == "self")
        {
            return forTheFight
                ? Do.SpawnNearForTheFight(npc, group, count, 0f, live)
                : Do.SpawnNear(npc, group, count, 0f, live);
        }

        if (place == "offset")
        {
            return forTheFight
                ? Do.SpawnOffsetForTheFight(npc, group, x, y, live, z)
                : Do.SpawnOffset(npc, group, x, y, live, z);
        }

        SpawnSpot[] spots = Enumerable.Range(0, Math.Max(1, count))
            .Select(_ => new SpawnSpot(x, y, z)).ToArray();
        return forTheFight
            ? Do.SpawnAtForTheFight(npc, group, live, spots)
            : Do.SpawnAt(npc, group, live, spots);
    }

    private static PatternAction HateAt(string name, int points) => name switch
    {
        "HateTarget" => Do.HateTarget(points),
        "HateAttacker" => Do.HateAttacker(points),
        "HateCaster" => Do.HateCaster(points),
        "HateSeen" => Do.HateSeen(points),
        "HateEventTarget" => Do.HateEventTarget(points),
        "HateMessageParam" => Do.HateMessageParam(points),
        "HateMessageSender" => Do.HateMessageSender(points),
        _ => throw Unknown("hate_at:" + name),
    };

    private static PatternAction SwitchTo(string name, string kind) => name switch
    {
        "TargetAttacker" => Do.TargetAttacker(),
        "TargetCaster" => Do.TargetCaster(),
        "TargetKiller" => Do.TargetKiller(),
        "TargetSeen" => Do.TargetSeen(),
        "TargetMessageSender" => Do.TargetMessageSender(),
        _ => throw Unknown(kind),
    };

    private static PatternAction FleeAs(string name, int seconds) => name switch
    {
        "Flee" => Do.Flee(seconds),
        "FleeFromAttacker" => Do.FleeFromAttacker(seconds),
        "FleeFromCaster" => Do.FleeFromCaster(seconds),
        "FleeFromSeen" => Do.FleeFromSeen(seconds),
        "FleeFromTalker" => Do.FleeFromTalker(seconds),
        "FleeFromEventTarget" => Do.FleeFromEventTarget(seconds),
        "FleeFromMessageParam" => Do.FleeFromMessageParam(seconds),
        "FleeFromFriendsAttacker" => Do.FleeFromFriendsAttacker(seconds),
        _ => throw Unknown("flee_" + name),
    };

    private static PatternAction SkillAt(string place, int skillId, string kind) => place switch
    {
        "Attacker" => Do.SkillOnAttacker(skillId),
        "Caster" => Do.SkillOnCaster(skillId),
        "EventTarget" => Do.SkillOnEventTarget(skillId),
        "MessageParam" => Do.SkillOnMessageParam(skillId),
        "MessageSender" => Do.SkillOnMessageSender(skillId),
        "Seen" => Do.SkillOnSeen(skillId),
        "FriendsKiller" => Do.SkillOnFriendsKiller(skillId),
        _ => throw Unknown(kind + " at " + place),
    };

    /// <summary>The immediate form, for a branch that casts and then removes the caster.</summary>
    /// <remarks>
    /// The queue drains only while the NPC has a target and still exists, so a queued cast in a
    /// branch that also despawns is a cast that never happens. The extractor decides which rows need
    /// this; see its hazard rule.
    /// </remarks>
    private static PatternAction SkillAtNow(string place, int skillId, string kind) => place switch
    {
        "Seen" => Do.SkillOnSeenNow(skillId),
        _ => throw Unknown(kind + " at " + place),
    };

    // ---- parsing ---------------------------------------------------------------------------------

    private static (string, string) Split(string value)
    {
        int at = value.IndexOf(':');
        return at < 0 ? (value, string.Empty) : (value[..at], value[(at + 1)..]);
    }

    private static NpcSkillTargetAttribute Target(string place, string kind) =>
        Enum.TryParse(place, out NpcSkillTargetAttribute parsed) ? parsed : throw Unknown(kind + " at " + place);

    private static AggroTarget Ranked(string place, string kind) =>
        Enum.TryParse(place, out AggroTarget parsed) ? parsed : throw Unknown(kind + " at " + place);

    private static MultiTargetOrder Order(string place, string kind) =>
        Enum.TryParse(place, out MultiTargetOrder parsed) ? parsed : throw Unknown(kind + " in " + place);

    /// <summary>The spawn group, which retail calls <c>SPAWN_ID_NONE</c> when a rung does not track
    /// what it placed.</summary>
    /// <remarks>
    /// The idle tables carry no group column at all: nothing in them refers back to what it spawned, so
    /// every one of their spawns is untracked. An absent group is that, and is the only field allowed to
    /// be missing -- everything else stays strict, because a number that quietly became zero is exactly
    /// the silent wrongness this loader exists to prevent.
    /// </remarks>
    private static int Group(string value, string context) =>
        string.IsNullOrEmpty(value) ? 0 : Int(value, context);

    private static int Int(string value, string context) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : throw new PatternTableFormatException($"'{value}' is not a number, in '{context}'");

    private static float Float(string value, string context) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
            ? parsed
            : throw new PatternTableFormatException($"'{value}' is not a number, in '{context}'");

    private static PatternTableFormatException Unknown(string token) =>
        new($"the pattern tables use '{token}', which this port has no translation for. "
            + "Add it to PatternTableLoader, or teach the extractor to refuse it.");
}

/// <summary>Thrown when a stored pattern table says something this port cannot translate.</summary>
/// <remarks>
/// Deliberately fatal. The tables used to be C#, so an untranslatable token could not get past the
/// compiler; now that they are data, this is what stands in its place.
/// </remarks>
public sealed class PatternTableFormatException : Exception
{
    public PatternTableFormatException(string message)
        : base(message)
    {
    }
}
