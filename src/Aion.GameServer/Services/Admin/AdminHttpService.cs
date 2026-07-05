using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services.Mail;
using Aion.GameServer.Services.Players;
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
    private const string MailPath = "/admin/express-item-mail";
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
        _logger.LogInformation("Admin HTTP endpoint listening on {Prefix} (POST {Path})", prefix, MailPath);
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

            if (!string.Equals(req.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(req.Url?.AbsolutePath?.TrimEnd('/'), MailPath, StringComparison.Ordinal))
            {
                await WriteJsonAsync(ctx, 404, new { ok = false, error = "Not found." });
                return;
            }

            string body;
            using (var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8))
                body = await reader.ReadToEndAsync();

            AdminMailRequest? dto;
            try
            {
                dto = JsonSerializer.Deserialize<AdminMailRequest>(body, JsonOpts);
            }
            catch
            {
                dto = null;
            }

            if (dto == null)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "Invalid JSON body." });
                return;
            }

            // Resolve the recipient name (SystemMailService.SendMail keys off the name). Accept either an explicit
            // name or the portal's character id (its primary key).
            string? recipientName = dto.RecipientName;
            if (string.IsNullOrWhiteSpace(recipientName))
            {
                if (dto.RecipientCharacterId <= 0)
                {
                    await WriteJsonAsync(ctx, 400, new { ok = false, error = "recipientName or recipientCharacterId is required." });
                    return;
                }

                var pcd = PlayerService.GetOrLoadPlayerCommonData(dto.RecipientCharacterId);
                if (pcd == null)
                {
                    await WriteJsonAsync(ctx, 404, new { ok = false, error = "Recipient character was not found." });
                    return;
                }

                recipientName = pcd.GetName();
            }

            if (dto.ItemId <= 0 || dto.ItemCount <= 0)
            {
                await WriteJsonAsync(ctx, 400, new { ok = false, error = "itemId and itemCount must be positive." });
                return;
            }

            var sender = string.IsNullOrWhiteSpace(dto.SenderName) ? "Aion Portal" : dto.SenderName!;
            var title = string.IsNullOrWhiteSpace(dto.Title) ? "Admin Delivery" : dto.Title!;
            var message = string.IsNullOrWhiteSpace(dto.Message) ? " " : dto.Message!;

            // Captured before the send purely so the response can report whether a live notification went out.
            bool online = GameWorld.GetInstance().GetPlayer(recipientName) != null;

            bool ok = SystemMailService.SendMail(sender, recipientName, title, message, dto.ItemId, dto.ItemCount, dto.Kinah, LetterType.EXPRESS);
            if (!ok)
            {
                await WriteJsonAsync(ctx, 422,
                    new { ok = false, error = "Delivery rejected (mailbox full, unknown item id, or invalid recipient)." });
                return;
            }

            _logger.LogInformation("Admin API: express item mail {ItemId}x{Count} -> {Recipient} ({Delivery})",
                dto.ItemId, dto.ItemCount, recipientName, online ? "online-notified" : "offline");
            await WriteJsonAsync(ctx, 200, new { ok = true, delivered = online ? "online" : "offline", recipientName });
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
}
