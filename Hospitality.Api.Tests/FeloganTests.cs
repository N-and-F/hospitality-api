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
    public async Task Staff_cannot_list_felogan_catalogs()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "felostaff@example.com", "felostaff");

        foreach (var path in new[] { "rooms", "cooks", "menu-items" })
        {
            var response = await client.GetAsync($"/v1/felogan/{path}");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("FORBIDDEN", await ReadErrorCode(response));
        }
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

    [Fact]
    public async Task Admin_can_manage_cooks()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);

        var list = await client.GetFromJsonAsync<JsonElement>("/v1/felogan/cooks");
        var cooks = list.GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(cooks, x => x.GetProperty("name").GetString() == "Lisa");
        Assert.Contains(cooks, x => x.GetProperty("name").GetString() == "Melly");

        var lisaId = cooks.First(x => x.GetProperty("name").GetString() == "Lisa")
            .GetProperty("id").GetGuid();
        var duplicate = await client.PostAsJsonAsync("/v1/felogan/cooks", new { name = "lisa" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("NAME_TAKEN", await ReadErrorCode(duplicate));

        var renamed = await client.PutAsJsonAsync(
            $"/v1/felogan/cooks/{lisaId}",
            new { name = "Chef Lisa" });
        renamed.EnsureSuccessStatusCode();

        (await client.PostAsync($"/v1/felogan/cooks/{lisaId}/archive", null))
            .EnsureSuccessStatusCode();
        var archivedAgain = await client.PostAsync($"/v1/felogan/cooks/{lisaId}/archive", null);
        Assert.Equal(HttpStatusCode.Conflict, archivedAgain.StatusCode);
        Assert.Equal("ALREADY_ARCHIVED", await ReadErrorCode(archivedAgain));

        var hidden = await client.GetFromJsonAsync<JsonElement>("/v1/felogan/cooks");
        Assert.DoesNotContain(
            hidden.GetProperty("items").EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == lisaId);
        var visible = await client.GetFromJsonAsync<JsonElement>(
            "/v1/felogan/cooks?includeArchived=true");
        Assert.Contains(
            visible.GetProperty("items").EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == lisaId);

        (await client.PostAsJsonAsync("/v1/felogan/cooks", new { name = "chef lisa" }))
            .EnsureSuccessStatusCode();
        var blockedUnarchive = await client.PostAsync(
            $"/v1/felogan/cooks/{lisaId}/unarchive",
            null);
        Assert.Equal(HttpStatusCode.Conflict, blockedUnarchive.StatusCode);
        Assert.Equal("NAME_TAKEN", await ReadErrorCode(blockedUnarchive));

        var archivedRename = await client.PutAsJsonAsync(
            $"/v1/felogan/cooks/{lisaId}",
            new { name = "Melly" });
        archivedRename.EnsureSuccessStatusCode();
        using var renamedDoc = JsonDocument.Parse(await archivedRename.Content.ReadAsStringAsync());
        Assert.NotEqual(JsonValueKind.Null, renamedDoc.RootElement.GetProperty("archivedAt").ValueKind);

        var missing = await client.PostAsync($"/v1/felogan/cooks/{Guid.NewGuid()}/archive", null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("COOK_NOT_FOUND", await ReadErrorCode(missing));
    }

    [Fact]
    public async Task Admin_can_manage_menu_items_and_validation()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);

        var list = await client.GetFromJsonAsync<JsonElement>("/v1/felogan/menu-items");
        var items = list.GetProperty("items").EnumerateArray().ToArray();
        var tocilog = items.First(x => x.GetProperty("name").GetString() == "Tocilog");
        Assert.Equal("plate", tocilog.GetProperty("kind").GetString());
        Assert.Equal(120.00m, tocilog.GetProperty("sellPrice").GetDecimal());
        Assert.Equal(55.17m, tocilog.GetProperty("cost").GetDecimal());
        Assert.Equal(
            "extra",
            items.First(x => x.GetProperty("name").GetString() == "Egg")
                .GetProperty("kind").GetString());

        foreach (var invalid in new[]
        {
            new { name = "Bad Kind", kind = "drink", sellPrice = 10m, cost = 1m },
            new { name = "Bad Case", kind = "Plate", sellPrice = 10m, cost = 1m },
            new { name = "Bad Sell", kind = "plate", sellPrice = -1m, cost = 1m },
            new { name = "Bad Cost", kind = "extra", sellPrice = 1m, cost = -1m },
            new { name = "Precise", kind = "plate", sellPrice = 1.001m, cost = 1m }
        })
        {
            var response = await client.PostAsJsonAsync("/v1/felogan/menu-items", invalid);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("VALIDATION_ERROR", await ReadErrorCode(response));
        }

        var created = await client.PostAsJsonAsync("/v1/felogan/menu-items", new
        {
            name = "Bangsilog",
            kind = "plate",
            sellPrice = 150m,
            cost = 70m
        });
        created.EnsureSuccessStatusCode();
        using var createdDoc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdDoc.RootElement.GetProperty("id").GetGuid();

        var duplicate = await client.PostAsJsonAsync("/v1/felogan/menu-items", new
        {
            name = "bangsilog",
            kind = "plate",
            sellPrice = 1m,
            cost = 1m
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("NAME_TAKEN", await ReadErrorCode(duplicate));

        var updated = await client.PutAsJsonAsync($"/v1/felogan/menu-items/{id}", new
        {
            name = "Bangsilog Special",
            kind = "plate",
            sellPrice = 175.25m,
            cost = 80.50m
        });
        updated.EnsureSuccessStatusCode();

        (await client.PostAsync($"/v1/felogan/menu-items/{id}/archive", null))
            .EnsureSuccessStatusCode();
        var archivedAgain = await client.PostAsync($"/v1/felogan/menu-items/{id}/archive", null);
        Assert.Equal(HttpStatusCode.Conflict, archivedAgain.StatusCode);
        Assert.Equal("ALREADY_ARCHIVED", await ReadErrorCode(archivedAgain));

        var archivedUpdate = await client.PutAsJsonAsync($"/v1/felogan/menu-items/{id}", new
        {
            name = "Tocilog",
            kind = "extra",
            sellPrice = 12m,
            cost = 5m
        });
        archivedUpdate.EnsureSuccessStatusCode();
        using var archivedDoc = JsonDocument.Parse(await archivedUpdate.Content.ReadAsStringAsync());
        Assert.NotEqual(JsonValueKind.Null, archivedDoc.RootElement.GetProperty("archivedAt").ValueKind);

        var blockedUnarchive = await client.PostAsync(
            $"/v1/felogan/menu-items/{id}/unarchive",
            null);
        Assert.Equal(HttpStatusCode.Conflict, blockedUnarchive.StatusCode);
        Assert.Equal("NAME_TAKEN", await ReadErrorCode(blockedUnarchive));

        var missing = await client.PostAsync(
            $"/v1/felogan/menu-items/{Guid.NewGuid()}/unarchive",
            null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("MENU_ITEM_NOT_FOUND", await ReadErrorCode(missing));
    }

    [Fact]
    public async Task Staff_cannot_create_servings()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "felostaff3@example.com", "felostaff3");

        var response = await client.PostAsJsonAsync("/v1/felogan/servings", new
        {
            servedOn = "2026-09-06",
            roomId = Guid.NewGuid(),
            cookId = Guid.NewGuid(),
            lines = Array.Empty<object>(),
            clientRequestId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_creates_serving_and_list_totals_use_snapshot_price()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);
        var (roomId, cookId, tocilogId) = await ServingIdsAsync(client);

        var created = await client.PostAsJsonAsync("/v1/felogan/servings", new
        {
            servedOn = "2026-09-06",
            roomId,
            cookId,
            note = "303 breakfast",
            lines = new[] { new { menuItemId = tocilogId, qty = 2 } },
            clientRequestId = Guid.NewGuid()
        });
        created.EnsureSuccessStatusCode();
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal("2026-09-06", row.RootElement.GetProperty("servedOn").GetString());
        Assert.Equal(2, row.RootElement.GetProperty("totals").GetProperty("qty").GetInt32());
        Assert.Equal(240.00m, row.RootElement.GetProperty("totals").GetProperty("sell").GetDecimal());
        Assert.Equal(240.00m, row.RootElement.GetProperty("lines")[0].GetProperty("sellTotal").GetDecimal());
        Assert.Equal(55.17m, row.RootElement.GetProperty("lines")[0].GetProperty("cost").GetDecimal());

        var menuItemUpdate = await client.PutAsJsonAsync($"/v1/felogan/menu-items/{tocilogId}", new
        {
            name = "Tocilog",
            kind = "plate",
            sellPrice = 200m,
            cost = 55.17m
        });
        menuItemUpdate.EnsureSuccessStatusCode();

        var list = await client.GetFromJsonAsync<JsonElement>(
            "/v1/felogan/servings?date=2026-09-06&range=day");
        Assert.Equal(1, list.GetProperty("totals").GetProperty("count").GetInt32());
        Assert.Equal(2, list.GetProperty("totals").GetProperty("qty").GetInt32());
        Assert.Equal(240.00m, list.GetProperty("totals").GetProperty("sell").GetDecimal());
        Assert.Equal(
            240.00m,
            list.GetProperty("items")[0].GetProperty("totals").GetProperty("sell").GetDecimal());
    }

    [Fact]
    public async Task Create_serving_rejects_empty_or_zero_qty_lines()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);
        var (roomId, cookId, tocilogId) = await ServingIdsAsync(client);

        var empty = await client.PostAsJsonAsync("/v1/felogan/servings", new
        {
            servedOn = "2026-09-06",
            roomId,
            cookId,
            lines = Array.Empty<object>(),
            clientRequestId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ReadErrorCode(empty));

        var zero = await client.PostAsJsonAsync("/v1/felogan/servings", new
        {
            servedOn = "2026-09-06",
            roomId,
            cookId,
            lines = new[] { new { menuItemId = tocilogId, qty = 0 } },
            clientRequestId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ReadErrorCode(zero));
    }

    [Fact]
    public async Task Create_serving_rejects_duplicate_menu_item_ids()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);
        var (roomId, cookId, tocilogId) = await ServingIdsAsync(client);

        var response = await client.PostAsJsonAsync("/v1/felogan/servings", new
        {
            servedOn = "2026-09-06",
            roomId,
            cookId,
            lines = new[]
            {
                new { menuItemId = tocilogId, qty = 1 },
                new { menuItemId = tocilogId, qty = 2 }
            },
            clientRequestId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ReadErrorCode(response));
    }

    [Fact]
    public async Task Create_serving_stores_explicit_minimum_date()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);
        var (roomId, cookId, tocilogId) = await ServingIdsAsync(client);

        var response = await client.PostAsJsonAsync("/v1/felogan/servings", new
        {
            servedOn = "0001-01-01",
            roomId,
            cookId,
            lines = new[] { new { menuItemId = tocilogId, qty = 1 } },
            clientRequestId = Guid.NewGuid()
        });

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("0001-01-01", document.RootElement.GetProperty("servedOn").GetString());
    }

    [Fact]
    public async Task Duplicate_client_request_id_replays_for_same_admin()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);
        var (roomId, cookId, tocilogId) = await ServingIdsAsync(client);
        var requestId = Guid.NewGuid();
        var body = new
        {
            servedOn = "2026-09-06",
            roomId,
            cookId,
            lines = new[] { new { menuItemId = tocilogId, qty = 1 } },
            clientRequestId = requestId
        };

        var first = await client.PostAsJsonAsync("/v1/felogan/servings", body);
        var replay = await client.PostAsJsonAsync("/v1/felogan/servings", body);
        first.EnsureSuccessStatusCode();
        replay.EnsureSuccessStatusCode();
        using var firstDoc = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        using var replayDoc = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
        Assert.Equal(
            firstDoc.RootElement.GetProperty("id").GetGuid(),
            replayDoc.RootElement.GetProperty("id").GetGuid());

        var createdUser = await client.PostAsJsonAsync("/v1/staff", new
        {
            email = "feloadmin2@example.com",
            username = "feloadmin2",
            password = "AdminPass1!",
            role = "Admin"
        });
        createdUser.EnsureSuccessStatusCode();

        var otherAdmin = factory.CreateClient();
        var login = await otherAdmin.PostAsJsonAsync("/v1/auth/login", new
        {
            username = "feloadmin2",
            password = "AdminPass1!"
        });
        login.EnsureSuccessStatusCode();
        using var tokenDoc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        otherAdmin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            tokenDoc.RootElement.GetProperty("access_token").GetString());

        var conflict = await otherAdmin.PostAsJsonAsync("/v1/felogan/servings", body);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("CLIENT_REQUEST_CONFLICT", await ReadErrorCode(conflict));
    }

    [Fact]
    public async Task Update_qty_keeps_snapshot_price_and_delete_removes_row()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);
        var (roomId, cookId, tocilogId) = await ServingIdsAsync(client);
        var created = await client.PostAsJsonAsync("/v1/felogan/servings", new
        {
            servedOn = "2026-09-06",
            roomId,
            cookId,
            lines = new[] { new { menuItemId = tocilogId, qty = 2 } },
            clientRequestId = Guid.NewGuid()
        });
        created.EnsureSuccessStatusCode();
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = row.RootElement.GetProperty("id").GetGuid();

        var menuItemUpdate = await client.PutAsJsonAsync($"/v1/felogan/menu-items/{tocilogId}", new
        {
            name = "Tocilog",
            kind = "plate",
            sellPrice = 200m,
            cost = 55.17m
        });
        menuItemUpdate.EnsureSuccessStatusCode();

        var updated = await client.PutAsJsonAsync($"/v1/felogan/servings/{id}", new
        {
            servedOn = "2026-09-06",
            roomId,
            cookId,
            lines = new[] { new { menuItemId = tocilogId, qty = 3 } }
        });
        updated.EnsureSuccessStatusCode();
        using var updatedDoc = JsonDocument.Parse(await updated.Content.ReadAsStringAsync());
        Assert.Equal(3, updatedDoc.RootElement.GetProperty("totals").GetProperty("qty").GetInt32());
        Assert.Equal(360.00m, updatedDoc.RootElement.GetProperty("totals").GetProperty("sell").GetDecimal());

        var deleted = await client.DeleteAsync($"/v1/felogan/servings/{id}");
        deleted.EnsureSuccessStatusCode();
        using var deletedDoc = JsonDocument.Parse(await deleted.Content.ReadAsStringAsync());
        Assert.Equal(id, deletedDoc.RootElement.GetProperty("id").GetGuid());

        var list = await client.GetFromJsonAsync<JsonElement>("/v1/felogan/servings?date=2026-09-06");
        Assert.Equal(0, list.GetProperty("items").GetArrayLength());

        var missing = await client.DeleteAsync($"/v1/felogan/servings/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("SERVING_NOT_FOUND", await ReadErrorCode(missing));
    }

    [Fact]
    public async Task Serving_week_range_is_manila_monday_sunday()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);
        var (roomId, cookId, tocilogId) = await ServingIdsAsync(client);

        async Task Post(string day) =>
            (await client.PostAsJsonAsync("/v1/felogan/servings", new
            {
                servedOn = day,
                roomId,
                cookId,
                lines = new[] { new { menuItemId = tocilogId, qty = 1 } },
                clientRequestId = Guid.NewGuid()
            })).EnsureSuccessStatusCode();

        await Post("2026-09-14");
        await Post("2026-09-20");
        await Post("2026-09-21");

        var week = await client.GetFromJsonAsync<JsonElement>(
            "/v1/felogan/servings?date=2026-09-16&range=week");
        Assert.Equal(2, week.GetProperty("items").GetArrayLength());
        Assert.Equal("week", week.GetProperty("range").GetString());
    }

    [Fact]
    public async Task Archived_room_cannot_be_used_on_create_but_existing_serving_can_keep_it()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);
        var (roomId, cookId, tocilogId) = await ServingIdsAsync(client);
        var created = await client.PostAsJsonAsync("/v1/felogan/servings", new
        {
            servedOn = "2026-09-06",
            roomId,
            cookId,
            lines = new[] { new { menuItemId = tocilogId, qty = 1 } },
            clientRequestId = Guid.NewGuid()
        });
        created.EnsureSuccessStatusCode();
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var servingId = row.RootElement.GetProperty("id").GetGuid();

        var archived = await client.PostAsync($"/v1/felogan/rooms/{roomId}/archive", null);
        archived.EnsureSuccessStatusCode();

        var blocked = await client.PostAsJsonAsync("/v1/felogan/servings", new
        {
            servedOn = "2026-09-07",
            roomId,
            cookId,
            lines = new[] { new { menuItemId = tocilogId, qty = 1 } },
            clientRequestId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.NotFound, blocked.StatusCode);
        Assert.Equal("ROOM_NOT_FOUND", await ReadErrorCode(blocked));

        var keep = await client.PutAsJsonAsync($"/v1/felogan/servings/{servingId}", new
        {
            servedOn = "2026-09-06",
            roomId,
            cookId,
            lines = new[] { new { menuItemId = tocilogId, qty = 1 } }
        });
        keep.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Update_serving_rejects_duplicate_menu_item_ids()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);
        var (roomId, cookId, tocilogId) = await ServingIdsAsync(client);
        var created = await client.PostAsJsonAsync("/v1/felogan/servings", new
        {
            servedOn = "2026-09-06",
            roomId,
            cookId,
            lines = new[] { new { menuItemId = tocilogId, qty = 1 } },
            clientRequestId = Guid.NewGuid()
        });
        created.EnsureSuccessStatusCode();
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var servingId = row.RootElement.GetProperty("id").GetGuid();

        var archived = await client.PostAsync(
            $"/v1/felogan/menu-items/{tocilogId}/archive",
            null);
        archived.EnsureSuccessStatusCode();

        var updated = await client.PutAsJsonAsync($"/v1/felogan/servings/{servingId}", new
        {
            servedOn = "2026-09-06",
            roomId,
            cookId,
            lines = new[]
            {
                new { menuItemId = tocilogId, qty = 1 },
                new { menuItemId = tocilogId, qty = 1 }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, updated.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await ReadErrorCode(updated));
    }

    [Fact]
    public async Task Update_serving_rejects_archived_cook_replacement()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);
        var (roomId, cookId, tocilogId) = await ServingIdsAsync(client);
        var cooks = await client.GetFromJsonAsync<JsonElement>("/v1/felogan/cooks");
        var archivedCookId = cooks.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("name").GetString() == "Melly")
            .GetProperty("id").GetGuid();
        var created = await client.PostAsJsonAsync("/v1/felogan/servings", new
        {
            servedOn = "2026-09-06",
            roomId,
            cookId,
            lines = new[] { new { menuItemId = tocilogId, qty = 1 } },
            clientRequestId = Guid.NewGuid()
        });
        created.EnsureSuccessStatusCode();
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var servingId = row.RootElement.GetProperty("id").GetGuid();

        var archived = await client.PostAsync(
            $"/v1/felogan/cooks/{archivedCookId}/archive",
            null);
        archived.EnsureSuccessStatusCode();

        var updated = await client.PutAsJsonAsync($"/v1/felogan/servings/{servingId}", new
        {
            servedOn = "2026-09-06",
            roomId,
            cookId = archivedCookId,
            lines = new[] { new { menuItemId = tocilogId, qty = 1 } }
        });

        Assert.Equal(HttpStatusCode.NotFound, updated.StatusCode);
        Assert.Equal("COOK_NOT_FOUND", await ReadErrorCode(updated));
    }

    [Fact]
    public async Task Update_serving_rejects_new_archived_menu_item_line()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);
        var (roomId, cookId, tocilogId) = await ServingIdsAsync(client);
        var menu = await client.GetFromJsonAsync<JsonElement>("/v1/felogan/menu-items");
        var archivedMenuItemId = menu.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("name").GetString() == "Egg")
            .GetProperty("id").GetGuid();
        var created = await client.PostAsJsonAsync("/v1/felogan/servings", new
        {
            servedOn = "2026-09-06",
            roomId,
            cookId,
            lines = new[] { new { menuItemId = tocilogId, qty = 1 } },
            clientRequestId = Guid.NewGuid()
        });
        created.EnsureSuccessStatusCode();
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var servingId = row.RootElement.GetProperty("id").GetGuid();

        var archived = await client.PostAsync(
            $"/v1/felogan/menu-items/{archivedMenuItemId}/archive",
            null);
        archived.EnsureSuccessStatusCode();

        var updated = await client.PutAsJsonAsync($"/v1/felogan/servings/{servingId}", new
        {
            servedOn = "2026-09-06",
            roomId,
            cookId,
            lines = new[]
            {
                new { menuItemId = tocilogId, qty = 1 },
                new { menuItemId = archivedMenuItemId, qty = 1 }
            }
        });

        Assert.Equal(HttpStatusCode.NotFound, updated.StatusCode);
        Assert.Equal("MENU_ITEM_NOT_FOUND", await ReadErrorCode(updated));
    }

    private static async Task<(Guid RoomId, Guid CookId, Guid TocilogId)> ServingIdsAsync(
        HttpClient client)
    {
        var rooms = await client.GetFromJsonAsync<JsonElement>("/v1/felogan/rooms");
        var cooks = await client.GetFromJsonAsync<JsonElement>("/v1/felogan/cooks");
        var menu = await client.GetFromJsonAsync<JsonElement>("/v1/felogan/menu-items");
        return (
            rooms.GetProperty("items").EnumerateArray()
                .First(x => x.GetProperty("name").GetString() == "303")
                .GetProperty("id").GetGuid(),
            cooks.GetProperty("items").EnumerateArray()
                .First(x => x.GetProperty("name").GetString() == "Lisa")
                .GetProperty("id").GetGuid(),
            menu.GetProperty("items").EnumerateArray()
                .First(x => x.GetProperty("name").GetString() == "Tocilog")
                .GetProperty("id").GetGuid());
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
