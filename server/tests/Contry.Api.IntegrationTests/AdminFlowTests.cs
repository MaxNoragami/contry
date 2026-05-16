using System.Net;
using System.Net.Http.Json;
using Contry.Api.Features.Auth;
using Contry.Api.Features.Ranked.Challenges;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Contry.Api.IntegrationTests;

public sealed class AdminFlowTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task AdminPutTarget_MissingXsrf_ReturnsBadRequestBeforeAuthorization()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var cookies = await LoginAsAdminAsync(client);
        var refreshOnlyCookies = new Dictionary<string, string>
        {
            ["contry_refresh"] = cookies["contry_refresh"]
        };

        var request = CreateRequest(HttpMethod.Put, "/ranked/challenges/today/target", refreshOnlyCookies);
        request.Content = JsonContent.Create(new SetTodayRankedTargetRequest("FR"));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<ProblemResponse>())!;
        Assert.Equal("/problems/security/missing-xsrf-token", problem.Type);
    }

    [Fact]
    public async Task AdminPutTarget_WithXsrf_ReturnsUpdatedTargetPayload()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var cookies = await LoginAsAdminAsync(client);
        var xsrfResponse = await client.SendAsync(CreateRequest(HttpMethod.Get, "/xsrf", cookies));
        Assert.Equal(HttpStatusCode.OK, xsrfResponse.StatusCode);
        var xsrf = (await xsrfResponse.Content.ReadFromJsonAsync<XsrfTokenResponse>())!;

        var request = CreateRequest(HttpMethod.Put, "/ranked/challenges/today/target", cookies, xsrf.Token);
        request.Content = JsonContent.Create(new SetTodayRankedTargetRequest("FR"));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = (await response.Content.ReadFromJsonAsync<SetTodayRankedTargetResponse>())!;
        Assert.Equal("FR", payload.TargetCountryId);
        Assert.Equal("France", payload.TargetCountryName);
        Assert.True(payload.SessionsReset);
    }

    private static async Task<Dictionary<string, string>> LoginAsAdminAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/sessions", new CreateSessionRequest(TestWebApplicationFactory.AdminUsername, TestWebApplicationFactory.AdminPassword));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return ParseCookies(response);
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
