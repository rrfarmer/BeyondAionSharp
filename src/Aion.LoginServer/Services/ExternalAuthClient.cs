using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Aion.LoginServer.Services;

public sealed record ExternalAuthResponse(string AccountId, int AionAuthResponseId);

public interface IExternalAuthClient
{
	Task<ExternalAuthResponse?> AuthenticateAsync(string user, string password, string url, CancellationToken cancellationToken = default);
}

public sealed class ExternalAuthClient : IExternalAuthClient
{
	private readonly HttpClient _httpClient;
	private readonly ILogger<ExternalAuthClient> _logger;

	public ExternalAuthClient(HttpClient httpClient, ILogger<ExternalAuthClient> logger)
	{
		_httpClient = httpClient;
		_logger = logger;
	}

	public async Task<ExternalAuthResponse?> AuthenticateAsync(string user, string password, string url, CancellationToken cancellationToken = default)
	{
		try
		{
			using var request = new HttpRequestMessage(HttpMethod.Post, url);
			request.Headers.UserAgent.ParseAdd("AionLS");
			request.Content = JsonContent.Create(new ExternalAuthRequest(user, password));
			using var response = await _httpClient.SendAsync(request, cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				var body = await response.Content.ReadAsStringAsync(cancellationToken);
				_logger.LogWarning("External auth returned status code {StatusCode}{Body}", (int)response.StatusCode, string.IsNullOrEmpty(body) ? string.Empty : $": {body}");
				return null;
			}

			var auth = await response.Content.ReadFromJsonAsync<ExternalAuthWireResponse>(cancellationToken);
			return auth == null ? null : new ExternalAuthResponse(auth.AccountId, auth.AionAuthResponseId);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not login user {User}", user);
			return null;
		}
	}

	private sealed record ExternalAuthRequest(
		[property: JsonPropertyName("user")] string User,
		[property: JsonPropertyName("password")] string Password);

	private sealed record ExternalAuthWireResponse(
		[property: JsonPropertyName("accountId")] string AccountId,
		[property: JsonPropertyName("aionAuthResponseId")] int AionAuthResponseId);
}
