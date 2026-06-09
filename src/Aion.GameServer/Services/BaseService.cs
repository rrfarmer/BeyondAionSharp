using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Base;
using Aion.GameServer.Model.Templates.Base;
using Aion.GameServer.Services.Panesterra;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/BaseService (Source, Estrayl).</summary>
public class BaseService
{
    private static readonly ILogger log = NullLogger.Instance;
    private static readonly BaseService INSTANCE = new BaseService();
    private readonly ConcurrentDictionary<int, Base> activeBases = new ConcurrentDictionary<int, Base>();
    private readonly Dictionary<int, BaseLocation> allBaseLocations = new Dictionary<int, BaseLocation>();

    /// <summary>Initializes all base locations.</summary>
    private BaseService()
    {
        log.LogInformation("Initializing bases...");

        foreach (BaseTemplate template in DataManager.BASE_DATA.GetAllBaseTemplates())
        {
            BaseLocation loc = template.GetType_() switch
            {
                BaseType.CASUAL => new BaseLocation(template),
                BaseType.SIEGE => new SiegeBaseLocation(template),
                BaseType.STAINED => new StainedBaseLocation(template),
                BaseType.PANESTERRA or BaseType.PANESTERRA_ARTIFACT or BaseType.PANESTERRA_FACTION_CAMP => new PanesterraBaseLocation(template),
                _ => throw new ArgumentOutOfRangeException(),
            };
            allBaseLocations[template.GetId()] = loc;
        }
    }

    /// <summary>Executes start of all casual and stained bases.</summary>
    public void InitBases()
    {
        foreach (BaseLocation loc in allBaseLocations.Values)
        {
            switch (loc.GetType_())
            {
                case BaseType.CASUAL:
                case BaseType.STAINED:
                case BaseType.PANESTERRA:
                case BaseType.PANESTERRA_FACTION_CAMP:
                    Start(loc.GetId());
                    break;
            }
        }
    }

    /// <summary>Generates a new BaseObject for given id.</summary>
    public void Start(int id)
    {
        Base @base;

        lock (this)
        {
            if (activeBases.ContainsKey(id))
                return;
            @base = NewBase(id);
            activeBases[id] = @base;
        }
        try
        {
            @base.Start();
        }
        catch (Exception e) when (e is BaseException || e is NullReferenceException)
        {
            log.LogError(e, "Base could not be started! ID:{Id}", id);
        }
    }

    /// <summary>
    /// Removes base with given id from activeBases and stops it. Should only directly call for SiegeBases.
    /// </summary>
    public void Stop(int id)
    {
        if (!IsActive(id))
            return;

        Base @base;
        lock (this)
        {
            activeBases.TryRemove(id, out @base);
        }
        if (@base == null || @base.IsStopped())
            return;

        try
        {
            @base.Stop();
        }
        catch (BaseException e)
        {
            log.LogError(e, "Base could not be stopped! ID:{Id}", id);
        }
    }

    /// <returns>A type-specific base object for given id, if a base location is given for the specific id.</returns>
    private Base NewBase(int id)
    {
        BaseLocation loc = allBaseLocations[id];
        return loc.GetType_() switch
        {
            BaseType.CASUAL => new CasualBase(loc),
            BaseType.STAINED => new StainedBase((StainedBaseLocation) loc),
            BaseType.SIEGE => new SiegeBase((SiegeBaseLocation) loc),
            BaseType.PANESTERRA => new PanesterraBase((PanesterraBaseLocation) loc),
            BaseType.PANESTERRA_ARTIFACT => new PanesterraArtifact((PanesterraBaseLocation) loc),
            BaseType.PANESTERRA_FACTION_CAMP => new PanesterraFactionCamp((PanesterraBaseLocation) loc),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    public void Capture(int id, BaseOccupier? newOccupier)
    {
        if (!IsActive(id))
            return;

        Stop(id);

        BaseLocation loc = allBaseLocations[id];

        if (loc.GetType_() == BaseType.PANESTERRA_FACTION_CAMP)
            PanesterraService.GetInstance().HandleTeamElimination(loc.GetOccupier().GetPanesterraFaction());

        if (newOccupier != null)
            loc.SetOccupier(newOccupier.Value);

        Start(id);

        if (loc is StainedBaseLocation coloredLoc)
            HandleStainedFeatures(coloredLoc.GetColor(), newOccupier);
    }

    private void HandleStainedFeatures(BaseColorType colorType, BaseOccupier? newOccupier)
    {
        List<StainedBase> spec = activeBases.Values.Where(@base => @base is StainedBase sb && sb.GetColor() == colorType)
            .Select(@base => (StainedBase) @base).ToList();

        long equalBases = spec.Count(@base => @base.GetOccupier() == newOccupier);
        if (newOccupier == BaseOccupier.BALAUR || equalBases < 3)
        {
            spec.ForEach(@base => @base.DeactivateEnhancedSpawns());
        }
        else
        {
            spec.ForEach(@base => @base.ScheduleEnhancedSpawns());
        }
    }

    public ICollection<BaseLocation> GetBaseLocations()
    {
        return allBaseLocations.Values;
    }

    public Base GetActiveBase(int id)
    {
        return activeBases.TryGetValue(id, out Base @base) ? @base : null;
    }

    public BaseLocation GetBaseLocation(int id)
    {
        return allBaseLocations.TryGetValue(id, out BaseLocation loc) ? loc : null;
    }

    public bool IsActive(int id)
    {
        return activeBases.ContainsKey(id);
    }

    public static BaseService GetInstance()
    {
        return INSTANCE;
    }
}
