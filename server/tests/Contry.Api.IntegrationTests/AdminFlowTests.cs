using System.Net;
using System.Net.Http.Json;
using Contry.Api.Features.Auth;
using Contry.Api.Features.Ranked.Challenges;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Contry.Api.IntegrationTests;

public sealed class AdminFlowTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task AdminPutChallenge_MissingXsrf_ReturnsBadRequestBeforeAuthorization()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var cookies = await LoginAsAdminAsync(client);
        var refreshOnlyCookies = new Dictionary<string, string>
        {
            ["contry_refresh"] = cookies["contry_refresh"]
        };

        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var request = CreateRequest(HttpMethod.Put, $"/ranked/challenges/{today}", refreshOnlyCookies);
        request.Content = JsonContent.Create(new SaveAdminRankedChallengeRequest("FR", ["continent", "population", "coordinates", "hemisphere", "temperature_avg_c"], true));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<ProblemResponse>())!;
        Assert.Equal("/problems/security/missing-xsrf-token", problem.Type);
    }

    [Fact]
    public async Task AdminPutChallenge_WithXsrf_ReturnsUpdatedChallengePayload()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var cookies = await LoginAsAdminAsync(client);
        var xsrfResponse = await client.SendAsync(CreateRequest(HttpMethod.Get, "/xsrf", cookies));
        Assert.Equal(HttpStatusCode.OK, xsrfResponse.StatusCode);
        var xsrf = (await xsrfResponse.Content.ReadFromJsonAsync<XsrfTokenResponse>())!;

        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var request = CreateRequest(HttpMethod.Put, $"/ranked/challenges/{today}", cookies, xsrf.Token);
        request.Content = JsonContent.Create(new SaveAdminRankedChallengeRequest("FR", ["continent", "population", "coordinates", "hemisphere", "temperature_avg_c"], true));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = (await response.Content.ReadFromJsonAsync<AdminRankedChallengeEditorResponse>())!;
        Assert.Equal("FR", payload.TargetCountryId);
        Assert.Equal("France", payload.TargetCountryName);
        Assert.True(payload.SessionsReset);
    }

    [Fact]
    public async Task AdminChallengeEditor_GetToday_ReturnsEditorPayload()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var cookies = await LoginAsAdminAsync(client);
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var response = await client.SendAsync(CreateRequest(HttpMethod.Get, $"/ranked/challenges/{today}", cookies));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AdminRankedChallengeEditorResponse>();
        Assert.NotNull(payload);
        Assert.Equal(5, payload.SelectedClues.Count);
        Assert.NotEmpty(payload.Countries);
        Assert.NotEmpty(payload.AvailableClues);
    }

    [Fact]
    public async Task NonAdminChallengeEditor_GetToday_ReturnsForbidden()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var register = await client.PostAsJsonAsync("/users", new RegisterUserRequest("admin-ui-user", "admin-ui-user@example.com", "Password123!"));
        var cookies = ParseCookies(register);
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var response = await client.SendAsync(CreateRequest(HttpMethod.Get, $"/ranked/challenges/{today}", cookies));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminChallengeEditor_SaveAndDeleteTomorrowSchedule_Works()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var cookies = await LoginAsAdminAsync(client);
        var xsrfResponse = await client.SendAsync(CreateRequest(HttpMethod.Get, "/xsrf", cookies));
        var xsrf = (await xsrfResponse.Content.ReadFromJsonAsync<XsrfTokenResponse>())!;
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd");

        var saveRequest = CreateRequest(HttpMethod.Put, $"/ranked/challenges/{tomorrow}", cookies, xsrf.Token);
        saveRequest.Content = JsonContent.Create(new SaveAdminRankedChallengeRequest("FR", ["continent", "population", "coordinates", "hemisphere", "temperature_avg_c"], false));
        var saveResponse = await client.SendAsync(saveRequest);
        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        var saved = (await saveResponse.Content.ReadFromJsonAsync<AdminRankedChallengeEditorResponse>())!;
        Assert.True(saved.IsPersisted);
        Assert.Equal("FR", saved.TargetCountryId);

        var deleteRequest = CreateRequest(HttpMethod.Delete, $"/ranked/challenges/{tomorrow}", cookies, xsrf.Token);
        var deleteResponse = await client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        var deleted = (await deleteResponse.Content.ReadFromJsonAsync<DeleteAdminRankedChallengeResponse>())!;
        Assert.True(deleted.Deleted);
        Assert.False(deleted.SessionsReset);
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
