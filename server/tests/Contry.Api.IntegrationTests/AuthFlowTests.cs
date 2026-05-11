using System.Net;
using System.Net.Http.Json;
using Contry.Api.Features.Auth;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Contry.Api.IntegrationTests;

public sealed class AuthFlowTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task AuthFlow_Registers_LogsIn_GetsXsrf_Refreshes_AndLogsOut()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var registerResponse = await client.PostAsJsonAsync("/users", new RegisterUserRequest("max", "max@example.com", "Password123!"));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registerBody = (await registerResponse.Content.ReadFromJsonAsync<AuthSessionResponse>())!;
        Assert.Equal("max", registerBody.User.Username);
        var cookies = ParseCookies(registerResponse);
        Assert.Contains("contry_access", cookies.Keys);
        Assert.Contains("contry_refresh", cookies.Keys);

        var meRequest = CreateRequest(HttpMethod.Get, "/users/me", cookies);
        var meResponse = await client.SendAsync(meRequest);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var xsrfResponse = await client.SendAsync(CreateRequest(HttpMethod.Get, "/xsrf", cookies));
        Assert.Equal(HttpStatusCode.OK, xsrfResponse.StatusCode);
        var xsrf = (await xsrfResponse.Content.ReadFromJsonAsync<XsrfTokenResponse>())!;

        var missingXsrfRefreshResponse = await client.SendAsync(CreateRequest(HttpMethod.Post, "/tokens/refresh", cookies));
        Assert.Equal(HttpStatusCode.BadRequest, missingXsrfRefreshResponse.StatusCode);
        var missingXsrfProblem = (await missingXsrfRefreshResponse.Content.ReadFromJsonAsync<ProblemResponse>())!;
        Assert.Equal("/problems/security/missing-xsrf-token", missingXsrfProblem.Type);
        Assert.Equal(400, missingXsrfProblem.Status);
        Assert.False(string.IsNullOrWhiteSpace(missingXsrfProblem.TraceId));

        var refreshResponse = await client.SendAsync(CreateRequest(HttpMethod.Post, "/tokens/refresh", cookies, xsrf.Token));
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var rotatedCookies = ParseCookies(refreshResponse);
        Assert.NotEqual(cookies["contry_refresh"], rotatedCookies["contry_refresh"]);

        var rotatedXsrfResponse = await client.SendAsync(CreateRequest(HttpMethod.Get, "/xsrf", rotatedCookies));
        var rotatedXsrf = (await rotatedXsrfResponse.Content.ReadFromJsonAsync<XsrfTokenResponse>())!;

        var logoutResponse = await client.SendAsync(CreateRequest(HttpMethod.Delete, "/sessions/current", rotatedCookies, rotatedXsrf.Token));
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
    }

    [Fact]
    public async Task RefreshTokenReuse_RevokesAllUserSessions()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var registerResponse = await client.PostAsJsonAsync("/users", new RegisterUserRequest("reuse-user", "reuse@example.com", "Password123!"));
        var oldCookies = ParseCookies(registerResponse);

        var oldXsrfResponse = await client.SendAsync(CreateRequest(HttpMethod.Get, "/xsrf", oldCookies));
        var oldXsrf = (await oldXsrfResponse.Content.ReadFromJsonAsync<XsrfTokenResponse>())!;

        var initialRefreshResponse = await client.SendAsync(CreateRequest(HttpMethod.Post, "/tokens/refresh", oldCookies, oldXsrf.Token));
        Assert.Equal(HttpStatusCode.OK, initialRefreshResponse.StatusCode);
        var currentCookies = ParseCookies(initialRefreshResponse);

        var currentXsrfResponse = await client.SendAsync(CreateRequest(HttpMethod.Get, "/xsrf", currentCookies));
        var currentXsrf = (await currentXsrfResponse.Content.ReadFromJsonAsync<XsrfTokenResponse>())!;

        var reuseResponse = await client.SendAsync(CreateRequest(HttpMethod.Post, "/tokens/refresh", oldCookies, oldXsrf.Token));
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
        var reuseProblem = (await reuseResponse.Content.ReadFromJsonAsync<ProblemResponse>())!;
        Assert.Equal("/problems/auth/refresh-token-reuse", reuseProblem.Type);
        Assert.Equal(401, reuseProblem.Status);

        var revokedCurrentSessionResponse = await client.SendAsync(CreateRequest(HttpMethod.Post, "/tokens/refresh", currentCookies, currentXsrf.Token));
        Assert.Equal(HttpStatusCode.Unauthorized, revokedCurrentSessionResponse.StatusCode);
        var revokedProblem = (await revokedCurrentSessionResponse.Content.ReadFromJsonAsync<ProblemResponse>())!;
        Assert.Equal("/problems/auth/refresh-token-reuse", revokedProblem.Type);
        Assert.Equal(401, revokedProblem.Status);
    }

    [Fact]
    public async Task InvalidCredentials_ReturnsUniformProblemDetails()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        await client.PostAsJsonAsync("/users", new RegisterUserRequest("bad-login", "bad-login@example.com", "Password123!"));

        var response = await client.PostAsJsonAsync("/sessions", new CreateSessionRequest("bad-login", "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<ProblemResponse>())!;
        Assert.Equal("/problems/auth/invalid-credentials", problem.Type);
        Assert.Equal(401, problem.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    [Fact]
    public async Task ValidationFailure_ReturnsFieldErrors()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var response = await client.PostAsJsonAsync("/users", new RegisterUserRequest("ab", "not-an-email", "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = (await response.Content.ReadFromJsonAsync<ProblemResponse>())!;
        Assert.Equal("/problems/validation", problem.Type);
        Assert.Equal(400, problem.Status);
        Assert.NotNull(problem.Errors);
        Assert.Contains("Username", problem.Errors!.Keys);
        Assert.Contains("Email", problem.Errors.Keys);
        Assert.Contains("Password", problem.Errors.Keys);
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
