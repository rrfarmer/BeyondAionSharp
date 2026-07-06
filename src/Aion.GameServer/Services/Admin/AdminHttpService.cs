using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Aion.GameServer.Cache;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Mail;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Collections;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services.Admin;

/// <summary>
/// Small authenticated admin HTTP endpoint (infrastructure boundary — not a Java feature) so the external web
/// portal can deliver mail through the LIVE server via SystemMailService.SendMail instead of writing to the game
/// DB directly. Routing everything through SendMail is what makes an online recipient get the instant
/// SM_MAIL_SERVICE + STR_POSTMAN_NOTIFY (bell + icon) and keeps the in-memory mailbox/counter consistent with the
/// DB. Disabled unless gameserver.admin.api.enabled=true AND a token is set (fail closed).
/// </summary>
public sealed class AdminHttpService : IHostedService
{
    private const string CapabilitiesPath = "/admin/capabilities";
    private const string MailPath = "/admin/express-mail";
    private const string LegacyMailPath = "/admin/express-item-mail";
    private const string ValidateMailPath = "/admin/validate-express-mail";
    private const string ValidateMailBatchPath = "/admin/validate-express-mail-batch";
    private const string MailBatchPath = "/admin/express-mail-batch";
    private const string ValidateItemStoragePath = "/admin/validate-item-storage";
    private const string OnlinePlayersPath = "/admin/online-players";
    private const string AccountStatePath = "/admin/account-state";
    private const string PlayerStatePath = "/admin/player-state";
    private const string PlayerStorageStatePath = "/admin/player-storage-state";
    private const string NotifyPlayerPath = "/admin/notify-player";
    private const string KickPlayerPath = "/admin/kick-player";
    private const string MoveToBindPointPath = "/admin/move-to-bind-point";
    private const string MoveToInstanceExitPath = "/admin/move-to-instance-exit";
    private const string UnstuckPlayerPath = "/admin/unstuck-player";
    private const string RefreshMailboxPath = "/admin/refresh-mailbox";
    private const string RefreshInventoryPath = "/admin/refresh-inventory";
    private const string RefreshWarehousePath = "/admin/refresh-warehouse";
    private const string RefreshAccountWarehousePath = "/admin/refresh-account-warehouse";
    private const string ValidatePlayerItemActionPath = "/admin/validate-player-item-action";
    private const string DiscardPlayerItemPath = "/admin/discard-player-item";
    private const string RepairItemSlotPath = "/admin/repair-item-slot";
    private const string RepairItemCountPath = "/admin/repair-item-count";
    private const string ReloadCachePath = "/admin/reload-cache";
    private const string BroadcastMessagePath = "/admin/broadcast-message";
    private const string MaintenanceWarningPath = "/admin/maintenance-warning";
    private static readonly string[] ReloadCacheTargets = new[] { "announcements", "html", "item-restrictions" };
    private static readonly string[] MessageScopes = new[] { "all", "elyos", "asmodians" };
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly GameServerOptions _options;
    private readonly ILogger<AdminHttpService> _logger;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public AdminHttpService(GameServerOptions options, ILogger<AdminHttpService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var cfg = _options.AdminApi;
        if (!cfg.Enabled)
            return Task.CompletedTask;

        if (string.IsNullOrWhiteSpace(cfg.Token))
        {
            _logger.LogWarning(
                "Admin API is enabled but no token is set (gameserver.admin.api.token / GAMESERVER_ADMIN_API_TOKEN); refusing to start the endpoint.");
            return Task.CompletedTask;
        }

        var prefix = $"http://{cfg.BindHost}:{cfg.Port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(prefix);
        try
        {
            _listener.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start admin HTTP endpoint on {Prefix}", prefix);
            _listener = null;
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = Task.Run(() => AcceptLoopAsync(_cts.Token));
        _logger.LogInformation(
            "Admin HTTP endpoint listening on {Prefix} (GET {CapabilitiesPath}, POST {MailPath}, POST {LegacyMailPath}, POST {ValidateMailPath}, POST {ValidateMailBatchPath}, POST {MailBatchPath}, POST {ValidateItemStoragePath}, GET {OnlinePlayersPath}, GET {AccountStatePath}, GET {PlayerStatePath}, GET {PlayerStorageStatePath}, POST {NotifyPlayerPath}, POST {KickPlayerPath}, POST {MoveToBindPointPath}, POST {MoveToInstanceExitPath}, POST {UnstuckPlayerPath}, POST {RefreshMailboxPath}, POST {RefreshInventoryPath}, POST {RefreshWarehousePath}, POST {RefreshAccountWarehousePath}, POST {ValidatePlayerItemActionPath}, POST {DiscardPlayerItemPath}, POST {RepairItemSlotPath}, POST {RepairItemCountPath}, POST {ReloadCachePath}, POST {BroadcastMessagePath}, POST {MaintenanceWarningPath})",
            prefix, CapabilitiesPath, MailPath, LegacyMailPath, ValidateMailPath, ValidateMailBatchPath, MailBatchPath, ValidateItemStoragePath, OnlinePlayersPath, AccountStatePath, PlayerStatePath, PlayerStorageStatePath, NotifyPlayerPath, KickPlayerPath, MoveToBindPointPath, MoveToInstanceExitPath, UnstuckPlayerPath, RefreshMailboxPath, RefreshInventoryPath, RefreshWarehousePath, RefreshAccountWarehousePath, ValidatePlayerItemActionPath, DiscardPlayerItemPath, RepairItemSlotPath, RepairItemCountPath, ReloadCachePath, BroadcastMessagePath, MaintenanceWarningPath);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        try
        {
            _listener?.Stop();
            _listener?.Close();
        }
        catch
        {
            // ignore shutdown races
        }

        if (_loop != null)
        {
            try
            {
                await _loop.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch
            {
                // best-effort drain
            }
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        var listener = _listener!;
        while (!ct.IsCancellationRequested && listener.IsListening)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await listener.GetContextAsync();
            }
            catch when (ct.IsCancellationRequested || !listener.IsListening)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Admin HTTP accept failed");
                continue;
            }

            _ = Task.Run(() => HandleAsync(ctx));
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        try
        {
            var req = ctx.Request;

            if (!FixedTimeTokenEquals(req.Headers["X-Admin-Token"], _options.AdminApi.Token))
            {
                await WriteJsonAsync(ctx, 401, new { ok = false, error = "Unauthorized." });
                return;
            }

            string path = req.Url?.AbsolutePath?.TrimEnd('/') ?? "";
            if (path.Length == 0)
                path = "/";

            if (string.Equals(req.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, CapabilitiesPath, StringComparison.Ordinal))
            {
                await HandleCapabilitiesAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, OnlinePlayersPath, StringComparison.Ordinal))
            {
                await HandleOnlinePlayersAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, AccountStatePath, StringComparison.Ordinal))
            {
                await HandleAccountStateAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, PlayerStatePath, StringComparison.Ordinal))
            {
                await HandlePlayerStateAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, PlayerStorageStatePath, StringComparison.Ordinal))
            {
                await HandlePlayerStorageStateAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(path, MailPath, StringComparison.Ordinal)
                    || string.Equals(path, LegacyMailPath, StringComparison.Ordinal)))
            {
                await HandleExpressMailAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, ValidateMailPath, StringComparison.Ordinal))
            {
                await HandleValidateExpressMailAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, ValidateMailBatchPath, StringComparison.Ordinal))
            {
                await HandleValidateExpressMailBatchAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, MailBatchPath, StringComparison.Ordinal))
            {
                await HandleExpressMailBatchAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, ValidateItemStoragePath, StringComparison.Ordinal))
            {
                await HandleValidateItemStorageAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, NotifyPlayerPath, StringComparison.Ordinal))
            {
                await HandleNotifyPlayerAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, KickPlayerPath, StringComparison.Ordinal))
            {
                await HandleKickPlayerAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, MoveToBindPointPath, StringComparison.Ordinal))
            {
                await HandleMoveToBindPointAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, MoveToInstanceExitPath, StringComparison.Ordinal))
            {
                await HandleMoveToInstanceExitAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, UnstuckPlayerPath, StringComparison.Ordinal))
            {
                await HandleUnstuckPlayerAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, RefreshMailboxPath, StringComparison.Ordinal))
            {
                await HandleRefreshMailboxAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, RefreshInventoryPath, StringComparison.Ordinal))
            {
                await HandleRefreshInventoryAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, RefreshWarehousePath, StringComparison.Ordinal))
            {
                await HandleRefreshWarehouseAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, RefreshAccountWarehousePath, StringComparison.Ordinal))
            {
                await HandleRefreshAccountWarehouseAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, ValidatePlayerItemActionPath, StringComparison.Ordinal))
            {
                await HandleValidatePlayerItemActionAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, DiscardPlayerItemPath, StringComparison.Ordinal))
            {
                await HandleDiscardPlayerItemAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, RepairItemSlotPath, StringComparison.Ordinal))
            {
                await HandleRepairItemSlotAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, RepairItemCountPath, StringComparison.Ordinal))
            {
                await HandleRepairItemCountAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, ReloadCachePath, StringComparison.Ordinal))
            {
                await HandleReloadCacheAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, BroadcastMessagePath, StringComparison.Ordinal))
            {
                await HandleBroadcastMessageAsync(ctx);
                return;
            }

            if (string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                && string.Equals(path, MaintenanceWarningPath, StringComparison.Ordinal))
            {
                await HandleMaintenanceWarningAsync(ctx);
                return;
            }

            await WriteJsonAsync(ctx, 404, new { ok = false, error = "Not found." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin HTTP handler error");
            try
            {
                await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
            }
            catch
            {
                // client gone
            }
        }
    }

    private async Task HandleValidateExpressMailAsync(HttpListenerContext ctx)
    {
        try
        {
            var dto = await ReadAdminMailRequestAsync(ctx);
            if (dto == null)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Invalid JSON body." });
                return;
            }

            AdminMailValidation validation = ValidateExpressMail(dto);
            await WriteJsonAsync(ctx, 200, MailValidationPayload(validation, true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin validate-express-mail handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private async Task HandleValidateExpressMailBatchAsync(HttpListenerContext ctx)
    {
        try
        {
            AdminMailBatchValidationRequest? dto = await ReadJsonAsync<AdminMailBatchValidationRequest>(ctx);
            if (dto == null)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Invalid JSON body." });
                return;
            }

            AdminMailBatchValidation validation = ValidateExpressMailBatch(dto);
            await WriteJsonAsync(ctx, 200, MailBatchValidationPayload(validation, true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin validate-express-mail-batch handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private async Task HandleExpressMailBatchAsync(HttpListenerContext ctx)
    {
        try
        {
            AdminMailBatchValidationRequest? dto = await ReadJsonAsync<AdminMailBatchValidationRequest>(ctx);
            if (dto == null)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Invalid JSON body." });
                return;
            }

            AdminMailBatchValidation batch = ValidateExpressMailBatch(dto);
            if (!batch.Valid)
            {
                await WriteMailBatchValidationFailureAsync(ctx, batch);
                return;
            }

            var sentEntries = new List<object>();
            for (int i = 0; i < batch.Entries.Count; i++)
            {
                AdminMailBatchEntryValidation entryValidation = batch.Entries[i];
                AdminMailBatchEntryRequest entry = dto.Entries![i];
                bool ok = SystemMailService.SendMail(
                    batch.SenderName,
                    batch.RecipientName!,
                    batch.Title,
                    batch.Message,
                    entry.ItemId,
                    entry.ItemCount,
                    entry.Kinah,
                    LetterType.EXPRESS);
                if (!ok)
                {
                    _logger.LogWarning("Admin API: express mail batch rejected by SystemMailService after {SentCount}/{EntryCount} entries -> {Recipient}",
                        sentEntries.Count, batch.Entries.Count, batch.RecipientName);
                    var failedEntry = new
                    {
                        index = entryValidation.Index,
                        itemId = entryValidation.ItemId,
                        itemCount = entryValidation.ItemCount,
                        kinah = entryValidation.Kinah,
                        itemName = entryValidation.ItemName
                    };
                    await WriteJsonAsync(ctx, 422, MailBatchSendPayload(
                        batch,
                        false,
                        sentEntries,
                        $"Delivery rejected by SystemMailService after {sentEntries.Count}/{batch.Entries.Count} letters.",
                        failedEntry,
                        new[] { "Delivery rejected by SystemMailService (mailbox full, unknown item id, invalid recipient, or persistence failure)." }));
                    return;
                }

                sentEntries.Add(new
                {
                    index = entryValidation.Index,
                    itemId = entryValidation.ItemId,
                    itemCount = entryValidation.ItemCount,
                    kinah = entryValidation.Kinah,
                    itemName = entryValidation.ItemName
                });
            }

            _logger.LogInformation("Admin API: express mail batch entries={EntryCount} kinahTotal={KinahTotal} -> {Recipient} ({Delivery})",
                batch.Entries.Count, batch.KinahTotal, batch.RecipientName, batch.Online ? "online-notified" : "offline");
            await WriteJsonAsync(ctx, 200, MailBatchSendPayload(batch, true, sentEntries));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin express mail batch handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private static async Task WriteMailBatchValidationFailureAsync(HttpListenerContext ctx, AdminMailBatchValidation validation)
    {
        string error = validation.Errors.Count > 0
            ? string.Join(" ", validation.Errors)
            : string.Join(" ", validation.Entries.SelectMany(entry => entry.Errors).Where(entryError => !string.IsNullOrWhiteSpace(entryError)));
        if (string.IsNullOrWhiteSpace(error))
            error = "Express mail bundle validation failed.";

        await WriteJsonAsync(ctx, MailBatchValidationStatus(validation), MailBatchValidationPayload(validation, false, error));
    }

    private static object MailBatchValidationPayload(AdminMailBatchValidation validation, bool ok, string? error = null)
    {
        return new
        {
            ok,
            valid = validation.Valid,
            error,
            recipientCharacterId = validation.RecipientCharacterId,
            recipientName = validation.RecipientName,
            online = validation.Online,
            delivered = validation.Online ? "online" : "offline",
            mailboxLetters = validation.MailboxLetters,
            mailboxLimit = validation.MailboxLimit,
            entryCount = validation.Entries.Count,
            validEntryCount = validation.ValidEntryCount,
            kinahTotal = validation.KinahTotal,
            kinahMaxAttachment = validation.KinahMaxAttachment,
            kinahCapEnabled = validation.KinahCapEnabled,
            kinahCapValue = validation.KinahCapValue,
            recipientKinah = validation.RecipientKinah,
            kinahWouldExceedCap = validation.KinahWouldExceedCap,
            errors = validation.Errors,
            warnings = validation.Warnings,
            entries = validation.Entries.Select(entry => new
            {
                index = entry.Index,
                valid = entry.Valid,
                itemId = entry.ItemId,
                itemCount = entry.ItemCount,
                kinah = entry.Kinah,
                itemName = entry.ItemName,
                itemMaxStackCount = entry.ItemMaxStackCount,
                errors = entry.Errors,
                warnings = entry.Warnings
            }).ToList()
        };
    }

    private static object MailBatchSendPayload(
        AdminMailBatchValidation validation,
        bool ok,
        IReadOnlyCollection<object> sentEntries,
        string? error = null,
        object? failedEntry = null,
        IEnumerable<string>? extraErrors = null)
    {
        var errors = extraErrors == null
            ? validation.Errors
            : validation.Errors.Concat(extraErrors).Where(entry => !string.IsNullOrWhiteSpace(entry)).ToList();

        return new
        {
            ok,
            valid = validation.Valid,
            error,
            recipientCharacterId = validation.RecipientCharacterId,
            recipientName = validation.RecipientName,
            online = validation.Online,
            delivered = validation.Online ? "online" : "offline",
            mailboxLetters = validation.MailboxLetters,
            mailboxLimit = validation.MailboxLimit,
            entryCount = validation.Entries.Count,
            validEntryCount = validation.ValidEntryCount,
            sentCount = sentEntries.Count,
            sentEntries,
            failedEntry,
            kinahTotal = validation.KinahTotal,
            kinahMaxAttachment = validation.KinahMaxAttachment,
            kinahCapEnabled = validation.KinahCapEnabled,
            kinahCapValue = validation.KinahCapValue,
            recipientKinah = validation.RecipientKinah,
            kinahWouldExceedCap = validation.KinahWouldExceedCap,
            errors,
            warnings = validation.Warnings,
            entries = validation.Entries.Select(entry => new
            {
                index = entry.Index,
                valid = entry.Valid,
                itemId = entry.ItemId,
                itemCount = entry.ItemCount,
                kinah = entry.Kinah,
                itemName = entry.ItemName,
                itemMaxStackCount = entry.ItemMaxStackCount,
                errors = entry.Errors,
                warnings = entry.Warnings
            }).ToList()
        };
    }

    private static int MailBatchValidationStatus(AdminMailBatchValidation validation)
    {
        var errors = validation.Errors.Concat(validation.Entries.SelectMany(entry => entry.Errors)).ToList();
        if (errors.Any(error => error.Contains("not found", StringComparison.OrdinalIgnoreCase)))
            return 404;
        if (errors.Any(error =>
                error.Contains("mailbox", StringComparison.OrdinalIgnoreCase)
                || error.Contains("exceed", StringComparison.OrdinalIgnoreCase)))
            return 422;
        return 400;
    }

    private async Task HandleValidateItemStorageAsync(HttpListenerContext ctx)
    {
        try
        {
            AdminItemStorageValidationRequest? dto = await ReadJsonAsync<AdminItemStorageValidationRequest>(ctx);
            if (dto == null)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Invalid JSON body." });
                return;
            }

            AdminItemStorageValidation validation = ValidateItemStorage(dto);
            await WriteJsonAsync(ctx, 200, ItemStorageValidationPayload(validation, true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin validate-item-storage handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private static object ItemStorageValidationPayload(
        AdminItemStorageValidation validation,
        bool ok,
        string? error = null)
    {
        return new
        {
            ok,
            valid = validation.Valid,
            error,
            itemId = validation.ItemId,
            itemName = validation.ItemName,
            itemMask = validation.ItemMask,
            itemQuality = validation.ItemQuality,
            itemType = validation.ItemType,
            itemGroup = validation.ItemGroup,
            maxStackCount = validation.MaxStackCount,
            kinah = validation.Kinah,
            limitOne = validation.LimitOne,
            canSplit = validation.CanSplit,
            breakable = validation.Breakable,
            deletable = validation.Deletable,
            itemCount = validation.ItemCount,
            countAllowed = validation.CountAllowed,
            currentStorageId = validation.CurrentStorageId,
            currentSlot = validation.CurrentSlot,
            currentStorageLimit = validation.CurrentStorageLimit,
            slotAllowed = validation.SlotAllowed,
            rowSoulBound = validation.RowSoulBound,
            templateSoulBound = validation.TemplateSoulBound,
            effectiveSoulBound = validation.EffectiveSoulBound,
            tradeable = validation.Tradeable,
            storableInCharacterWarehouse = validation.StorableInCharacterWarehouse,
            storableInAccountWarehouse = validation.StorableInAccountWarehouse,
            targetPolicy = validation.TargetPolicy,
            targetAllowed = validation.TargetAllowed,
            errors = validation.Errors,
            warnings = validation.Warnings
        };
    }

    private async Task HandleExpressMailAsync(HttpListenerContext ctx)
    {
        try
        {
            var dto = await ReadAdminMailRequestAsync(ctx);
            if (dto == null)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Invalid JSON body." });
                return;
            }

            AdminMailValidation validation = ValidateExpressMail(dto);
            if (!validation.Valid)
            {
                await WriteMailValidationFailureAsync(ctx, validation);
                return;
            }

            bool ok = SystemMailService.SendMail(
                validation.SenderName,
                validation.RecipientName!,
                validation.Title,
                validation.Message,
                validation.ItemId,
                validation.ItemCount,
                validation.Kinah,
                LetterType.EXPRESS);
            if (!ok)
            {
                await WriteJsonAsync(ctx, 422,
                    MailValidationPayload(
                        validation,
                        false,
                        "Delivery rejected by SystemMailService.",
                        new[] { "Delivery rejected by SystemMailService (mailbox full, unknown item id, or invalid recipient)." }));
                return;
            }

            _logger.LogInformation("Admin API: express mail item={ItemId}x{Count} kinah={Kinah} -> {Recipient} ({Delivery})",
                validation.ItemId, validation.ItemCount, validation.Kinah, validation.RecipientName, validation.Online ? "online-notified" : "offline");
            await WriteJsonAsync(ctx, 200, MailValidationPayload(validation, true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin express mail handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private static async Task WriteMailValidationFailureAsync(HttpListenerContext ctx, AdminMailValidation validation)
    {
        string error = validation.Errors.Count == 0 ? "Express mail validation failed." : string.Join(" ", validation.Errors);
        await WriteJsonAsync(ctx, MailValidationStatus(validation), MailValidationPayload(validation, false, error));
    }

    private static object MailValidationPayload(AdminMailValidation validation, bool ok, string? error = null, IEnumerable<string>? extraErrors = null)
    {
        var errors = extraErrors == null
            ? validation.Errors
            : validation.Errors.Concat(extraErrors).Where(entry => !string.IsNullOrWhiteSpace(entry)).ToList();

        return new
        {
            ok,
            valid = validation.Valid,
            error,
            recipientCharacterId = validation.RecipientCharacterId,
            recipientName = validation.RecipientName,
            online = validation.Online,
            delivered = validation.Online ? "online" : "offline",
            mailboxLetters = validation.MailboxLetters,
            mailboxLimit = validation.MailboxLimit,
            itemId = validation.ItemId,
            itemCount = validation.ItemCount,
            itemName = validation.ItemName,
            itemMaxStackCount = validation.ItemMaxStackCount,
            kinah = validation.Kinah,
            kinahMaxAttachment = validation.KinahMaxAttachment,
            kinahCapEnabled = validation.KinahCapEnabled,
            kinahCapValue = validation.KinahCapValue,
            recipientKinah = validation.RecipientKinah,
            kinahWouldExceedCap = validation.KinahWouldExceedCap,
            errors,
            warnings = validation.Warnings
        };
    }

    private static int MailValidationStatus(AdminMailValidation validation)
    {
        if (validation.RecipientNotFound
            || validation.Errors.Any(error => error.Contains("template", StringComparison.OrdinalIgnoreCase)
                && error.Contains("not found", StringComparison.OrdinalIgnoreCase)))
            return 404;

        if (validation.MailboxFull
            || validation.Errors.Any(error =>
                error.Contains("mailbox", StringComparison.OrdinalIgnoreCase)
                || error.Contains("cap", StringComparison.OrdinalIgnoreCase)
                || error.Contains("above", StringComparison.OrdinalIgnoreCase)
                || error.Contains("exceeds", StringComparison.OrdinalIgnoreCase)))
            return 422;

        return 400;
    }

    private static async Task<AdminMailRequest?> ReadAdminMailRequestAsync(HttpListenerContext ctx)
    {
        string body;
        using (var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding ?? Encoding.UTF8))
            body = await reader.ReadToEndAsync();

        try
        {
            return JsonSerializer.Deserialize<AdminMailRequest>(body, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private static AdminMailValidation ValidateExpressMail(AdminMailRequest dto)
    {
        var validation = new AdminMailValidation();
        validation.ItemId = dto.ItemId;
        validation.ItemCount = dto.ItemCount;
        string? recipientName = string.IsNullOrWhiteSpace(dto.RecipientName) ? null : dto.RecipientName.Trim();

        if (recipientName == null && dto.RecipientCharacterId <= 0)
        {
            validation.Errors.Add("recipientName or recipientCharacterId is required.");
        }

        if (recipientName != null && recipientName.Length > 16)
        {
            validation.Errors.Add("Recipient name must be 16 characters or fewer.");
        }

        PlayerCommonData? recipientCommonData = null;
        if (validation.Errors.Count == 0)
        {
            recipientCommonData = recipientName != null
                ? PlayerService.GetOrLoadPlayerCommonData(recipientName)
                : PlayerService.GetOrLoadPlayerCommonData(dto.RecipientCharacterId);

            if (recipientCommonData == null)
            {
                validation.RecipientNotFound = true;
                validation.Errors.Add("Recipient character was not found.");
            }
            else
            {
                validation.RecipientCharacterId = recipientCommonData.GetPlayerObjId();
                validation.RecipientName = recipientCommonData.GetName();
                validation.MailboxLetters = recipientCommonData.GetMailboxLetters();
                Player? onlineRecipient = GameWorld.GetInstance().GetPlayer(validation.RecipientName);
                validation.Online = onlineRecipient != null;
                validation.RecipientKinah = onlineRecipient?.GetInventory().GetKinah();

                if (validation.MailboxLetters > 199)
                {
                    validation.MailboxFull = true;
                    validation.Errors.Add($"Recipient mailbox is full ({validation.MailboxLetters}/{validation.MailboxLimit} letters).");
                }
            }
        }

        bool hasItemAttachment = dto.ItemId != 0 || dto.ItemCount != 0;
        if (dto.Kinah < 0)
            validation.Errors.Add("kinah cannot be negative.");
        if (dto.Kinah > validation.KinahMaxAttachment)
            validation.Errors.Add($"Kinah attachments above {validation.KinahMaxAttachment} are not safe for the current mail packet format.");
        if (!hasItemAttachment && dto.Kinah <= 0)
            validation.Errors.Add("item attachment or positive kinah is required.");
        if (hasItemAttachment && (dto.ItemId <= 0 || dto.ItemCount <= 0))
            validation.Errors.Add("itemId and itemCount must be positive.");
        ValidateKinahGrant(dto.Kinah, validation);

        if (hasItemAttachment && dto.ItemId > 0)
        {
            var template = DataManager.ITEM_DATA.GetItemTemplate(dto.ItemId);
            if (template == null)
            {
                validation.Errors.Add($"Item template {dto.ItemId} was not found.");
            }
            else
            {
                validation.ItemName = template.GetName();
                validation.ItemMaxStackCount = template.GetMaxStackCount();
                if (template.IsKinah())
                    validation.Errors.Add("Kinah uses the mail kinah field; item mail cannot attach the Kinah item template.");
                else if (dto.ItemCount > validation.ItemMaxStackCount)
                    validation.Errors.Add($"Item count exceeds this template's max stack count of {validation.ItemMaxStackCount}.");
            }
        }

        string sender = string.IsNullOrWhiteSpace(dto.SenderName) ? "Aion Portal" : dto.SenderName.Trim();
        string title = string.IsNullOrWhiteSpace(dto.Title) ? "Admin Delivery" : dto.Title.Trim();
        string message = string.IsNullOrWhiteSpace(dto.Message) ? " " : dto.Message.Trim();

        if (!sender.StartsWith("$$", StringComparison.Ordinal) && sender.Length > 16)
            validation.Errors.Add("Sender must be 16 characters or fewer unless it is a system sender beginning with $$.");
        if (title.Length > 20)
        {
            validation.Warnings.Add("Title is longer than 20 characters and SystemMailService will truncate it.");
            title = title.Substring(0, 20);
        }
        if (message.Length > 1000)
        {
            validation.Warnings.Add("Message is longer than 1000 characters and SystemMailService will truncate it.");
            message = message.Substring(0, 1000);
        }

        validation.SenderName = sender;
        validation.Title = title;
        validation.Message = message;

        return validation;
    }

    private static AdminMailBatchValidation ValidateExpressMailBatch(AdminMailBatchValidationRequest dto)
    {
        var batch = new AdminMailBatchValidation();
        var entries = dto.Entries ?? new List<AdminMailBatchEntryRequest>();
        if (entries.Count == 0)
            batch.Errors.Add("At least one bundle entry is required.");
        if (entries.Count > 20)
            batch.Errors.Add("Mail bundles are limited to 20 letters.");

        long cumulativeKinah = 0;
        int acceptedLetters = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            AdminMailBatchEntryRequest entry = entries[i];
            AdminMailValidation validation = ValidateExpressMail(new AdminMailRequest
            {
                RecipientCharacterId = dto.RecipientCharacterId,
                RecipientName = dto.RecipientName,
                SenderName = dto.SenderName,
                Title = dto.Title,
                Message = dto.Message,
                ItemId = entry.ItemId,
                ItemCount = entry.ItemCount,
                Kinah = entry.Kinah
            });

            if (i == 0 || batch.RecipientCharacterId == 0)
            {
                batch.RecipientCharacterId = validation.RecipientCharacterId;
                batch.RecipientName = validation.RecipientName;
                batch.Online = validation.Online;
                batch.MailboxLetters = validation.MailboxLetters;
                batch.MailboxLimit = validation.MailboxLimit;
                batch.KinahMaxAttachment = validation.KinahMaxAttachment;
                batch.KinahCapEnabled = validation.KinahCapEnabled;
                batch.KinahCapValue = validation.KinahCapValue;
                batch.RecipientKinah = validation.RecipientKinah;
                batch.SenderName = validation.SenderName;
                batch.Title = validation.Title;
                batch.Message = validation.Message;
            }

            var entryValidation = new AdminMailBatchEntryValidation
            {
                Index = i,
                ItemId = entry.ItemId,
                ItemCount = entry.ItemCount,
                Kinah = entry.Kinah,
                ItemName = validation.ItemName,
                ItemMaxStackCount = validation.ItemMaxStackCount
            };
            entryValidation.Errors.AddRange(validation.Errors);
            entryValidation.Warnings.AddRange(validation.Warnings);

            if (validation.Errors.Count == 0 && validation.MailboxLetters + acceptedLetters >= validation.MailboxLimit)
            {
                entryValidation.Errors.Add(
                    $"This bundle would exceed the recipient mailbox limit ({validation.MailboxLetters + acceptedLetters}/{validation.MailboxLimit} letters before entry {i + 1}).");
            }

            long positiveKinah = Math.Max(0, validation.Kinah);
            if (validation.KinahCapEnabled && validation.RecipientKinah.HasValue && positiveKinah > 0)
            {
                long projectedKinah = validation.RecipientKinah.Value + cumulativeKinah + positiveKinah;
                if (projectedKinah > validation.KinahCapValue)
                {
                    batch.KinahWouldExceedCap = true;
                    entryValidation.Warnings.Add(
                        $"Claiming this bundle through entry {i + 1} could exceed the configured Kinah cap of {validation.KinahCapValue}.");
                }
            }

            if (entryValidation.Valid)
            {
                acceptedLetters++;
                cumulativeKinah += positiveKinah;
                batch.ValidEntryCount++;
                batch.KinahTotal += positiveKinah;
            }

            batch.Entries.Add(entryValidation);
        }

        if (batch.KinahWouldExceedCap)
            batch.Warnings.Add("The cumulative Kinah attached by this bundle could exceed the recipient's configured Kinah cap if all letters are claimed.");

        return batch;
    }

    private static void ValidateKinahGrant(long kinah, AdminMailValidation validation)
    {
        validation.Kinah = kinah;
        validation.KinahCapEnabled = Aion.GameServer.Configs.Main.CustomConfig.ENABLE_KINAH_CAP;
        validation.KinahCapValue = Aion.GameServer.Configs.Main.CustomConfig.KINAH_CAP_VALUE;

        if (kinah <= 0)
            return;

        if (validation.KinahCapEnabled)
        {
            if (kinah > validation.KinahCapValue)
            {
                validation.Errors.Add($"Kinah attachment exceeds the configured Kinah cap of {validation.KinahCapValue}.");
            }
            if (validation.RecipientKinah.HasValue && kinah > validation.KinahCapValue - validation.RecipientKinah.Value)
            {
                validation.KinahWouldExceedCap = true;
                validation.Warnings.Add(
                    $"Recipient currently has {validation.RecipientKinah.Value} Kinah; claiming this mail could exceed the configured cap of {validation.KinahCapValue} if they do not spend Kinah first.");
            }
        }
    }

    private static AdminItemStorageValidation ValidateItemStorage(AdminItemStorageValidationRequest dto)
    {
        var validation = new AdminItemStorageValidation
        {
            ItemId = dto.ItemId,
            RowSoulBound = dto.IsSoulBound
        };

        if (dto.ItemId <= 0)
        {
            validation.Errors.Add("itemId must be positive.");
            return validation;
        }

        var template = DataManager.ITEM_DATA.GetItemTemplate(dto.ItemId);
        if (template == null)
        {
            validation.Errors.Add($"Item template {dto.ItemId} was not found.");
            return validation;
        }

        int mask = template.GetMask();
        validation.ItemName = template.GetName();
        validation.ItemMask = mask;
        validation.ItemQuality = template.GetItemQuality().ToString();
        validation.ItemType = template.GetItemType().ToString();
        validation.ItemGroup = template.GetItemGroup().ToString();
        validation.MaxStackCount = template.GetMaxStackCount();
        validation.Kinah = template.IsKinah();
        validation.LimitOne = template.HasLimitOne();
        validation.CanSplit = template.CanSplit();
        validation.Breakable = template.IsBreakable();
        validation.Deletable = template.IsDeletable();
        validation.TemplateSoulBound = template.IsSoulBound();
        validation.EffectiveSoulBound = dto.IsSoulBound || validation.TemplateSoulBound;
        validation.Tradeable = HasMask(mask, ItemMask.TRADEABLE) && !validation.EffectiveSoulBound;
        validation.StorableInCharacterWarehouse = HasMask(mask, ItemMask.STORABLE_IN_WH);
        validation.StorableInAccountWarehouse = HasMask(mask, ItemMask.STORABLE_IN_AWH) && !validation.EffectiveSoulBound;
        validation.TargetPolicy = NormalizeStorageTarget(dto.TargetPolicy, dto.TargetStorageId);
        ValidateItemRowCount(dto, validation);
        ValidateItemRowSlot(dto, validation);

        if (validation.TargetPolicy == "characterWarehouse")
        {
            validation.TargetAllowed = validation.StorableInCharacterWarehouse;
            if (!validation.TargetAllowed.Value)
                validation.Errors.Add("This item cannot be stored in a character warehouse.");
        }
        else if (validation.TargetPolicy == "accountWarehouse")
        {
            validation.TargetAllowed = validation.StorableInAccountWarehouse;
            if (!validation.TargetAllowed.Value)
            {
                validation.Errors.Add(validation.EffectiveSoulBound
                    ? "Soulbound items cannot be stored in an account warehouse."
                    : "This item cannot be stored in an account warehouse.");
            }
        }
        else if (!string.IsNullOrEmpty(validation.TargetPolicy))
        {
            validation.Warnings.Add($"Target policy '{validation.TargetPolicy}' is not recognized by the game-server validator.");
        }

        return validation;
    }

    private static void ValidateItemRowCount(AdminItemStorageValidationRequest dto, AdminItemStorageValidation validation)
    {
        string itemCount = (dto.ItemCount ?? "").Trim();
        if (itemCount.Length == 0)
            return;

        validation.ItemCount = itemCount;
        if (!long.TryParse(itemCount, out long parsedCount))
        {
            validation.CountAllowed = false;
            validation.Errors.Add($"Item count '{itemCount}' is not a valid integer.");
            return;
        }

        if (parsedCount <= 0)
        {
            validation.CountAllowed = false;
            validation.Errors.Add($"Item count must be positive; got {parsedCount}.");
            return;
        }

        if (!validation.Kinah && parsedCount > validation.MaxStackCount)
        {
            validation.CountAllowed = false;
            validation.Errors.Add($"Item count {parsedCount} exceeds this template's max stack count of {validation.MaxStackCount}.");
            return;
        }

        validation.CountAllowed = true;
    }

    private static void ValidateItemRowSlot(AdminItemStorageValidationRequest dto, AdminItemStorageValidation validation)
    {
        string currentSlot = (dto.CurrentSlot ?? "").Trim();
        if (currentSlot.Length == 0 || dto.CurrentStorageLimit <= 0)
            return;

        validation.CurrentStorageId = dto.CurrentStorageId;
        validation.CurrentStorageLimit = dto.CurrentStorageLimit;
        validation.CurrentSlot = currentSlot;

        if (!long.TryParse(currentSlot, out long parsedSlot))
        {
            validation.SlotAllowed = false;
            validation.Errors.Add($"Storage slot '{currentSlot}' is not a valid integer.");
            return;
        }

        if (validation.Kinah && parsedSlot == 65535)
        {
            validation.SlotAllowed = true;
            return;
        }

        if (parsedSlot < 0 || parsedSlot >= dto.CurrentStorageLimit)
        {
            validation.SlotAllowed = false;
            validation.Errors.Add($"Storage slot {parsedSlot} is outside the usable range 0-{dto.CurrentStorageLimit - 1}.");
            return;
        }

        validation.SlotAllowed = true;
    }

    private static bool HasMask(int mask, int flag)
    {
        return (mask & flag) == flag;
    }

    private static string NormalizeStorageTarget(string? targetPolicy, int targetStorageId)
    {
        string normalized = (targetPolicy ?? "").Trim();
        if (!string.IsNullOrEmpty(normalized))
            return normalized;

        return targetStorageId switch
        {
            1 => "characterWarehouse",
            2 => "accountWarehouse",
            120 => "characterWarehouse",
            121 => "accountWarehouse",
            _ => ""
        };
    }

    private async Task HandleCapabilitiesAsync(HttpListenerContext ctx)
    {
        int onlinePlayerCount = GameWorld.GetInstance().GetAllPlayers().Count(player => player != null);
        await WriteJsonAsync(ctx, 200, new
        {
            ok = true,
            at = DateTimeOffset.UtcNow,
            service = nameof(AdminHttpService),
            apiVersion = 2,
            onlinePlayerCount,
            endpoints = new object[]
            {
                new { method = "GET", path = CapabilitiesPath, category = "diagnostics", mutates = false, description = "Returns this admin API capability list." },
                new { method = "POST", path = MailPath, category = "mail", mutates = true, description = "Sends item and/or Kinah express mail through SystemMailService." },
                new { method = "POST", path = LegacyMailPath, category = "mail", mutates = true, deprecated = true, canonicalPath = MailPath, description = "Legacy alias for /admin/express-mail." },
                new { method = "POST", path = ValidateMailPath, category = "mail", mutates = false, description = "Validates express mail delivery without creating mail." },
                new { method = "POST", path = ValidateMailBatchPath, category = "mail", mutates = false, description = "Validates an express mail bundle without creating mail, including cumulative mailbox capacity." },
                new { method = "POST", path = MailBatchPath, category = "mail", mutates = true, description = "Validates and sends an express mail bundle through SystemMailService." },
                new { method = "POST", path = ValidateItemStoragePath, category = "items", mutates = false, description = "Validates live item storage, tradeability, count, and slot rules." },
                new { method = "GET", path = OnlinePlayersPath, category = "players", mutates = false, description = "Lists loaded online players from the running game world." },
                new { method = "GET", path = AccountStatePath, category = "players", mutates = false, description = "Returns loaded live players and account warehouse snapshot for an account." },
                new { method = "GET", path = PlayerStatePath, category = "players", mutates = false, description = "Returns live player state or last-known offline character state." },
                new { method = "GET", path = PlayerStorageStatePath, category = "players", mutates = false, description = "Returns read-only live inventory, warehouse, mailbox, and position snapshots for a loaded player." },
                new { method = "POST", path = NotifyPlayerPath, category = "players", mutates = true, description = "Sends an admin notice packet to a live player." },
                new { method = "POST", path = KickPlayerPath, category = "players", mutates = true, description = "Disconnects a live player through the game-server connection path." },
                new { method = "POST", path = MoveToBindPointPath, category = "movement", mutates = true, description = "Moves a live player to the server-selected bind point." },
                new { method = "POST", path = MoveToInstanceExitPath, category = "movement", mutates = true, description = "Moves a live player to the instance exit or bind fallback." },
                new { method = "POST", path = UnstuckPlayerPath, category = "movement", mutates = true, description = "Runs the admin unstuck movement path for a live player." },
                new { method = "POST", path = RefreshMailboxPath, category = "refresh", mutates = true, description = "Reloads and resends live mailbox state for a loaded player." },
                new { method = "POST", path = RefreshInventoryPath, category = "refresh", mutates = true, description = "Resends current live inventory/equipment/Kinah packets." },
                new { method = "POST", path = RefreshWarehousePath, category = "refresh", mutates = true, description = "Resends current live character/account warehouse packets." },
                new { method = "POST", path = RefreshAccountWarehousePath, category = "refresh", mutates = true, description = "Resends current live account warehouse packets to every loaded character on an account." },
                new { method = "POST", path = ValidatePlayerItemActionPath, category = "items", mutates = false, description = "Validates live discard/repair item actions without mutating storage." },
                new { method = "POST", path = DiscardPlayerItemPath, category = "items", mutates = true, description = "Discards a live item through Storage.Delete and persists inventory." },
                new { method = "POST", path = RepairItemSlotPath, category = "items", mutates = true, description = "Moves a live warehouse item to a valid free slot and persists inventory." },
                new { method = "POST", path = RepairItemCountPath, category = "items", mutates = true, description = "Clamps a live overstacked item count and persists inventory." },
                new { method = "POST", path = ReloadCachePath, category = "server", mutates = true, allowedTargets = ReloadCacheTargets, description = "Reloads selected server-owned caches." },
                new { method = "POST", path = BroadcastMessagePath, category = "server", mutates = true, allowedScopes = MessageScopes, description = "Broadcasts a live server message to a selected scope." },
                new { method = "POST", path = MaintenanceWarningPath, category = "server", mutates = true, allowedScopes = MessageScopes, description = "Schedules live maintenance warning broadcasts." }
            }
        });
    }

    private async Task HandleOnlinePlayersAsync(HttpListenerContext ctx)
    {
        var players = GameWorld.GetInstance().GetAllPlayers()
            .Where(player => player != null)
            .Select(player =>
            {
                var common = player.GetCommonData();
                return new
                {
                    characterId = common.GetPlayerObjId(),
                    objectId = player.GetObjectId(),
                    name = common.GetName(),
                    accountId = player.GetAccount().GetId(),
                    accountName = player.GetAccountName(),
                    accessLevel = player.AccessLevel,
                    level = common.GetLevel(),
                    race = common.GetRace().ToString(),
                    playerClass = common.GetPlayerClass().ToString(),
                    worldId = player.GetWorldId(),
                    instanceId = player.GetInstanceId(),
                    x = Math.Round(player.GetX(), 2),
                    y = Math.Round(player.GetY(), 2),
                    z = Math.Round(player.GetZ(), 2),
                    heading = player.GetHeading(),
                    bindPoint = BindDestinationFor(player)
                };
            })
            .OrderBy(player => player.name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        await WriteJsonAsync(ctx, 200, new
        {
            ok = true,
            at = DateTimeOffset.UtcNow,
            count = players.Count,
            players
        });
    }

    private async Task HandleAccountStateAsync(HttpListenerContext ctx)
    {
        try
        {
            var req = ctx.Request;
            int.TryParse(req.QueryString["accountId"], out int accountId);
            string accountName = (req.QueryString["accountName"] ?? "").Trim();
            if (accountId <= 0 && string.IsNullOrWhiteSpace(accountName))
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "accountId or accountName is required." });
                return;
            }

            var livePlayers = GameWorld.GetInstance().GetAllPlayers()
                .Where(player => player != null)
                .Where(player => accountId > 0
                    ? player.GetAccount().GetId() == accountId
                    : string.Equals(player.GetAccountName(), accountName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(player => player.GetName(), StringComparer.OrdinalIgnoreCase)
                .ToList();

            Player? firstPlayer = livePlayers.FirstOrDefault();
            int resolvedAccountId = accountId > 0 ? accountId : firstPlayer?.GetAccount().GetId() ?? 0;
            string resolvedAccountName = !string.IsNullOrWhiteSpace(accountName)
                ? accountName
                : firstPlayer?.GetAccountName() ?? "";

            var players = livePlayers
                .Select(player =>
                {
                    var common = player.GetCommonData();
                    return new
                    {
                        characterId = common.GetPlayerObjId(),
                        objectId = player.GetObjectId(),
                        name = common.GetName(),
                        accountId = player.GetAccount().GetId(),
                        accountName = player.GetAccountName(),
                        accessLevel = player.AccessLevel,
                        level = common.GetLevel(),
                        race = common.GetRace().ToString(),
                        playerClass = common.GetPlayerClass().ToString(),
                        worldId = player.GetWorldId(),
                        instanceId = player.GetInstanceId(),
                        x = Math.Round(player.GetX(), 2),
                        y = Math.Round(player.GetY(), 2),
                        z = Math.Round(player.GetZ(), 2),
                        heading = player.GetHeading(),
                        bindPoint = BindDestinationFor(player)
                    };
                })
                .ToList();

            await WriteJsonAsync(ctx, 200, new
            {
                ok = true,
                at = DateTimeOffset.UtcNow,
                accountId = resolvedAccountId,
                accountName = resolvedAccountName,
                loaded = livePlayers.Count > 0,
                online = livePlayers.Count > 0,
                onlineCount = livePlayers.Count,
                players,
                warehouse = firstPlayer == null ? null : SnapshotWarehouse(firstPlayer)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin account-state handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private async Task HandlePlayerStateAsync(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        int.TryParse(req.QueryString["recipientCharacterId"] ?? req.QueryString["characterId"], out int characterId);
        string? characterName = req.QueryString["characterName"];
        if (characterId <= 0 && string.IsNullOrWhiteSpace(characterName))
        {
            await WriteJsonAsync(ctx, 400, new { ok = false, error = "recipientCharacterId or characterName is required." });
            return;
        }

        Player? player = ResolveOnlinePlayer(characterId, characterName);
        if (player == null)
        {
            PlayerCommonData? offlineCommon = ResolvePlayerCommonData(characterId, characterName);
            if (offlineCommon == null)
            {
                await WriteJsonAsync(ctx, 404, new { ok = false, error = "Character was not found." });
                return;
            }

            await WriteJsonAsync(ctx, 200, new
            {
                ok = true,
                online = false,
                at = DateTimeOffset.UtcNow,
                recipientCharacterId = offlineCommon.GetPlayerObjId(),
                recipientName = offlineCommon.GetName(),
                lastKnown = OfflinePlayerStatePayload(offlineCommon)
            });
            return;
        }

        var common = player.GetCommonData();
        var payload = new
        {
            characterId = common.GetPlayerObjId(),
            objectId = player.GetObjectId(),
            name = common.GetName(),
            accountId = player.GetAccount().GetId(),
            accountName = player.GetAccountName(),
            accessLevel = player.AccessLevel,
            level = common.GetLevel(),
            race = common.GetRace().ToString(),
            playerClass = common.GetPlayerClass().ToString(),
            worldId = player.GetWorldId(),
            instanceId = player.GetInstanceId(),
            x = Math.Round(player.GetX(), 2),
            y = Math.Round(player.GetY(), 2),
            z = Math.Round(player.GetZ(), 2),
            heading = player.GetHeading(),
            bindPoint = BindDestinationFor(player)
        };

        await WriteJsonAsync(ctx, 200, new
        {
            ok = true,
            online = true,
            at = DateTimeOffset.UtcNow,
            recipientCharacterId = common.GetPlayerObjId(),
            recipientName = common.GetName(),
            player = payload
        });
    }

    private async Task HandlePlayerStorageStateAsync(HttpListenerContext ctx)
    {
        try
        {
            var req = ctx.Request;
            int.TryParse(req.QueryString["recipientCharacterId"] ?? req.QueryString["characterId"], out int characterId);
            string? characterName = req.QueryString["characterName"];
            if (characterId <= 0 && string.IsNullOrWhiteSpace(characterName))
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "recipientCharacterId or characterName is required." });
                return;
            }

            Player? player = ResolveOnlinePlayer(characterId, characterName);
            if (player == null)
            {
                await WriteRecipientNotOnlineAsync(ctx, characterId, characterName);
                return;
            }

            var common = player.GetCommonData();
            await WriteJsonAsync(ctx, 200, new
            {
                ok = true,
                online = true,
                at = DateTimeOffset.UtcNow,
                recipientCharacterId = common.GetPlayerObjId(),
                recipientName = common.GetName(),
                position = PositionSnapshotFor(player, "live"),
                inventory = SnapshotInventory(player),
                warehouse = SnapshotWarehouse(player),
                mailbox = SnapshotMailbox(player.GetMailbox())
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin player-storage-state handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private async Task HandleNotifyPlayerAsync(HttpListenerContext ctx)
    {
        try
        {
            AdminNotifyPlayerRequest? dto = await ReadJsonAsync<AdminNotifyPlayerRequest>(ctx);
            if (dto == null)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Invalid JSON body." });
                return;
            }

            var player = ResolveOnlinePlayer(dto.RecipientCharacterId, dto.RecipientName);
            if (player == null)
            {
                await WriteRecipientNotOnlineAsync(ctx, dto.RecipientCharacterId, dto.RecipientName);
                return;
            }

            string message = NormalizeRequiredText(dto.Message, "Message", 1000);
            PacketSendUtility.SendMessage(player, message, ChatType.BRIGHT_YELLOW_CENTER);

            var common = player.GetCommonData();
            int characterId = common.GetPlayerObjId();
            string recipientName = common.GetName();

            _logger.LogInformation("Admin API: live notify -> {Recipient} ({CharacterId})", recipientName, characterId);
            await WriteJsonAsync(ctx, 200, new
            {
                ok = true,
                recipientCharacterId = characterId,
                recipientName,
                delivered = "online"
            });
        }
        catch (ArgumentException ex)
        {
            await WriteJsonAsync(ctx, 400, new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin live notify handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private async Task HandleKickPlayerAsync(HttpListenerContext ctx)
    {
        try
        {
            AdminKickPlayerRequest? dto = await ReadJsonAsync<AdminKickPlayerRequest>(ctx);
            if (dto == null)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Invalid JSON body." });
                return;
            }

            var player = ResolveOnlinePlayer(dto.RecipientCharacterId, dto.RecipientName);
            if (player == null)
            {
                await WriteRecipientNotOnlineAsync(ctx, dto.RecipientCharacterId, dto.RecipientName);
                return;
            }

            string reason = NormalizeOptionalText(dto.Reason, "Reason", 200);
            var common = player.GetCommonData();
            int characterId = common.GetPlayerObjId();
            string recipientName = common.GetName();

            player.GetClientConnection().Close(SM_SYSTEM_MESSAGE.STR_KICK_CHARACTER());

            _logger.LogWarning("Admin API: live kick -> {Recipient} ({CharacterId}) reason={Reason}",
                recipientName, characterId, string.IsNullOrEmpty(reason) ? "(none)" : reason);
            await WriteJsonAsync(ctx, 200, new
            {
                ok = true,
                recipientCharacterId = characterId,
                recipientName,
                disconnected = true
            });
        }
        catch (ArgumentException ex)
        {
            await WriteJsonAsync(ctx, 400, new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin live kick handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private async Task HandleMoveToBindPointAsync(HttpListenerContext ctx)
    {
        try
        {
            AdminMoveToBindPointRequest? dto = await ReadJsonAsync<AdminMoveToBindPointRequest>(ctx);
            if (dto == null)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Invalid JSON body." });
                return;
            }

            var player = ResolveOnlinePlayer(dto.RecipientCharacterId, dto.RecipientName);
            if (player == null)
            {
                await WriteRecipientNotOnlineAsync(ctx, dto.RecipientCharacterId, dto.RecipientName);
                return;
            }

            string reason = NormalizeOptionalText(dto.Reason, "Reason", 200);
            var common = player.GetCommonData();
            int characterId = common.GetPlayerObjId();
            string recipientName = common.GetName();
            var destination = BindDestinationFor(player);
            var from = PositionSnapshotFor(player, "before");

            TeleportService.MoveToBindLocation(player);
            var to = PositionSnapshotFor(player, "actual");

            _logger.LogWarning("Admin API: move to bind point -> {Recipient} ({CharacterId}) destination={DestinationWorldId} actual={ActualWorldId}:{ActualX},{ActualY},{ActualZ} reason={Reason}",
                recipientName, characterId, destination.WorldId, to.WorldId, to.X, to.Y, to.Z, string.IsNullOrEmpty(reason) ? "(none)" : reason);
            await WriteJsonAsync(ctx, 200, new
            {
                ok = true,
                recipientCharacterId = characterId,
                recipientName,
                moved = true,
                from,
                to,
                destination
            });
        }
        catch (ArgumentException ex)
        {
            await WriteJsonAsync(ctx, 400, new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin move-to-bind-point handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private async Task HandleRefreshMailboxAsync(HttpListenerContext ctx)
    {
        try
        {
            AdminRefreshMailboxRequest? dto = await ReadJsonAsync<AdminRefreshMailboxRequest>(ctx);
            if (dto == null)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Invalid JSON body." });
                return;
            }

            var player = ResolveOnlinePlayer(dto.RecipientCharacterId, dto.RecipientName);
            if (player == null)
            {
                await WriteRecipientNotOnlineAsync(ctx, dto.RecipientCharacterId, dto.RecipientName);
                return;
            }

            string reason = NormalizeOptionalText(dto.Reason, "Reason", 200);
            var common = player.GetCommonData();
            int characterId = common.GetPlayerObjId();
            string recipientName = common.GetName();
            var currentMailbox = player.GetMailbox();
            byte mailboxState = currentMailbox?.mailBoxState ?? 0;
            var before = SnapshotMailbox(currentMailbox);

            player.SetMailbox(MailDAO.LoadPlayerMailbox(player));
            var refreshedMailbox = player.GetMailbox();
            if (refreshedMailbox != null)
            {
                refreshedMailbox.mailBoxState = mailboxState;
                common.SetMailboxLetters(refreshedMailbox.Size());
            }
            var after = SnapshotMailbox(refreshedMailbox);

            PacketSendUtility.SendPacket(player, new SM_MAIL_SERVICE());

            if (refreshedMailbox != null && mailboxState != 0)
            {
                bool isPostman = (mailboxState & PlayerMailboxState.EXPRESS) == PlayerMailboxState.EXPRESS;
                MailService.SendMailList(player, isPostman, false);
            }

            _logger.LogInformation("Admin API: refresh mailbox -> {Recipient} ({CharacterId}) total={BeforeTotal}->{AfterTotal} unread={BeforeUnread}->{AfterUnread} reason={Reason}",
                recipientName, characterId, before.TotalCount, after.TotalCount, before.UnreadCount, after.UnreadCount,
                string.IsNullOrEmpty(reason) ? "(none)" : reason);
            await WriteJsonAsync(ctx, 200, new
            {
                ok = true,
                recipientCharacterId = characterId,
                recipientName,
                refreshed = true,
                mailboxState,
                before,
                after
            });
        }
        catch (ArgumentException ex)
        {
            await WriteJsonAsync(ctx, 400, new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin refresh-mailbox handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private async Task HandleMoveToInstanceExitAsync(HttpListenerContext ctx)
    {
        try
        {
            AdminMoveToInstanceExitRequest? dto = await ReadJsonAsync<AdminMoveToInstanceExitRequest>(ctx);
            if (dto == null)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Invalid JSON body." });
                return;
            }

            var player = ResolveOnlinePlayer(dto.RecipientCharacterId, dto.RecipientName);
            if (player == null)
            {
                await WriteRecipientNotOnlineAsync(ctx, dto.RecipientCharacterId, dto.RecipientName);
                return;
            }

            string reason = NormalizeOptionalText(dto.Reason, "Reason", 200);
            var common = player.GetCommonData();
            int characterId = common.GetPlayerObjId();
            string recipientName = common.GetName();
            var destination = InstanceExitDestinationFor(player);
            var from = PositionSnapshotFor(player, "before");

            TeleportService.MoveToInstanceExit(player, player.GetWorldId(), player.GetRace());
            var to = PositionSnapshotFor(player, "actual");

            _logger.LogWarning("Admin API: move to instance exit -> {Recipient} ({CharacterId}) destination={DestinationSource}:{DestinationWorldId} actual={ActualWorldId}:{ActualX},{ActualY},{ActualZ} reason={Reason}",
                recipientName, characterId, destination.Source, destination.WorldId, to.WorldId, to.X, to.Y, to.Z, string.IsNullOrEmpty(reason) ? "(none)" : reason);
            await WriteJsonAsync(ctx, 200, new
            {
                ok = true,
                recipientCharacterId = characterId,
                recipientName,
                moved = true,
                from,
                to,
                destination
            });
        }
        catch (ArgumentException ex)
        {
            await WriteJsonAsync(ctx, 400, new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin move-to-instance-exit handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private async Task HandleUnstuckPlayerAsync(HttpListenerContext ctx)
    {
        try
        {
            AdminUnstuckPlayerRequest? dto = await ReadJsonAsync<AdminUnstuckPlayerRequest>(ctx);
            if (dto == null)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Invalid JSON body." });
                return;
            }

            var player = ResolveOnlinePlayer(dto.RecipientCharacterId, dto.RecipientName);
            if (player == null)
            {
                await WriteRecipientNotOnlineAsync(ctx, dto.RecipientCharacterId, dto.RecipientName);
                return;
            }

            string reason = NormalizeOptionalText(dto.Reason, "Reason", 200);
            var common = player.GetCommonData();
            int characterId = common.GetPlayerObjId();
            string recipientName = common.GetName();
            var destination = InstanceExitDestinationFor(player);
            string action = destination.Source == "instance-exit" ? "instance-exit" : "bind-fallback";
            var from = PositionSnapshotFor(player, "before");

            TeleportService.MoveToInstanceExit(player, player.GetWorldId(), player.GetRace());
            var to = PositionSnapshotFor(player, "actual");

            _logger.LogWarning("Admin API: unstuck player -> {Recipient} ({CharacterId}) action={Action} destination={DestinationSource}:{DestinationWorldId} actual={ActualWorldId}:{ActualX},{ActualY},{ActualZ} reason={Reason}",
                recipientName, characterId, action, destination.Source, destination.WorldId, to.WorldId, to.X, to.Y, to.Z, string.IsNullOrEmpty(reason) ? "(none)" : reason);
            await WriteJsonAsync(ctx, 200, new
            {
                ok = true,
                recipientCharacterId = characterId,
                recipientName,
                moved = true,
                action,
                from,
                to,
                destination
            });
        }
        catch (ArgumentException ex)
        {
            await WriteJsonAsync(ctx, 400, new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin unstuck-player handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private async Task HandleRefreshInventoryAsync(HttpListenerContext ctx)
    {
        try
        {
            AdminRefreshInventoryRequest? dto = await ReadJsonAsync<AdminRefreshInventoryRequest>(ctx);
            if (dto == null)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Invalid JSON body." });
                return;
            }

            var player = ResolveOnlinePlayer(dto.RecipientCharacterId, dto.RecipientName);
            if (player == null)
            {
                await WriteRecipientNotOnlineAsync(ctx, dto.RecipientCharacterId, dto.RecipientName);
                return;
            }

            string reason = NormalizeOptionalText(dto.Reason, "Reason", 200);
            var common = player.GetCommonData();
            int characterId = common.GetPlayerObjId();
            string recipientName = common.GetName();
            AdminInventorySnapshot inventory = SendInventoryRefresh(player);

            _logger.LogInformation("Admin API: refresh inventory -> {Recipient} ({CharacterId}) cubeItems={CubeItems} equipped={EquippedItems} kinah={Kinah} reason={Reason}",
                recipientName, characterId, inventory.CubeItemCount, inventory.EquippedItemCount, inventory.Kinah,
                string.IsNullOrEmpty(reason) ? "(none)" : reason);
            await WriteJsonAsync(ctx, 200, new
            {
                ok = true,
                recipientCharacterId = characterId,
                recipientName,
                refreshed = true,
                inventory
            });
        }
        catch (ArgumentException ex)
        {
            await WriteJsonAsync(ctx, 400, new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin refresh-inventory handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private async Task HandleRefreshWarehouseAsync(HttpListenerContext ctx)
    {
        try
        {
            AdminRefreshWarehouseRequest? dto = await ReadJsonAsync<AdminRefreshWarehouseRequest>(ctx);
            if (dto == null)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Invalid JSON body." });
                return;
            }

            var player = ResolveOnlinePlayer(dto.RecipientCharacterId, dto.RecipientName);
            if (player == null)
            {
                await WriteRecipientNotOnlineAsync(ctx, dto.RecipientCharacterId, dto.RecipientName);
                return;
            }

            string reason = NormalizeOptionalText(dto.Reason, "Reason", 200);
            var common = player.GetCommonData();
            int characterId = common.GetPlayerObjId();
            string recipientName = common.GetName();
            AdminWarehouseSnapshot warehouse = SendWarehouseRefresh(player);

            _logger.LogInformation("Admin API: refresh warehouse -> {Recipient} ({CharacterId}) characterItems={CharacterItems} accountItems={AccountItems} accountKinah={AccountKinah} reason={Reason}",
                recipientName, characterId, warehouse.CharacterWarehouseItemCount, warehouse.AccountWarehouseItemCount, warehouse.AccountWarehouseKinah,
                string.IsNullOrEmpty(reason) ? "(none)" : reason);
            await WriteJsonAsync(ctx, 200, new
            {
                ok = true,
                recipientCharacterId = characterId,
                recipientName,
                refreshed = true,
                warehouse
            });
        }
        catch (ArgumentException ex)
        {
            await WriteJsonAsync(ctx, 400, new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin refresh-warehouse handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private async Task HandleRefreshAccountWarehouseAsync(HttpListenerContext ctx)
    {
        try
        {
            AdminRefreshAccountWarehouseRequest? dto = await ReadJsonAsync<AdminRefreshAccountWarehouseRequest>(ctx);
            if (dto == null)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Invalid JSON body." });
                return;
            }

            string accountName = (dto.AccountName ?? "").Trim();
            if (dto.AccountId <= 0 && string.IsNullOrWhiteSpace(accountName))
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "accountId or accountName is required." });
                return;
            }

            string reason = NormalizeOptionalText(dto.Reason, "Reason", 200);
            var livePlayers = GameWorld.GetInstance().GetAllPlayers()
                .Where(player => player != null)
                .Where(player => dto.AccountId > 0
                    ? player.GetAccount().GetId() == dto.AccountId
                    : string.Equals(player.GetAccountName(), accountName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(player => player.GetName(), StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (livePlayers.Count == 0)
            {
                await WriteJsonAsync(ctx, 404, new { ok = false, error = "No loaded characters were found for this account." });
                return;
            }

            Player firstPlayer = livePlayers[0];
            int accountId = firstPlayer.GetAccount().GetId();
            string resolvedAccountName = firstPlayer.GetAccountName();
            var players = livePlayers
                .Select(player =>
                {
                    var common = player.GetCommonData();
                    AdminWarehouseSnapshot warehouse = SendWarehouseRefresh(player);
                    return new
                    {
                        recipientCharacterId = common.GetPlayerObjId(),
                        recipientName = common.GetName(),
                        warehouse
                    };
                })
                .ToList();

            _logger.LogInformation("Admin API: refresh account warehouse -> account={AccountName} ({AccountId}) loadedPlayers={LoadedPlayers} reason={Reason}",
                resolvedAccountName, accountId, players.Count, string.IsNullOrEmpty(reason) ? "(none)" : reason);
            await WriteJsonAsync(ctx, 200, new
            {
                ok = true,
                accountId,
                accountName = resolvedAccountName,
                refreshed = true,
                refreshedCount = players.Count,
                players
            });
        }
        catch (ArgumentException ex)
        {
            await WriteJsonAsync(ctx, 400, new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin refresh-account-warehouse handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private async Task HandleValidatePlayerItemActionAsync(HttpListenerContext ctx)
    {
        try
        {
            AdminValidatePlayerItemActionRequest? dto = await ReadJsonAsync<AdminValidatePlayerItemActionRequest>(ctx);
            if (dto == null)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Invalid JSON body." });
                return;
            }

            AdminPlayerItemActionValidation validation = ValidatePlayerItemAction(dto);
            await WriteJsonAsync(ctx, 200, PlayerItemActionValidationPayload(validation, true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin validate-player-item-action handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private static async Task WritePlayerItemActionValidationFailureAsync(HttpListenerContext ctx, AdminPlayerItemActionValidation validation)
    {
        string message = validation.Errors.Count > 0
            ? string.Join(" ", validation.Errors)
            : "Live item action validation failed.";
        await WriteJsonAsync(ctx, PlayerItemActionValidationStatus(validation), PlayerItemActionValidationPayload(validation, false, message));
    }

    private static object PlayerItemActionValidationPayload(
        AdminPlayerItemActionValidation validation,
        bool ok,
        string? error = null,
        IEnumerable<string>? extraErrors = null,
        bool? persisted = null)
    {
        var errors = extraErrors == null
            ? validation.Errors
            : validation.Errors.Concat(extraErrors).Where(entry => !string.IsNullOrWhiteSpace(entry)).ToList();

        return new
        {
            ok,
            valid = validation.Valid,
            error,
            action = validation.Action,
            recipientCharacterId = validation.RecipientCharacterId,
            recipientName = validation.RecipientName,
            itemUniqueId = validation.ItemUniqueId,
            itemId = validation.ItemId,
            itemName = validation.ItemName,
            itemCount = validation.ItemCount,
            maxStackCount = validation.MaxStackCount,
            storageId = validation.StorageId,
            storageName = validation.StorageName,
            currentSlot = validation.CurrentSlot,
            targetSlot = validation.TargetSlot,
            targetCount = validation.TargetCount,
            storageLimit = validation.StorageLimit,
            changed = validation.Changed,
            persisted,
            errors,
            warnings = validation.Warnings
        };
    }

    private static int PlayerItemActionValidationStatus(AdminPlayerItemActionValidation validation)
    {
        if (validation.Errors.Any(error =>
                error.Contains("not online", StringComparison.OrdinalIgnoreCase)
                || error.Contains("not found", StringComparison.OrdinalIgnoreCase)
                || error.Contains("not loaded", StringComparison.OrdinalIgnoreCase)))
            return 404;

        if (validation.Errors.Any(error =>
                error.Contains("already occupied", StringComparison.OrdinalIgnoreCase)
                || error.Contains("No free live warehouse slot", StringComparison.OrdinalIgnoreCase)))
            return 409;

        return 400;
    }

    private async Task HandleDiscardPlayerItemAsync(HttpListenerContext ctx)
    {
        try
        {
            AdminDiscardPlayerItemRequest? dto = await ReadJsonAsync<AdminDiscardPlayerItemRequest>(ctx);
            if (dto == null)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Invalid JSON body." });
                return;
            }

            AdminPlayerItemActionValidation validation = ValidatePlayerItemAction(new AdminValidatePlayerItemActionRequest
            {
                RecipientCharacterId = dto.RecipientCharacterId,
                RecipientName = dto.RecipientName,
                ItemUniqueId = dto.ItemUniqueId,
                StorageId = dto.StorageId,
                Action = "discard"
            });
            if (!validation.Valid)
            {
                await WritePlayerItemActionValidationFailureAsync(ctx, validation);
                return;
            }

            var player = validation.Player!;
            var storageType = validation.StorageType!;
            var storage = validation.Storage!;
            var item = validation.Item!;

            string reason = NormalizeOptionalText(dto.Reason, "Reason", 200);
            int characterId = player.GetCommonData().GetPlayerObjId();
            string recipientName = player.GetCommonData().GetName();
            int itemId = item.GetItemId();
            string itemName = item.GetItemName();
            long itemCount = item.GetItemCount();
            long slot = item.GetEquipmentSlot();

            var deleted = storage.Delete(item, ItemPacketService.ItemDeleteType.DISCARD);
            if (deleted == null)
            {
                await WriteJsonAsync(ctx, 409, PlayerItemActionValidationPayload(
                    validation,
                    false,
                    "Item could not be removed from live storage.",
                    new[] { "Item could not be removed from live storage." },
                    persisted: false));
                return;
            }

            if (!InventoryDAO.Store(player))
            {
                _logger.LogError("Admin API: discard item failed to persist after live removal recipient={Recipient} ({CharacterId}) itemUniqueId={ItemUniqueId}",
                    recipientName, characterId, dto.ItemUniqueId);
                await WriteJsonAsync(ctx, 500, PlayerItemActionValidationPayload(
                    validation,
                    false,
                    "Item was removed from live storage, but inventory persistence failed.",
                    new[] { "Item was removed from live storage, but inventory persistence failed." },
                    persisted: false));
                return;
            }

            _logger.LogWarning("Admin API: discarded item -> {Recipient} ({CharacterId}) item={ItemId}:{ItemName} object={ItemUniqueId} count={ItemCount} storage={StorageId} slot={Slot} reason={Reason}",
                recipientName, characterId, itemId, itemName, dto.ItemUniqueId, itemCount, storageType.GetId(), slot,
                string.IsNullOrEmpty(reason) ? "(none)" : reason);
            await WriteJsonAsync(ctx, 200, new
            {
                ok = true,
                recipientCharacterId = characterId,
                recipientName,
                itemUniqueId = dto.ItemUniqueId,
                itemId,
                itemName,
                itemCount,
                storageId = storageType.GetId(),
                storageName = AdminStorageName(storageType),
                slot,
                discarded = true,
                persisted = true
            });
        }
        catch (ArgumentException ex)
        {
            await WriteJsonAsync(ctx, 400, new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin discard-player-item handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private async Task HandleRepairItemSlotAsync(HttpListenerContext ctx)
    {
        try
        {
            AdminRepairItemSlotRequest? dto = await ReadJsonAsync<AdminRepairItemSlotRequest>(ctx);
            if (dto == null)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Invalid JSON body." });
                return;
            }

            AdminPlayerItemActionValidation validation = ValidatePlayerItemAction(new AdminValidatePlayerItemActionRequest
            {
                RecipientCharacterId = dto.RecipientCharacterId,
                RecipientName = dto.RecipientName,
                ItemUniqueId = dto.ItemUniqueId,
                StorageId = dto.StorageId,
                Action = "repair-slot",
                TargetSlot = dto.TargetSlot
            });
            if (!validation.Valid)
            {
                await WritePlayerItemActionValidationFailureAsync(ctx, validation);
                return;
            }

            var player = validation.Player!;
            var storageType = validation.StorageType!;
            var item = validation.Item!;
            long previousSlot = item.GetEquipmentSlot();
            int targetSlot = validation.TargetSlot;

            string reason = NormalizeOptionalText(dto.Reason, "Reason", 200);
            int characterId = player.GetCommonData().GetPlayerObjId();
            string recipientName = player.GetCommonData().GetName();
            int itemId = item.GetItemId();
            string itemName = item.GetItemName();
            long itemCount = item.GetItemCount();

            if (previousSlot != targetSlot)
            {
                item.SetEquipmentSlot(targetSlot);
                ItemPacketService.SendItemUpdatePacket(player, storageType, item, ItemPacketService.ItemUpdateType.PUT);
            }

            if (!InventoryDAO.Store(player))
            {
                _logger.LogError("Admin API: repair item slot failed to persist recipient={Recipient} ({CharacterId}) itemUniqueId={ItemUniqueId} {PreviousSlot}->{TargetSlot}",
                    recipientName, characterId, dto.ItemUniqueId, previousSlot, targetSlot);
                await WriteJsonAsync(ctx, 500, PlayerItemActionValidationPayload(
                    validation,
                    false,
                    "Slot was updated in live storage, but inventory persistence failed.",
                    new[] { "Slot was updated in live storage, but inventory persistence failed." },
                    persisted: false));
                return;
            }

            AdminWarehouseSnapshot warehouse = SendWarehouseRefresh(player);
            _logger.LogWarning("Admin API: repaired item slot -> {Recipient} ({CharacterId}) item={ItemId}:{ItemName} object={ItemUniqueId} count={ItemCount} storage={StorageId} slot={PreviousSlot}->{TargetSlot} reason={Reason}",
                recipientName, characterId, itemId, itemName, dto.ItemUniqueId, itemCount, storageType.GetId(), previousSlot, targetSlot,
                string.IsNullOrEmpty(reason) ? "(none)" : reason);
            await WriteJsonAsync(ctx, 200, new
            {
                ok = true,
                recipientCharacterId = characterId,
                recipientName,
                itemUniqueId = dto.ItemUniqueId,
                itemId,
                itemName,
                itemCount,
                storageId = storageType.GetId(),
                storageName = AdminStorageName(storageType),
                previousSlot,
                slot = targetSlot,
                changed = previousSlot != targetSlot,
                persisted = true,
                warehouse
            });
        }
        catch (ArgumentException ex)
        {
            await WriteJsonAsync(ctx, 400, new { ok = false, error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            await WriteJsonAsync(ctx, 409, new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin repair-item-slot handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private async Task HandleRepairItemCountAsync(HttpListenerContext ctx)
    {
        try
        {
            AdminRepairItemCountRequest? dto = await ReadJsonAsync<AdminRepairItemCountRequest>(ctx);
            if (dto == null)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Invalid JSON body." });
                return;
            }

            AdminPlayerItemActionValidation validation = ValidatePlayerItemAction(new AdminValidatePlayerItemActionRequest
            {
                RecipientCharacterId = dto.RecipientCharacterId,
                RecipientName = dto.RecipientName,
                ItemUniqueId = dto.ItemUniqueId,
                StorageId = dto.StorageId,
                Action = "repair-count",
                TargetCount = dto.TargetCount
            });
            if (!validation.Valid)
            {
                await WritePlayerItemActionValidationFailureAsync(ctx, validation);
                return;
            }

            var player = validation.Player!;
            var storageType = validation.StorageType!;
            var storage = validation.Storage!;
            var item = validation.Item!;
            long previousCount = item.GetItemCount();
            long maxStackCount = item.GetItemTemplate().GetMaxStackCount();
            long targetCount = long.Parse(validation.TargetCount);

            string reason = NormalizeOptionalText(dto.Reason, "Reason", 200);
            int characterId = player.GetCommonData().GetPlayerObjId();
            string recipientName = player.GetCommonData().GetName();
            int itemId = item.GetItemId();
            string itemName = item.GetItemName();
            long reduction = previousCount - targetCount;

            long leftover = storage.DecreaseItemCount(item, reduction, ItemPacketService.ItemUpdateType.DEC_ITEM_USE);
            if (leftover != 0 || item.GetItemCount() != targetCount)
            {
                await WriteJsonAsync(ctx, 409, PlayerItemActionValidationPayload(
                    validation,
                    false,
                    "Item count changed while repair was being applied.",
                    new[] { "Item count changed while repair was being applied." },
                    persisted: false));
                return;
            }

            if (!InventoryDAO.Store(player))
            {
                _logger.LogError("Admin API: repair item count failed to persist recipient={Recipient} ({CharacterId}) itemUniqueId={ItemUniqueId} {PreviousCount}->{TargetCount}",
                    recipientName, characterId, dto.ItemUniqueId, previousCount, targetCount);
                await WriteJsonAsync(ctx, 500, PlayerItemActionValidationPayload(
                    validation,
                    false,
                    "Count was updated in live storage, but inventory persistence failed.",
                    new[] { "Count was updated in live storage, but inventory persistence failed." },
                    persisted: false));
                return;
            }

            AdminInventorySnapshot? inventory = storageType == StorageType.CUBE ? SendInventoryRefresh(player) : null;
            AdminWarehouseSnapshot? warehouse = storageType == StorageType.CUBE ? null : SendWarehouseRefresh(player);
            _logger.LogWarning("Admin API: repaired item count -> {Recipient} ({CharacterId}) item={ItemId}:{ItemName} object={ItemUniqueId} storage={StorageId} count={PreviousCount}->{TargetCount} reason={Reason}",
                recipientName, characterId, itemId, itemName, dto.ItemUniqueId, storageType.GetId(), previousCount, targetCount,
                string.IsNullOrEmpty(reason) ? "(none)" : reason);
            await WriteJsonAsync(ctx, 200, new
            {
                ok = true,
                recipientCharacterId = characterId,
                recipientName,
                itemUniqueId = dto.ItemUniqueId,
                itemId,
                itemName,
                previousCount = previousCount.ToString(),
                itemCount = targetCount.ToString(),
                maxStackCount = maxStackCount.ToString(),
                storageId = storageType.GetId(),
                storageName = AdminStorageName(storageType),
                changed = true,
                persisted = true,
                inventory,
                warehouse
            });
        }
        catch (ArgumentException ex)
        {
            await WriteJsonAsync(ctx, 400, new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin repair-item-count handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private async Task HandleReloadCacheAsync(HttpListenerContext ctx)
    {
        try
        {
            AdminReloadCacheRequest? dto = await ReadJsonAsync<AdminReloadCacheRequest>(ctx);
            if (dto == null)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Invalid JSON body." });
                return;
            }

            string target = NormalizeServerTarget(dto.Target);
            if (!ReloadCacheTargets.Contains(target))
            {
                await WriteInvalidReloadTargetAsync(ctx, target);
                return;
            }

            string reason = NormalizeOptionalText(dto.Reason, "Reason", 200);
            string detail;
            int? itemCount = null;

            switch (target)
            {
                case "announcements":
                    AnnouncementService.GetInstance().Reload();
                    itemCount = AnnouncementService.GetInstance().GetAnnouncements().Count;
                    detail = "Reloaded " + itemCount.Value + " announcements.";
                    break;
                case "html":
                    HTMLCache.GetInstance().Reload(true);
                    detail = HTMLCache.GetInstance().ToString();
                    break;
                case "item-restrictions":
                    AdminService.GetInstance().Reload();
                    detail = "Item operation restrictions reloaded.";
                    break;
                default:
                    await WriteInvalidReloadTargetAsync(ctx, target);
                    return;
            }

            _logger.LogWarning("Admin API: reload cache target={Target} detail={Detail} reason={Reason}",
                target, detail, string.IsNullOrEmpty(reason) ? "(none)" : reason);
            await WriteJsonAsync(ctx, 200, new
            {
                ok = true,
                target,
                reloaded = true,
                detail,
                itemCount
            });
        }
        catch (ArgumentException ex)
        {
            await WriteJsonAsync(ctx, 400, new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin reload-cache handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private async Task HandleBroadcastMessageAsync(HttpListenerContext ctx)
    {
        try
        {
            AdminBroadcastMessageRequest? dto = await ReadJsonAsync<AdminBroadcastMessageRequest>(ctx);
            if (dto == null)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Invalid JSON body." });
                return;
            }

            string scope = NormalizeMessageScope(dto.Scope);
            if (!MessageScopes.Contains(scope))
            {
                await WriteInvalidMessageScopeAsync(ctx, scope);
                return;
            }

            Race? race = RaceForScope(scope);
            string message = NormalizeRequiredText(dto.Message, "Message", 1000);

            int deliveredCount = SendAdminMessageToScope(message, race);

            _logger.LogInformation("Admin API: broadcast scope={Scope} delivered={DeliveredCount}", scope, deliveredCount);
            await WriteJsonAsync(ctx, 200, new
            {
                ok = true,
                scope,
                deliveredCount
            });
        }
        catch (ArgumentException ex)
        {
            await WriteJsonAsync(ctx, 400, new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin broadcast handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private async Task HandleMaintenanceWarningAsync(HttpListenerContext ctx)
    {
        try
        {
            AdminMaintenanceWarningRequest? dto = await ReadJsonAsync<AdminMaintenanceWarningRequest>(ctx);
            if (dto == null)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Invalid JSON body." });
                return;
            }

            int minutesUntilMaintenance = dto.MinutesUntilMaintenance;
            if (minutesUntilMaintenance < 1 || minutesUntilMaintenance > 1440)
            {
                await WriteInvalidMaintenanceMinutesAsync(ctx, minutesUntilMaintenance);
                return;
            }

            string scope = NormalizeMessageScope(dto.Scope);
            if (!MessageScopes.Contains(scope))
            {
                await WriteInvalidMessageScopeAsync(ctx, scope);
                return;
            }

            Race? race = RaceForScope(scope);
            string messageTemplate = string.IsNullOrWhiteSpace(dto.MessageTemplate)
                ? "Server maintenance begins in {minutes} {minuteLabel}. Please log out safely."
                : NormalizeRequiredText(dto.MessageTemplate, "Message template", 1000);
            string scheduleId = Guid.NewGuid().ToString("N");
            List<AdminMaintenanceWarningSchedule> schedule = BuildMaintenanceWarningSchedule(minutesUntilMaintenance)
                .Select(item => new AdminMaintenanceWarningSchedule
                {
                    RemainingMinutes = item.RemainingMinutes,
                    DelaySeconds = item.DelaySeconds,
                    Message = FormatMaintenanceWarning(messageTemplate, item.RemainingMinutes)
                })
                .ToList();

            foreach (var warning in schedule)
            {
                ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    int deliveredCount = SendAdminMessageToScope(warning.Message, race);
                    _logger.LogInformation(
                        "Admin API: maintenance warning schedule={ScheduleId} remaining={RemainingMinutes} scope={Scope} delivered={DeliveredCount}",
                        scheduleId, warning.RemainingMinutes, scope, deliveredCount);
                    return ValueTask.CompletedTask;
                }, TimeSpan.FromSeconds(warning.DelaySeconds));
            }

            _logger.LogInformation("Admin API: scheduled maintenance warnings schedule={ScheduleId} minutes={Minutes} scope={Scope} count={Count}",
                scheduleId, minutesUntilMaintenance, scope, schedule.Count);
            await WriteJsonAsync(ctx, 200, new
            {
                ok = true,
                scheduleId,
                scope,
                minutesUntilMaintenance,
                warningCount = schedule.Count,
                warnings = schedule
            });
        }
        catch (ArgumentException ex)
        {
            await WriteJsonAsync(ctx, 400, new { ok = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin maintenance warning handler error");
            await WriteJsonAsync(ctx, 500, new { ok = false, error = "Internal error." });
        }
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpListenerContext ctx)
    {
        string body;
        using (var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding ?? Encoding.UTF8))
            body = await reader.ReadToEndAsync();
        try
        {
            return JsonSerializer.Deserialize<T>(body, JsonOpts);
        }
        catch
        {
            return default;
        }
    }

    private static async Task WriteRecipientNotOnlineAsync(HttpListenerContext ctx, int recipientCharacterId, string? recipientName)
    {
        PlayerCommonData? offlineCommon = ResolvePlayerCommonData(recipientCharacterId, recipientName);
        await WriteJsonAsync(ctx, 404, new
        {
            ok = false,
            online = false,
            error = "Recipient is not online.",
            recipientCharacterId = offlineCommon?.GetPlayerObjId() ?? recipientCharacterId,
            recipientName = offlineCommon?.GetName() ?? (recipientName ?? "").Trim(),
            lastKnown = offlineCommon == null ? null : OfflinePlayerStatePayload(offlineCommon)
        });
    }

    private static object OfflinePlayerStatePayload(PlayerCommonData common)
    {
        return new
        {
            characterId = common.GetPlayerObjId(),
            name = common.GetName(),
            level = common.GetLevel(),
            race = common.GetRace().ToString(),
            playerClass = common.GetPlayerClass().ToString(),
            worldId = common.GetMapId(),
            x = Math.Round(common.GetX(), 2),
            y = Math.Round(common.GetY(), 2),
            z = Math.Round(common.GetZ(), 2),
            heading = common.GetHeading(),
            lastOnline = common.GetLastOnline()
        };
    }

    private static Player? ResolveOnlinePlayer(int characterId, string? characterName)
    {
        if (characterId > 0)
            return GameWorld.GetInstance().GetPlayer(characterId);
        if (string.IsNullOrWhiteSpace(characterName))
            return null;

        var direct = GameWorld.GetInstance().GetPlayer(characterName);
        if (direct != null)
            return direct;

        return GameWorld.GetInstance().GetAllPlayers()
            .FirstOrDefault(player => string.Equals(player.GetName(), characterName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static PlayerCommonData? ResolvePlayerCommonData(int characterId, string? characterName)
    {
        if (characterId > 0)
            return PlayerService.GetOrLoadPlayerCommonData(characterId);
        if (string.IsNullOrWhiteSpace(characterName))
            return null;
        return PlayerService.GetOrLoadPlayerCommonData(characterName.Trim());
    }

    private static string NormalizeServerTarget(string? value)
    {
        return (value ?? "").Trim().ToLowerInvariant();
    }

    private static string NormalizeMessageScope(string? value)
    {
        string scope = string.IsNullOrWhiteSpace(value)
            ? "all"
            : value.Trim().ToLowerInvariant();
        return scope == "asmo" ? "asmodians" : scope;
    }

    private static Task WriteInvalidReloadTargetAsync(HttpListenerContext ctx, string target)
    {
        return WriteJsonAsync(ctx, 400, new
        {
            ok = false,
            error = "Target must be announcements, html, or item-restrictions.",
            target,
            allowedTargets = ReloadCacheTargets
        });
    }

    private static Task WriteInvalidMessageScopeAsync(HttpListenerContext ctx, string scope)
    {
        return WriteJsonAsync(ctx, 400, new
        {
            ok = false,
            error = "Scope must be all, elyos, or asmodians.",
            scope,
            allowedScopes = MessageScopes
        });
    }

    private static Task WriteInvalidMaintenanceMinutesAsync(HttpListenerContext ctx, int minutesUntilMaintenance)
    {
        return WriteJsonAsync(ctx, 400, new
        {
            ok = false,
            error = "Minutes until maintenance must be between 1 and 1440.",
            minutesUntilMaintenance,
            minMinutes = 1,
            maxMinutes = 1440
        });
    }

    private static Race? RaceForScope(string scope)
    {
        return scope switch
        {
            "all" => null,
            "elyos" => Race.ELYOS,
            "asmodians" => Race.ASMODIANS,
            "asmo" => Race.ASMODIANS,
            _ => throw new ArgumentException("Scope must be all, elyos, or asmodians.")
        };
    }

    private static AdminPlayerItemActionValidation ValidatePlayerItemAction(AdminValidatePlayerItemActionRequest dto)
    {
        string action = (dto.Action ?? "").Trim().ToLowerInvariant();
        var validation = new AdminPlayerItemActionValidation
        {
            Action = action,
            RecipientCharacterId = dto.RecipientCharacterId,
            RecipientName = dto.RecipientName,
            ItemUniqueId = dto.ItemUniqueId,
            StorageId = dto.StorageId,
            TargetSlot = dto.TargetSlot,
            TargetCount = (dto.TargetCount ?? "").Trim()
        };

        if (action != "discard" && action != "repair-slot" && action != "repair-count")
        {
            validation.Errors.Add("Action must be discard, repair-slot, or repair-count.");
            return validation;
        }

        var player = ResolveOnlinePlayer(dto.RecipientCharacterId, dto.RecipientName);
        if (player == null)
        {
            validation.Errors.Add("Recipient is not online.");
            return validation;
        }

        validation.Player = player;
        validation.RecipientCharacterId = player.GetCommonData().GetPlayerObjId();
        validation.RecipientName = player.GetCommonData().GetName();

        if (dto.ItemUniqueId <= 0)
        {
            validation.Errors.Add("itemUniqueId must be positive.");
            return validation;
        }

        var storageType = StorageType.GetStorageTypeById(dto.StorageId);
        if ((action == "discard" && !IsAdminDiscardStorage(storageType))
            || (action == "repair-slot" && !IsAdminSlotRepairStorage(storageType))
            || (action == "repair-count" && !IsAdminCountRepairStorage(storageType)))
        {
            validation.Errors.Add(action switch
            {
                "discard" => "Discard is only supported for cube, character warehouse, and account warehouse items.",
                "repair-slot" => "Slot repair is only supported for character and account warehouse items.",
                _ => "Count repair is only supported for cube, character warehouse, and account warehouse items."
            });
            return validation;
        }

        validation.StorageType = storageType;
        if (storageType == StorageType.REGULAR_WAREHOUSE)
            player.SetWarehouseLimit();

        var storage = player.GetStorage(storageType.GetId());
        if (storage == null)
        {
            validation.Errors.Add("Requested live storage is not loaded for this player.");
            return validation;
        }

        validation.Storage = storage;
        var item = storage.GetItemByObjId(dto.ItemUniqueId);
        if (item == null)
        {
            validation.Errors.Add("Item was not found in the requested live storage.");
            return validation;
        }

        validation.Item = item;
        var template = item.GetItemTemplate();
        validation.ItemId = item.GetItemId();
        validation.ItemName = item.GetItemName();
        validation.ItemCount = item.GetItemCount().ToString();
        validation.MaxStackCount = template.GetMaxStackCount().ToString();
        validation.StorageId = storageType.GetId();
        validation.StorageName = AdminStorageName(storageType);
        validation.CurrentSlot = item.GetEquipmentSlot().ToString();
        validation.StorageLimit = storage.GetLimit();

        if (action == "discard")
        {
            ValidateDiscardPlayerItemAction(player, item, template, validation);
        }
        else if (action == "repair-slot")
        {
            ValidateRepairItemSlotAction(storage, item, template, dto.TargetSlot, validation);
        }
        else
        {
            ValidateRepairItemCountAction(item, template, dto.TargetCount, validation);
        }

        return validation;
    }

    private static void ValidateDiscardPlayerItemAction(
        Player player,
        Item item,
        Aion.GameServer.Model.Templates.Items.ItemTemplate template,
        AdminPlayerItemActionValidation validation)
    {
        if (item.IsEquipped())
            validation.Errors.Add("Equipped items must be unequipped before discard.");
        if (template.IsKinah())
            validation.Errors.Add("Kinah cannot be discarded through the item discard endpoint.");
        if (template.GetItemGroup() == Model.Templates.Items.Enums.ItemGroup.QUEST)
            validation.Errors.Add("Quest items are blocked from admin discard.");
        if (!template.IsDeletable() || !ItemRestrictionService.CanRemoveItem(player, item))
            validation.Errors.Add("The game server does not allow this item to be removed.");

        validation.Changed = validation.Valid;
    }

    private static void ValidateRepairItemSlotAction(
        IStorage storage,
        Item item,
        Aion.GameServer.Model.Templates.Items.ItemTemplate template,
        int requestedTargetSlot,
        AdminPlayerItemActionValidation validation)
    {
        if (template.IsKinah())
        {
            validation.Errors.Add("Kinah does not use a normal warehouse slot.");
            return;
        }

        long previousSlot = item.GetEquipmentSlot();
        int limit = storage.GetLimit();
        int targetSlot;
        try
        {
            targetSlot = requestedTargetSlot >= 0
                ? requestedTargetSlot
                : FirstFreeWarehouseSlot(storage, item.GetObjectId(), limit);
        }
        catch (InvalidOperationException ex)
        {
            validation.Errors.Add(ex.Message);
            return;
        }

        validation.TargetSlot = targetSlot;
        if (targetSlot < 0 || targetSlot >= limit)
        {
            validation.Errors.Add("Target slot is outside the live warehouse limit.");
            return;
        }

        var occupyingItem = storage.GetItems().FirstOrDefault(candidate =>
            candidate.GetObjectId() != item.GetObjectId()
            && !candidate.GetItemTemplate().IsKinah()
            && candidate.GetEquipmentSlot() == targetSlot);
        if (occupyingItem != null)
        {
            validation.Errors.Add("Target slot is already occupied.");
            return;
        }

        validation.Changed = previousSlot != targetSlot;
        if (!validation.Changed)
            validation.Warnings.Add("Item is already in the target slot.");
    }

    private static void ValidateRepairItemCountAction(
        Item item,
        Aion.GameServer.Model.Templates.Items.ItemTemplate template,
        string? requestedTargetCount,
        AdminPlayerItemActionValidation validation)
    {
        if (template.IsKinah())
        {
            validation.Errors.Add("Kinah count repair must use the Kinah-specific tools.");
            return;
        }

        long previousCount = item.GetItemCount();
        long maxStackCount = template.GetMaxStackCount();
        if (previousCount <= 0)
        {
            validation.Errors.Add("Item count is not positive; use discard after reviewing the row.");
            return;
        }
        if (maxStackCount <= 0)
        {
            validation.Errors.Add("Template max stack count is not valid.");
            return;
        }
        if (previousCount <= maxStackCount)
        {
            validation.Errors.Add("Item count is already within the template max stack.");
            return;
        }

        long targetCount = maxStackCount;
        string targetCountText = (requestedTargetCount ?? "").Trim();
        if (targetCountText.Length > 0 && !long.TryParse(targetCountText, out targetCount))
        {
            validation.Errors.Add("Target count is not a valid integer.");
            return;
        }

        validation.TargetCount = targetCount.ToString();
        if (targetCount <= 0 || targetCount > maxStackCount)
        {
            validation.Errors.Add("Target count must be positive and no greater than the template max stack.");
            return;
        }
        if (targetCount >= previousCount)
        {
            validation.Errors.Add("Target count must be lower than the current item count.");
            return;
        }

        validation.Changed = targetCount != previousCount;
    }

    private static bool IsAdminDiscardStorage(StorageType? storageType)
    {
        return storageType == StorageType.CUBE
            || storageType == StorageType.REGULAR_WAREHOUSE
            || storageType == StorageType.ACCOUNT_WAREHOUSE;
    }

    private static bool IsAdminCountRepairStorage(StorageType? storageType)
    {
        return storageType == StorageType.CUBE
            || storageType == StorageType.REGULAR_WAREHOUSE
            || storageType == StorageType.ACCOUNT_WAREHOUSE;
    }

    private static bool IsAdminSlotRepairStorage(StorageType? storageType)
    {
        return storageType == StorageType.REGULAR_WAREHOUSE
            || storageType == StorageType.ACCOUNT_WAREHOUSE;
    }

    private static int FirstFreeWarehouseSlot(IStorage storage, int ignoredItemObjectId, int limit)
    {
        var used = storage.GetItems()
            .Where(item => item.GetObjectId() != ignoredItemObjectId && !item.GetItemTemplate().IsKinah())
            .Select(item => item.GetEquipmentSlot())
            .Where(slot => slot >= 0 && slot < limit)
            .ToHashSet();

        for (int slot = 0; slot < limit; slot++)
        {
            if (!used.Contains(slot))
                return slot;
        }

        throw new InvalidOperationException("No free live warehouse slot is available.");
    }

    private static string AdminStorageName(StorageType storageType)
    {
        if (storageType == StorageType.CUBE)
            return "Cube";
        if (storageType == StorageType.REGULAR_WAREHOUSE)
            return "Character Warehouse";
        if (storageType == StorageType.ACCOUNT_WAREHOUSE)
            return "Account Warehouse";
        return "Storage " + storageType.GetId();
    }

    private static int SendAdminMessageToScope(string message, Race? race)
    {
        int deliveredCount = 0;
        foreach (var player in GameWorld.GetInstance().GetAllPlayers())
        {
            if (player == null || (race != null && player.GetRace() != race.Value))
                continue;
            PacketSendUtility.SendMessage(player, message, ChatType.BRIGHT_YELLOW_CENTER);
            deliveredCount++;
        }
        return deliveredCount;
    }

    private static List<AdminMaintenanceWarningSchedule> BuildMaintenanceWarningSchedule(int minutesUntilMaintenance)
    {
        int[] standardRemainingMinutes = { minutesUntilMaintenance, 120, 60, 30, 15, 10, 5, 1 };
        return standardRemainingMinutes
            .Where(remaining => remaining > 0 && remaining <= minutesUntilMaintenance)
            .Distinct()
            .OrderByDescending(remaining => remaining)
            .Select(remaining => new AdminMaintenanceWarningSchedule
            {
                RemainingMinutes = remaining,
                DelaySeconds = (minutesUntilMaintenance - remaining) * 60,
                Message = ""
            })
            .ToList();
    }

    private static string FormatMaintenanceWarning(string messageTemplate, int remainingMinutes)
    {
        string minuteLabel = remainingMinutes == 1 ? "minute" : "minutes";
        return messageTemplate
            .Replace("{minutes}", remainingMinutes.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{minuteLabel}", minuteLabel, StringComparison.OrdinalIgnoreCase);
    }

    private static AdminBindDestination BindDestinationFor(Player player)
    {
        var bindPoint = player.GetBindPoint();
        if (bindPoint != null)
        {
            return new AdminBindDestination
            {
                Source = "bind-point",
                WorldId = bindPoint.GetMapId(),
                X = Math.Round(bindPoint.GetX(), 2),
                Y = Math.Round(bindPoint.GetY(), 2),
                Z = Math.Round(bindPoint.GetZ(), 2),
                Heading = bindPoint.GetHeading()
            };
        }

        PlayerInitialData.LocationData locationData = DataManager.PLAYER_INITIAL_DATA.GetSpawnLocation(player.GetRace());
        return new AdminBindDestination
        {
            Source = "initial-spawn",
            WorldId = locationData.GetMapId(),
            X = Math.Round(locationData.GetX(), 2),
            Y = Math.Round(locationData.GetY(), 2),
            Z = Math.Round(locationData.GetZ(), 2),
            Heading = locationData.GetHeading()
        };
    }

    private static AdminBindDestination InstanceExitDestinationFor(Player player)
    {
        var instanceExit = DataManager.INSTANCE_EXIT_DATA.GetInstanceExit(player.GetWorldId(), player.GetRace());
        if (instanceExit != null && InstanceService.InstanceExists(instanceExit.GetExitWorld(), 1))
        {
            return new AdminBindDestination
            {
                Source = "instance-exit",
                WorldId = instanceExit.GetExitWorld(),
                X = Math.Round(instanceExit.GetX(), 2),
                Y = Math.Round(instanceExit.GetY(), 2),
                Z = Math.Round(instanceExit.GetZ(), 2),
                Heading = instanceExit.GetH()
            };
        }

        var destination = BindDestinationFor(player);
        destination.Source = "bind-fallback";
        return destination;
    }

    private static AdminPositionSnapshot PositionSnapshotFor(Player player, string source)
    {
        return new AdminPositionSnapshot
        {
            Source = source,
            WorldId = player.GetWorldId(),
            InstanceId = player.GetInstanceId(),
            X = Math.Round(player.GetX(), 2),
            Y = Math.Round(player.GetY(), 2),
            Z = Math.Round(player.GetZ(), 2),
            Heading = player.GetHeading()
        };
    }

    private static AdminMailboxSnapshot SnapshotMailbox(Aion.GameServer.Model.GameObjects.Players.Mailbox? mailbox)
    {
        return new AdminMailboxSnapshot
        {
            TotalCount = mailbox?.Size() ?? 0,
            UnreadCount = mailbox?.GetUnreadCount() ?? 0,
            UnreadExpressCount = mailbox?.GetUnreadCountByType(LetterType.EXPRESS) ?? 0,
            UnreadBlackCloudCount = mailbox?.GetUnreadCountByType(LetterType.BLACKCLOUD) ?? 0
        };
    }

    private static AdminInventorySnapshot SendInventoryRefresh(Player player)
    {
        player.SetCubeLimit();
        Storage inventory = player.GetInventory();
        if (inventory.GetKinah() == 0)
            inventory.IncreaseKinah(0);

        List<Item> allItems = new List<Item>();
        var kinahItem = inventory.GetKinahItem();
        if (kinahItem != null)
            allItems.Add(kinahItem);
        var equippedItems = player.GetEquipment().GetEquippedItems();
        allItems.AddRange(equippedItems);
        allItems.AddRange(inventory.GetItems());

        var inventoryItemSplitList = new FixedElementCountSplitList<Item>(allItems, true, 10);
        inventoryItemSplitList.ForEach(part => PacketSendUtility.SendPacket(player, new SM_INVENTORY_INFO(part.IsFirst(), part, player)));
        PacketSendUtility.SendPacket(player, new SM_INVENTORY_INFO(false, new List<Item>(), player));
        PacketSendUtility.SendPacket(player, SM_CUBE_UPDATE.CubeSize(StorageType.CUBE, player));

        return SnapshotInventory(player, inventory, equippedItems.Count, allItems.Count);
    }

    private static AdminInventorySnapshot SnapshotInventory(Player player)
    {
        Storage inventory = player.GetInventory();
        int equippedItemCount = player.GetEquipment().GetEquippedItems().Count;
        int totalPacketItemCount = equippedItemCount + inventory.GetItems().Count + (inventory.GetKinahItem() == null ? 0 : 1);
        return SnapshotInventory(player, inventory, equippedItemCount, totalPacketItemCount);
    }

    private static AdminInventorySnapshot SnapshotInventory(Player player, Storage inventory, int equippedItemCount, int totalPacketItemCount)
    {
        return new AdminInventorySnapshot
        {
            CubeItemCount = inventory.Size(),
            EquippedItemCount = equippedItemCount,
            TotalPacketItemCount = totalPacketItemCount,
            CubeLimit = inventory.GetLimit(),
            CubeFreeSlots = inventory.GetFreeSlots(),
            Kinah = inventory.GetKinah()
        };
    }

    private static AdminWarehouseSnapshot SendWarehouseRefresh(Player player)
    {
        player.SetWarehouseLimit();
        WarehouseService.SendWarehouseInfo(player, true);
        PacketSendUtility.SendPacket(player, SM_CUBE_UPDATE.CubeSize(StorageType.REGULAR_WAREHOUSE, player));

        return SnapshotWarehouse(player);
    }

    private static AdminWarehouseSnapshot SnapshotWarehouse(Player player)
    {
        IStorage characterWarehouse = player.GetStorage(StorageType.REGULAR_WAREHOUSE.GetId());
        IStorage accountWarehouse = player.GetStorage(StorageType.ACCOUNT_WAREHOUSE.GetId());
        return new AdminWarehouseSnapshot
        {
            CharacterWarehouseItemCount = characterWarehouse?.Size() ?? 0,
            CharacterWarehouseLimit = characterWarehouse?.GetLimit() ?? 0,
            CharacterWarehouseFreeSlots = characterWarehouse?.GetFreeSlots() ?? 0,
            AccountWarehouseItemCount = accountWarehouse?.Size() ?? 0,
            AccountWarehouseLimit = accountWarehouse?.GetLimit() ?? StorageType.ACCOUNT_WAREHOUSE.GetLimit(),
            AccountWarehouseFreeSlots = accountWarehouse?.GetFreeSlots() ?? 0,
            AccountWarehouseKinah = accountWarehouse?.GetKinah() ?? 0
        };
    }

    private static string NormalizeRequiredText(string? value, string label, int maxLength)
    {
        string normalized = (value ?? "").Trim();
        if (normalized.Length == 0)
            throw new ArgumentException(label + " is required.");
        if (normalized.Length > maxLength)
            throw new ArgumentException(label + " must be " + maxLength + " characters or fewer.");
        return normalized;
    }

    private static string NormalizeOptionalText(string? value, string label, int maxLength)
    {
        string normalized = (value ?? "").Trim();
        if (normalized.Length > maxLength)
            throw new ArgumentException(label + " must be " + maxLength + " characters or fewer.");
        return normalized;
    }

    private static async Task WriteJsonAsync(HttpListenerContext ctx, int status, object payload)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts);
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }

    private static bool FixedTimeTokenEquals(string? provided, string? expected)
    {
        if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(expected))
            return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));
    }

    private sealed class AdminMailRequest
    {
        public int RecipientCharacterId { get; set; }
        public string? RecipientName { get; set; }
        public string? SenderName { get; set; }
        public string? Title { get; set; }
        public string? Message { get; set; }
        public int ItemId { get; set; }
        public long ItemCount { get; set; }
        public long Kinah { get; set; }
    }

    private sealed class AdminMailValidation
    {
        public bool Valid => Errors.Count == 0;
        public int RecipientCharacterId { get; set; }
        public string? RecipientName { get; set; }
        public string SenderName { get; set; } = "Aion Portal";
        public string Title { get; set; } = "Admin Delivery";
        public string Message { get; set; } = " ";
        public bool Online { get; set; }
        public int MailboxLetters { get; set; }
        public int MailboxLimit { get; set; } = 200;
        public bool RecipientNotFound { get; set; }
        public bool MailboxFull { get; set; }
        public int ItemId { get; set; }
        public long ItemCount { get; set; }
        public string? ItemName { get; set; }
        public long ItemMaxStackCount { get; set; }
        public long Kinah { get; set; }
        public long KinahMaxAttachment { get; set; } = int.MaxValue;
        public bool KinahCapEnabled { get; set; }
        public long KinahCapValue { get; set; }
        public long? RecipientKinah { get; set; }
        public bool KinahWouldExceedCap { get; set; }
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
    }

    private sealed class AdminMailBatchValidationRequest
    {
        public int RecipientCharacterId { get; set; }
        public string? RecipientName { get; set; }
        public string? SenderName { get; set; }
        public string? Title { get; set; }
        public string? Message { get; set; }
        public List<AdminMailBatchEntryRequest>? Entries { get; set; }
    }

    private sealed class AdminMailBatchEntryRequest
    {
        public int ItemId { get; set; }
        public long ItemCount { get; set; }
        public long Kinah { get; set; }
    }

    private sealed class AdminMailBatchValidation
    {
        public bool Valid => Errors.Count == 0 && Entries.All(entry => entry.Valid);
        public int RecipientCharacterId { get; set; }
        public string? RecipientName { get; set; }
        public bool Online { get; set; }
        public int MailboxLetters { get; set; }
        public int MailboxLimit { get; set; } = 200;
        public int ValidEntryCount { get; set; }
        public string SenderName { get; set; } = "Aion Portal";
        public string Title { get; set; } = "Admin Delivery";
        public string Message { get; set; } = " ";
        public long KinahTotal { get; set; }
        public long KinahMaxAttachment { get; set; } = int.MaxValue;
        public bool KinahCapEnabled { get; set; }
        public long KinahCapValue { get; set; }
        public long? RecipientKinah { get; set; }
        public bool KinahWouldExceedCap { get; set; }
        public List<AdminMailBatchEntryValidation> Entries { get; } = new();
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
    }

    private sealed class AdminMailBatchEntryValidation
    {
        public bool Valid => Errors.Count == 0;
        public int Index { get; set; }
        public int ItemId { get; set; }
        public long ItemCount { get; set; }
        public long Kinah { get; set; }
        public string? ItemName { get; set; }
        public long ItemMaxStackCount { get; set; }
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
    }

    private sealed class AdminItemStorageValidationRequest
    {
        public int ItemId { get; set; }
        public bool IsSoulBound { get; set; }
        public int TargetStorageId { get; set; }
        public string? TargetPolicy { get; set; }
        public string? ItemCount { get; set; }
        public int CurrentStorageId { get; set; }
        public string? CurrentSlot { get; set; }
        public int CurrentStorageLimit { get; set; }
    }

    private sealed class AdminItemStorageValidation
    {
        public bool Valid => Errors.Count == 0;
        public int ItemId { get; set; }
        public string? ItemName { get; set; }
        public int ItemMask { get; set; }
        public string? ItemQuality { get; set; }
        public string? ItemType { get; set; }
        public string? ItemGroup { get; set; }
        public long MaxStackCount { get; set; }
        public bool Kinah { get; set; }
        public bool LimitOne { get; set; }
        public bool CanSplit { get; set; }
        public bool Breakable { get; set; }
        public bool Deletable { get; set; }
        public string? ItemCount { get; set; }
        public bool? CountAllowed { get; set; }
        public int CurrentStorageId { get; set; }
        public string? CurrentSlot { get; set; }
        public int CurrentStorageLimit { get; set; }
        public bool? SlotAllowed { get; set; }
        public bool RowSoulBound { get; set; }
        public bool TemplateSoulBound { get; set; }
        public bool EffectiveSoulBound { get; set; }
        public bool Tradeable { get; set; }
        public bool StorableInCharacterWarehouse { get; set; }
        public bool StorableInAccountWarehouse { get; set; }
        public string TargetPolicy { get; set; } = "";
        public bool? TargetAllowed { get; set; }
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
    }

    private sealed class AdminNotifyPlayerRequest
    {
        public int RecipientCharacterId { get; set; }
        public string? RecipientName { get; set; }
        public string? Message { get; set; }
    }

    private sealed class AdminKickPlayerRequest
    {
        public int RecipientCharacterId { get; set; }
        public string? RecipientName { get; set; }
        public string? Reason { get; set; }
    }

    private sealed class AdminMoveToBindPointRequest
    {
        public int RecipientCharacterId { get; set; }
        public string? RecipientName { get; set; }
        public string? Reason { get; set; }
    }

    private sealed class AdminRefreshMailboxRequest
    {
        public int RecipientCharacterId { get; set; }
        public string? RecipientName { get; set; }
        public string? Reason { get; set; }
    }

    private sealed class AdminMoveToInstanceExitRequest
    {
        public int RecipientCharacterId { get; set; }
        public string? RecipientName { get; set; }
        public string? Reason { get; set; }
    }

    private sealed class AdminUnstuckPlayerRequest
    {
        public int RecipientCharacterId { get; set; }
        public string? RecipientName { get; set; }
        public string? Reason { get; set; }
    }

    private sealed class AdminRefreshInventoryRequest
    {
        public int RecipientCharacterId { get; set; }
        public string? RecipientName { get; set; }
        public string? Reason { get; set; }
    }

    private sealed class AdminRefreshWarehouseRequest
    {
        public int RecipientCharacterId { get; set; }
        public string? RecipientName { get; set; }
        public string? Reason { get; set; }
    }

    private sealed class AdminRefreshAccountWarehouseRequest
    {
        public int AccountId { get; set; }
        public string? AccountName { get; set; }
        public string? Reason { get; set; }
    }

    private sealed class AdminValidatePlayerItemActionRequest
    {
        public int RecipientCharacterId { get; set; }
        public string? RecipientName { get; set; }
        public int ItemUniqueId { get; set; }
        public int StorageId { get; set; }
        public string? Action { get; set; }
        public int TargetSlot { get; set; } = -1;
        public string? TargetCount { get; set; }
    }

    private sealed class AdminPlayerItemActionValidation
    {
        public bool Valid => Errors.Count == 0;
        public Player? Player { get; set; }
        public StorageType? StorageType { get; set; }
        public IStorage? Storage { get; set; }
        public Item? Item { get; set; }
        public string Action { get; set; } = "";
        public int RecipientCharacterId { get; set; }
        public string? RecipientName { get; set; }
        public int ItemUniqueId { get; set; }
        public int ItemId { get; set; }
        public string? ItemName { get; set; }
        public string ItemCount { get; set; } = "";
        public string MaxStackCount { get; set; } = "";
        public int StorageId { get; set; }
        public string StorageName { get; set; } = "";
        public string CurrentSlot { get; set; } = "";
        public int TargetSlot { get; set; } = -1;
        public string TargetCount { get; set; } = "";
        public int StorageLimit { get; set; }
        public bool Changed { get; set; }
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
    }

    private sealed class AdminDiscardPlayerItemRequest
    {
        public int RecipientCharacterId { get; set; }
        public string? RecipientName { get; set; }
        public int ItemUniqueId { get; set; }
        public int StorageId { get; set; }
        public string? Reason { get; set; }
    }

    private sealed class AdminRepairItemSlotRequest
    {
        public int RecipientCharacterId { get; set; }
        public string? RecipientName { get; set; }
        public int ItemUniqueId { get; set; }
        public int StorageId { get; set; }
        public int TargetSlot { get; set; } = -1;
        public string? Reason { get; set; }
    }

    private sealed class AdminRepairItemCountRequest
    {
        public int RecipientCharacterId { get; set; }
        public string? RecipientName { get; set; }
        public int ItemUniqueId { get; set; }
        public int StorageId { get; set; }
        public string? TargetCount { get; set; }
        public string? Reason { get; set; }
    }

    private sealed class AdminReloadCacheRequest
    {
        public string? Target { get; set; }
        public string? Reason { get; set; }
    }

    private sealed class AdminBindDestination
    {
        public string Source { get; set; } = "";
        public int WorldId { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public int Heading { get; set; }
    }

    private sealed class AdminPositionSnapshot
    {
        public string Source { get; set; } = "";
        public int WorldId { get; set; }
        public int InstanceId { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public int Heading { get; set; }
    }

    private sealed class AdminMailboxSnapshot
    {
        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
        public int UnreadExpressCount { get; set; }
        public int UnreadBlackCloudCount { get; set; }
    }

    private sealed class AdminInventorySnapshot
    {
        public int CubeItemCount { get; set; }
        public int EquippedItemCount { get; set; }
        public int TotalPacketItemCount { get; set; }
        public int CubeLimit { get; set; }
        public int CubeFreeSlots { get; set; }
        public long Kinah { get; set; }
    }

    private sealed class AdminWarehouseSnapshot
    {
        public int CharacterWarehouseItemCount { get; set; }
        public int CharacterWarehouseLimit { get; set; }
        public int CharacterWarehouseFreeSlots { get; set; }
        public int AccountWarehouseItemCount { get; set; }
        public int AccountWarehouseLimit { get; set; }
        public int AccountWarehouseFreeSlots { get; set; }
        public long AccountWarehouseKinah { get; set; }
    }

    private sealed class AdminBroadcastMessageRequest
    {
        public string? Scope { get; set; }
        public string? Message { get; set; }
    }

    private sealed class AdminMaintenanceWarningRequest
    {
        public string? Scope { get; set; }
        public int MinutesUntilMaintenance { get; set; }
        public string? MessageTemplate { get; set; }
    }

    private sealed class AdminMaintenanceWarningSchedule
    {
        public int RemainingMinutes { get; set; }
        public int DelaySeconds { get; set; }
        public string Message { get; set; } = "";
    }
}
