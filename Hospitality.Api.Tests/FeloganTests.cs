using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hospitality.Api.Tests;

public sealed class FeloganTests
{
    [Fact]
    public async Task Staff_cannot_list_felogan_rooms()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "felostaff@example.com", "felostaff");

        var response = await client.GetAsync("/v1/felogan/rooms");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("FORBIDDEN", await ReadErrorCode(response));
    }

    [Fact]
    public async Task Admin_lists_seeded_rooms_and_rejects_duplicate_active_name()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);

        var list = await client.GetFromJsonAsync<JsonElement>("/v1/felogan/rooms");
        var names = list.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("name").GetString())
            .ToArray();
        Assert.Contains("303", names);
        Assert.Contains("F1", names);
        Assert.DoesNotContain(names, n => n is null);

        var dup = await client.PostAsJsonAsync("/v1/felogan/rooms", new { name = "303" });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("NAME_TAKEN", await ReadErrorCode(dup));

        var created = await client.PostAsJsonAsync("/v1/felogan/rooms", new { name = "Penthouse" });
        created.EnsureSuccessStatusCode();
        using var createdDoc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal("Penthouse", createdDoc.RootElement.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, createdDoc.RootElement.GetProperty("archivedAt").ValueKind);
    }

    [Fact]
    public async Task Admin_rejects_null_room_name()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);

        var response = await client.PostAsJsonAsync("/v1/felogan/rooms", new { name = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ReadErrorCode(response));
    }

    private static async Task AuthorizeAdminAsync(HttpClient client)
    {
        var login = await client.PostAsJsonAsync("/v1/auth/login", new
        {
            username = "admin",
            password = "AdminPass1!"
        });
        login.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", doc.RootElement.GetProperty("access_token").GetString());
    }

    private static async Task AuthorizeStaffAsync(HttpClient client, string email, string username)
    {
        var register = await client.PostAsJsonAsync("/v1/auth/register", new
        {
            email,
            username,
            password = "StaffPass1!"
        });
        register.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", doc.RootElement.GetProperty("access_token").GetString());
    }

    private static async Task<string?> ReadErrorCode(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("error").GetProperty("code").GetString();
    }
}
