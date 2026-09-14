using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Hospitality.Api.Data;
using Microsoft.Extensions.DependencyInjection;

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

    [Fact]
    public async Task Admin_can_archive_unarchive_and_rename_a_room()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);

        var list = await client.GetFromJsonAsync<JsonElement>("/v1/felogan/rooms");
        var room303 = list.GetProperty("items").EnumerateArray()
            .First(x => x.GetProperty("name").GetString() == "303");
        var id = room303.GetProperty("id").GetGuid();

        var archived = await client.PostAsync($"/v1/felogan/rooms/{id}/archive", null);
        archived.EnsureSuccessStatusCode();

        var active = await client.GetFromJsonAsync<JsonElement>("/v1/felogan/rooms");
        Assert.DoesNotContain(
            active.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("name").GetString()),
            n => n == "303");

        var withArchived = await client.GetFromJsonAsync<JsonElement>("/v1/felogan/rooms?includeArchived=true");
        Assert.Contains(
            withArchived.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("name").GetString()),
            n => n == "303");

        var twice = await client.PostAsync($"/v1/felogan/rooms/{id}/archive", null);
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        Assert.Equal("ALREADY_ARCHIVED", await ReadErrorCode(twice));

        var restored = await client.PostAsync($"/v1/felogan/rooms/{id}/unarchive", null);
        restored.EnsureSuccessStatusCode();
        var activeAgain = await client.GetFromJsonAsync<JsonElement>("/v1/felogan/rooms");
        Assert.Contains(
            activeAgain.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("name").GetString()),
            n => n == "303");

        var renamed = await client.PutAsJsonAsync($"/v1/felogan/rooms/{id}", new { name = "303-renamed" });
        renamed.EnsureSuccessStatusCode();

        using var scope = factory.Services
            .GetRequiredService<IServiceScopeFactory>()
            .CreateScope();
        await global::Hospitality.Api.Felogan.FeloganSeed.EnsureAsync(
            scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            CancellationToken.None);

        var afterSeed = await client.GetFromJsonAsync<JsonElement>("/v1/felogan/rooms");
        Assert.Contains("303-renamed", afterSeed.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("name").GetString()));
        Assert.DoesNotContain("303", afterSeed.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task Room_lifecycle_returns_required_errors()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);

        var missing = await client.PostAsync($"/v1/felogan/rooms/{Guid.NewGuid()}/archive", null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("ROOM_NOT_FOUND", await ReadErrorCode(missing));

        var list = await client.GetFromJsonAsync<JsonElement>("/v1/felogan/rooms");
        var activeId = list.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var alreadyActive = await client.PostAsync($"/v1/felogan/rooms/{activeId}/unarchive", null);
        Assert.Equal(HttpStatusCode.Conflict, alreadyActive.StatusCode);
        Assert.Equal("NOT_ARCHIVED", await ReadErrorCode(alreadyActive));
    }

    [Fact]
    public async Task Room_name_uniqueness_applies_only_to_active_rooms()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);

        var list = await client.GetFromJsonAsync<JsonElement>("/v1/felogan/rooms");
        var rooms = list.GetProperty("items").EnumerateArray().ToArray();
        var roomF1Id = rooms.First(x => x.GetProperty("name").GetString() == "F1")
            .GetProperty("id").GetGuid();

        var activeRename = await client.PutAsJsonAsync(
            $"/v1/felogan/rooms/{roomF1Id}",
            new { name = "f2" });
        Assert.Equal(HttpStatusCode.Conflict, activeRename.StatusCode);
        Assert.Equal("NAME_TAKEN", await ReadErrorCode(activeRename));

        var archived = await client.PostAsync($"/v1/felogan/rooms/{roomF1Id}/archive", null);
        archived.EnsureSuccessStatusCode();
        var replacement = await client.PostAsJsonAsync("/v1/felogan/rooms", new { name = "f1" });
        replacement.EnsureSuccessStatusCode();

        var blockedUnarchive = await client.PostAsync(
            $"/v1/felogan/rooms/{roomF1Id}/unarchive",
            null);
        Assert.Equal(HttpStatusCode.Conflict, blockedUnarchive.StatusCode);
        Assert.Equal("NAME_TAKEN", await ReadErrorCode(blockedUnarchive));

        var archivedRename = await client.PutAsJsonAsync(
            $"/v1/felogan/rooms/{roomF1Id}",
            new { name = "F2" });
        archivedRename.EnsureSuccessStatusCode();
        using var renamedDoc = JsonDocument.Parse(await archivedRename.Content.ReadAsStringAsync());
        Assert.Equal("F2", renamedDoc.RootElement.GetProperty("name").GetString());
        Assert.NotEqual(JsonValueKind.Null, renamedDoc.RootElement.GetProperty("archivedAt").ValueKind);
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
