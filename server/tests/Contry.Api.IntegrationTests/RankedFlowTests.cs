using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Contry.Api.Features.Auth;
using Contry.Api.Features.Ranked.Guesses;
using Contry.Api.Features.Ranked.Sessions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Contry.Api.IntegrationTests;

public sealed class RankedFlowTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task GetCurrentRankedSession_BeforeGuess_ReturnsNotStarted()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var (_, cookies) = await RegisterAndGetCookiesAsync(client, "ranked-not-started", "ranked-not-started@example.com");
        var response = await client.SendAsync(CreateRequest(HttpMethod.Get, "/ranked/sessions/current", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<RankedSessionResponse>())!;
        Assert.Equal("not_started", session.Status);
        Assert.Empty(session.Guesses);
    }

    [Fact]
    public async Task RankedGuess_MissingXsrf_ReturnsBadRequest()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var (_, cookies) = await RegisterAndGetCookiesAsync(client, "ranked-xsrf", "ranked-xsrf@example.com");
        var request = CreateRequest(HttpMethod.Post, "/ranked/guesses", cookies);
        request.Content = JsonContent.Create(new CreateRankedGuessRequest("MD"));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<ProblemResponse>())!;
        Assert.Equal("/problems/security/missing-xsrf-token", problem.Type);
    }

    [Fact]
    public async Task RankedGuess_CreatesAuthoritativeSession_AndRejectsDuplicates()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var (_, cookies, xsrf) = await RegisterAndGetXsrfAsync(client, "ranked-play", "ranked-play@example.com");
        var firstCountryId = await GetNonWinningCountryIdAsync();

        var firstRequest = CreateRequest(HttpMethod.Post, "/ranked/guesses", cookies, xsrf);
        firstRequest.Content = JsonContent.Create(new CreateRankedGuessRequest(firstCountryId));
        var firstResponse = await client.SendAsync(firstRequest);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var created = (await firstResponse.Content.ReadFromJsonAsync<CreateRankedGuessResponse>())!;
        Assert.Equal("playing", created.Status);
        Assert.Equal(1, created.GuessCount);
        Assert.Equal(firstCountryId, created.Guess.GuessCountryId);
        Assert.Equal(5, created.Guess.Results.Count);
        Assert.DoesNotContain(created.Guess.Results, result => result.ClueId == "continent" && result.Value == "NO DATA");
        Assert.DoesNotContain(created.Guess.Results, result => result.ClueId == "population" && result.Value == "NO DATA");
        Assert.DoesNotContain(created.Guess.Results, result => result.ClueId == "temperature_avg_c" && result.Value == "NO DATA");
        var firstResponseText = await firstResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"clue\":", firstResponseText, StringComparison.Ordinal);
        Assert.DoesNotContain("\"clues\":", firstResponseText, StringComparison.Ordinal);
        Assert.DoesNotContain("\"guesses\":", firstResponseText, StringComparison.Ordinal);

        var duplicateRequest = CreateRequest(HttpMethod.Post, "/ranked/guesses", cookies, xsrf);
        duplicateRequest.Content = JsonContent.Create(new CreateRankedGuessRequest(firstCountryId));
        var duplicateResponse = await client.SendAsync(duplicateRequest);

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        var duplicateProblem = (await duplicateResponse.Content.ReadFromJsonAsync<ProblemResponse>())!;
        Assert.Equal("/problems/ranked/duplicate-guess", duplicateProblem.Type);

        var sessionResponse = await client.SendAsync(CreateRequest(HttpMethod.Get, "/ranked/sessions/current", cookies));
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        var session = (await sessionResponse.Content.ReadFromJsonAsync<RankedSessionResponse>())!;
        Assert.Equal("playing", session.Status);
        Assert.Single(session.Guesses);
    }

    [Fact]
    public async Task RankedGuess_CorrectGuess_CompletesSession()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var (_, cookies, xsrf) = await RegisterAndGetXsrfAsync(client, "ranked-win", "ranked-win@example.com");
        var targetCountryId = await GetCurrentTargetCountryIdAsync();

        var guessRequest = CreateRequest(HttpMethod.Post, "/ranked/guesses", cookies, xsrf);
        guessRequest.Content = JsonContent.Create(new CreateRankedGuessRequest(targetCountryId));
        var guessResponse = await client.SendAsync(guessRequest);

        Assert.Equal(HttpStatusCode.OK, guessResponse.StatusCode);
        var guess = (await guessResponse.Content.ReadFromJsonAsync<CreateRankedGuessResponse>())!;
        Assert.Equal("won", guess.Status);
        Assert.NotNull(guess.CompletedAtUtc);
        Assert.Equal(targetCountryId, guess.Guess.GuessCountryId);

        var anotherGuess = CreateRequest(HttpMethod.Post, "/ranked/guesses", cookies, xsrf);
        anotherGuess.Content = JsonContent.Create(new CreateRankedGuessRequest(await GetNonWinningCountryIdAsync()));
        var completedResponse = await client.SendAsync(anotherGuess);

        Assert.Equal(HttpStatusCode.Conflict, completedResponse.StatusCode);
        var problem = (await completedResponse.Content.ReadFromJsonAsync<ProblemResponse>())!;
        Assert.Equal("/problems/ranked/session-completed", problem.Type);
    }

    private static async Task<(AuthSessionResponse Session, Dictionary<string, string> Cookies)> RegisterAndGetCookiesAsync(HttpClient client, string username, string email)
    {
        var registerResponse = await client.PostAsJsonAsync("/users", new RegisterUserRequest(username, email, "Password123!"));
        registerResponse.EnsureSuccessStatusCode();
        return ((await registerResponse.Content.ReadFromJsonAsync<AuthSessionResponse>())!, ParseCookies(registerResponse));
    }

    private static async Task<(AuthSessionResponse Session, Dictionary<string, string> Cookies, string Xsrf)> RegisterAndGetXsrfAsync(HttpClient client, string username, string email)
    {
        var (session, cookies) = await RegisterAndGetCookiesAsync(client, username, email);
        var xsrfResponse = await client.SendAsync(CreateRequest(HttpMethod.Get, "/xsrf", cookies));
        xsrfResponse.EnsureSuccessStatusCode();
        var xsrf = (await xsrfResponse.Content.ReadFromJsonAsync<XsrfTokenResponse>())!;
        return (session, cookies, xsrf.Token);
    }

    private static async Task<string> GetCurrentTargetCountryIdAsync()
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var countriesPath = "/home/makkusu/uni/contry/server/datasets/base/countries.csv";
        var countryIds = (await File.ReadAllLinesAsync(countriesPath))
            .Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Split(',', 2)[0].Trim().ToUpperInvariant())
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var input = Encoding.UTF8.GetBytes(today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        var hash = SHA256.HashData(input);
        var index = BitConverter.ToUInt32(hash, 0) % countryIds.Count;
        return countryIds[(int)index];
    }

    private static async Task<string> GetNonWinningCountryIdAsync()
    {
        var targetCountryId = await GetCurrentTargetCountryIdAsync();
        return targetCountryId == "MD" ? "RO" : "MD";
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string uri, IReadOnlyDictionary<string, string> cookies, string? xsrfToken = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("Cookie", string.Join("; ", cookies.Select(entry => $"{entry.Key}={entry.Value}")));

        if (!string.IsNullOrWhiteSpace(xsrfToken))
        {
            request.Headers.Add("X-XSRF-TOKEN", xsrfToken);
        }

        return request;
    }

    private static Dictionary<string, string> ParseCookies(HttpResponseMessage response)
    {
        var cookies = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var setCookie in response.Headers.GetValues("Set-Cookie"))
        {
            var firstSegment = setCookie.Split(';', 2)[0];
            var separatorIndex = firstSegment.IndexOf('=');
            cookies[firstSegment[..separatorIndex]] = firstSegment[(separatorIndex + 1)..];
        }

        return cookies;
    }
}
