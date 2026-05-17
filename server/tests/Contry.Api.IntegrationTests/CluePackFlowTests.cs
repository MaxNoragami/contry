using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Contry.Api.Features.Auth;
using Contry.Api.Features.CluePacks;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Contry.Api.IntegrationTests;

public sealed class CluePackFlowTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task CluePacks_PublicList_HidesPrivateItemsFromAnonymousUsers()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var (_, cookies, xsrf) = await RegisterAndGetXsrfAsync(client, "clue-owner", "clue-owner@example.com");

        await CreateCluePackAsync(client, cookies, xsrf, "public_pack", "public");
        await CreateCluePackAsync(client, cookies, xsrf, "private_pack", "private");

        var response = await client.GetAsync("/clue-packs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = (await response.Content.ReadFromJsonAsync<ListCluePacksResponse>())!;
        Assert.Contains(body.Items, item => item.DatasetId == "public_pack");
        Assert.DoesNotContain(body.Items, item => item.DatasetId == "private_pack");
    }

    [Fact]
    public async Task CluePacks_NonOwnerCannotUpdateButAdminCan()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var (_, ownerCookies, ownerXsrf) = await RegisterAndGetXsrfAsync(client, "clue-edit-owner", "clue-edit-owner@example.com");
        var created = await CreateCluePackAsync(client, ownerCookies, ownerXsrf, "shared_pack", "public");

        var (_, otherCookies, otherXsrf) = await RegisterAndGetXsrfAsync(client, "clue-edit-other", "clue-edit-other@example.com");
        var forbiddenRequest = CreateRequest(HttpMethod.Put, $"/clue-packs/{created.Id}", otherCookies, otherXsrf);
        forbiddenRequest.Content = JsonContent.Create(BuildRequest("shared_pack", "Other Label", "public"));
        var forbiddenResponse = await client.SendAsync(forbiddenRequest);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        var (adminCookies, adminXsrf) = await LoginAdminAndGetXsrfAsync(client);
        var adminRequest = CreateRequest(HttpMethod.Put, $"/clue-packs/{created.Id}", adminCookies, adminXsrf);
        adminRequest.Content = JsonContent.Create(BuildRequest("shared_pack", "Admin Override", "public"));
        var adminResponse = await client.SendAsync(adminRequest);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);

        var updated = (await adminResponse.Content.ReadFromJsonAsync<CluePackDetailResponse>())!;
        Assert.Equal("Admin Override", updated.Label);
    }

    [Fact]
    public async Task CluePacks_PrivateDetail_IsNotVisibleToAnotherUser()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var (_, ownerCookies, ownerXsrf) = await RegisterAndGetXsrfAsync(client, "clue-private-owner", "clue-private-owner@example.com");
        var created = await CreateCluePackAsync(client, ownerCookies, ownerXsrf, "private_detail_pack", "private");

        var (_, otherCookies) = await RegisterAndGetCookiesAsync(client, "clue-private-other", "clue-private-other@example.com");
        var request = CreateRequest(HttpMethod.Get, $"/clue-packs/{created.Id}", otherCookies);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<CluePackDetailResponse> CreateCluePackAsync(
        HttpClient client,
        IReadOnlyDictionary<string, string> cookies,
        string xsrfToken,
        string datasetId,
        string visibility)
    {
        var request = CreateRequest(HttpMethod.Post, "/clue-packs", cookies, xsrfToken);
        request.Content = JsonContent.Create(BuildRequest(datasetId, $"Label {datasetId}", visibility));
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CluePackDetailResponse>())!;
    }

    private static UpsertCluePackRequest BuildRequest(string datasetId, string label, string visibility)
        => new(
            datasetId,
            label,
            $"Description for {datasetId}",
            "numeric",
            "higher_lower",
            null,
            "globe",
            [],
            [new CluePackRowRequest("FR", 10), new CluePackRowRequest("DE", 20)],
            visibility);

    private static async Task<(AuthSessionResponse Session, Dictionary<string, string> Cookies)> RegisterAndGetCookiesAsync(HttpClient client, string username, string email)
    {
        var response = await client.PostAsJsonAsync("/users", new RegisterUserRequest(username, email, "Password123!"));
        response.EnsureSuccessStatusCode();
        var session = (await response.Content.ReadFromJsonAsync<AuthSessionResponse>())!;
        return (session, ParseCookies(response));
    }

    private static async Task<(AuthSessionResponse Session, Dictionary<string, string> Cookies, string Xsrf)> RegisterAndGetXsrfAsync(HttpClient client, string username, string email)
    {
        var (session, cookies) = await RegisterAndGetCookiesAsync(client, username, email);
        var xsrfResponse = await client.SendAsync(CreateRequest(HttpMethod.Get, "/xsrf", cookies));
        xsrfResponse.EnsureSuccessStatusCode();
        var xsrf = (await xsrfResponse.Content.ReadFromJsonAsync<XsrfTokenResponse>())!;
        return (session, cookies, xsrf.Token);
    }

    private static async Task<(Dictionary<string, string> Cookies, string Xsrf)> LoginAdminAndGetXsrfAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/sessions", new CreateSessionRequest(TestWebApplicationFactory.AdminUsername, TestWebApplicationFactory.AdminPassword));
        response.EnsureSuccessStatusCode();
        var cookies = ParseCookies(response);
        var xsrfResponse = await client.SendAsync(CreateRequest(HttpMethod.Get, "/xsrf", cookies));
        xsrfResponse.EnsureSuccessStatusCode();
        var xsrf = (await xsrfResponse.Content.ReadFromJsonAsync<XsrfTokenResponse>())!;
        return (cookies, xsrf.Token);
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
