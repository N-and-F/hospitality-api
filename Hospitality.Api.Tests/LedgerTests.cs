using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hospitality.Api.Tests;

public sealed class LedgerTests
{
    [Fact]
    public async Task Cash_in_updates_wallets()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);
        await SetOpeningAsync(client, cash: 1000, gcash: 2000, tpc: 0);

        var staff = factory.CreateClient();
        await AuthorizeStaffAsync(staff, "ledger1@example.com", "ledger1");
        var created = await staff.PostAsJsonAsync("/v1/transactions", new
        {
            method = "cash_in",
            amount = 500,
            profit = 10,
            profitInGcash = false,
            clientRequestId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal(510, row.RootElement.GetProperty("deltas").GetProperty("cashOnHand").GetDecimal());
        Assert.Equal(-500, row.RootElement.GetProperty("deltas").GetProperty("gcash").GetDecimal());

        var wallets = await staff.GetAsync("/v1/wallets");
        wallets.EnsureSuccessStatusCode();
        using var w = JsonDocument.Parse(await wallets.Content.ReadAsStringAsync());
        Assert.Equal(1510, w.RootElement.GetProperty("cashOnHand").GetDecimal());
        Assert.Equal(1500, w.RootElement.GetProperty("gcash").GetDecimal());
    }

    [Fact]
    public async Task Cash_out_with_charge_forces_profit_in_gcash()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "charge@example.com", "charge");
        await SeedOpeningViaAdmin(factory, cash: 1000, gcash: 0);

        var created = await client.PostAsJsonAsync("/v1/transactions", new
        {
            method = "cash_out_with_charge",
            amount = 100,
            profit = 10,
            profitInGcash = false,
            clientRequestId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.True(row.RootElement.GetProperty("profitInGcash").GetBoolean());
        Assert.Equal(-100, row.RootElement.GetProperty("deltas").GetProperty("cashOnHand").GetDecimal());
        Assert.Equal(110, row.RootElement.GetProperty("deltas").GetProperty("gcash").GetDecimal());
    }

    [Fact]
    public async Task Cash_in_honors_profit_in_gcash()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "nogcashfee@example.com", "nogcashfee");
        await SeedOpeningViaAdmin(factory, cash: 1000, gcash: 2000);

        var created = await client.PostAsJsonAsync("/v1/transactions", new
        {
            method = "cash_in",
            amount = 500,
            profit = 10,
            profitInGcash = true,
            clientRequestId = Guid.NewGuid()
        });
        created.EnsureSuccessStatusCode();
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.True(row.RootElement.GetProperty("profitInGcash").GetBoolean());
        Assert.Equal(500, row.RootElement.GetProperty("deltas").GetProperty("cashOnHand").GetDecimal());
        Assert.Equal(-490, row.RootElement.GetProperty("deltas").GetProperty("gcash").GetDecimal());
    }

    [Fact]
    public async Task Setting_wallets_after_activity_sets_current_balances()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);
        await SetOpeningAsync(client, cash: 1000, gcash: 2000, tpc: 50);
        await client.PostAsJsonAsync("/v1/transactions", new
        {
            method = "cash_in",
            amount = 500,
            profit = 10,
            profitInGcash = false,
            clientRequestId = Guid.NewGuid()
        });

        await SetOpeningAsync(client, cash: 800, gcash: 900, tpc: 40);

        var wallets = await client.GetAsync("/v1/wallets");
        wallets.EnsureSuccessStatusCode();
        using var w = JsonDocument.Parse(await wallets.Content.ReadAsStringAsync());
        Assert.Equal(800, w.RootElement.GetProperty("cashOnHand").GetDecimal());
        Assert.Equal(900, w.RootElement.GetProperty("gcash").GetDecimal());
        Assert.Equal(40, w.RootElement.GetProperty("tpc").GetDecimal());
    }

    [Fact]
    public async Task Omitting_profit_uses_suggestion()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "sug@example.com", "suguser");
        await SeedOpeningViaAdmin(factory, cash: 1000, gcash: 1000);

        var created = await client.PostAsJsonAsync("/v1/transactions", new
        {
            method = "cash_in",
            amount = 500,
            clientRequestId = Guid.NewGuid()
        });
        created.EnsureSuccessStatusCode();
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal(10, row.RootElement.GetProperty("profit").GetDecimal());
    }

    [Fact]
    public async Task Explicit_profit_wins()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "exp@example.com", "expuser");
        await SeedOpeningViaAdmin(factory, cash: 1000, gcash: 1000);

        var created = await client.PostAsJsonAsync("/v1/transactions", new
        {
            method = "cash_in",
            amount = 500,
            profit = 7,
            clientRequestId = Guid.NewGuid()
        });
        created.EnsureSuccessStatusCode();
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal(7, row.RootElement.GetProperty("profit").GetDecimal());
    }

    [Fact]
    public async Task Duplicate_client_request_id_returns_same_row()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "idemp@example.com", "idemp");
        await SeedOpeningViaAdmin(factory, cash: 1000, gcash: 1000);
        var id = Guid.NewGuid();
        var body = new { method = "add_cash_capital", amount = 50, clientRequestId = id };

        var first = await client.PostAsJsonAsync("/v1/transactions", body);
        var second = await client.PostAsJsonAsync("/v1/transactions", body);
        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();
        using var a = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        using var b = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal(a.RootElement.GetProperty("id").GetGuid(), b.RootElement.GetProperty("id").GetGuid());

        var wallets = await client.GetFromJsonAsync<JsonElement>("/v1/wallets");
        Assert.Equal(1050, wallets.GetProperty("cashOnHand").GetDecimal());
    }

    [Fact]
    public async Task Cash_out_without_cash_is_insufficient()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "broke@example.com", "broke");

        var created = await client.PostAsJsonAsync("/v1/transactions", new
        {
            method = "cash_out",
            amount = 100,
            profit = 10,
            clientRequestId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.Conflict, created.StatusCode);
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal("INSUFFICIENT_WALLET", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Tpc_load_may_go_negative()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "tpc@example.com", "tpcuser");

        var created = await client.PostAsJsonAsync("/v1/transactions", new
        {
            method = "load_via_tpc",
            amount = 50,
            profit = 5,
            clientRequestId = Guid.NewGuid()
        });
        created.EnsureSuccessStatusCode();
        var wallets = await client.GetFromJsonAsync<JsonElement>("/v1/wallets");
        Assert.Equal(-50, wallets.GetProperty("tpc").GetDecimal());
    }

    [Fact]
    public async Task Load_via_tpc_with_cashback_credits_tpc()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "tpc-cb@example.com", "tpccb");

        var created = await client.PostAsJsonAsync("/v1/transactions", new
        {
            method = "load_via_tpc",
            amount = 50,
            profit = 5,
            cashback = 1,
            clientRequestId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal(1, row.RootElement.GetProperty("cashback").GetDecimal());
        Assert.Equal(-49, row.RootElement.GetProperty("deltas").GetProperty("tpc").GetDecimal());
        Assert.Equal(55, row.RootElement.GetProperty("deltas").GetProperty("cashOnHand").GetDecimal());

        var wallets = await client.GetFromJsonAsync<JsonElement>("/v1/wallets");
        Assert.Equal(-49, wallets.GetProperty("tpc").GetDecimal());
        Assert.Equal(55, wallets.GetProperty("cashOnHand").GetDecimal());
    }

    [Fact]
    public async Task Load_via_tpc_rejects_negative_cashback()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "tpc-neg@example.com", "tpcneg");

        var created = await client.PostAsJsonAsync("/v1/transactions", new
        {
            method = "load_via_tpc",
            amount = 50,
            cashback = -1,
            clientRequestId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal("VALIDATION_ERROR", body.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Cashback_is_ignored_unless_load_via_tpc()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "tpc-ign@example.com", "tpcign");
        await SeedOpeningViaAdmin(factory, cash: 1000, gcash: 2000);

        var created = await client.PostAsJsonAsync("/v1/transactions", new
        {
            method = "cash_in",
            amount = 500,
            profit = 10,
            cashback = 1,
            clientRequestId = Guid.NewGuid()
        });
        created.EnsureSuccessStatusCode();
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal(0, row.RootElement.GetProperty("cashback").GetDecimal());
        Assert.Equal(0, row.RootElement.GetProperty("deltas").GetProperty("tpc").GetDecimal());
    }

    [Fact]
    public async Task Staff_can_update_load_via_tpc_cashback()
    {
        await using var factory = new ApiFactory();
        var staff = factory.CreateClient();
        await AuthorizeStaffAsync(staff, "tpc-edit@example.com", "tpcedit");

        var created = await staff.PostAsJsonAsync("/v1/transactions", new
        {
            method = "load_via_tpc",
            amount = 50,
            profit = 5,
            cashback = 0,
            clientRequestId = Guid.NewGuid()
        });
        created.EnsureSuccessStatusCode();
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = row.RootElement.GetProperty("id").GetGuid();

        var updated = await staff.PutAsJsonAsync($"/v1/transactions/{id}", new
        {
            method = "load_via_tpc",
            amount = 50,
            profit = 5,
            cashback = 1
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var updatedRow = JsonDocument.Parse(await updated.Content.ReadAsStringAsync());
        Assert.Equal(1, updatedRow.RootElement.GetProperty("cashback").GetDecimal());
        Assert.Equal(-49, updatedRow.RootElement.GetProperty("deltas").GetProperty("tpc").GetDecimal());

        var wallets = await staff.GetFromJsonAsync<JsonElement>("/v1/wallets");
        Assert.Equal(-49, wallets.GetProperty("tpc").GetDecimal());
    }

    [Fact]
    public async Task Load_via_gcash_convenience_deducts_gcash()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "gcash-conv@example.com", "gcashconv");
        await SeedOpeningViaAdmin(factory, cash: 0, gcash: 500);

        var created = await client.PostAsJsonAsync("/v1/transactions", new
        {
            method = "load_via_gcash",
            amount = 100,
            profit = 5,
            convenienceCharge = 2,
            clientRequestId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal(2, row.RootElement.GetProperty("convenienceCharge").GetDecimal());
        Assert.Equal(105, row.RootElement.GetProperty("deltas").GetProperty("cashOnHand").GetDecimal());
        Assert.Equal(-102, row.RootElement.GetProperty("deltas").GetProperty("gcash").GetDecimal());

        var wallets = await client.GetFromJsonAsync<JsonElement>("/v1/wallets");
        Assert.Equal(105, wallets.GetProperty("cashOnHand").GetDecimal());
        Assert.Equal(398, wallets.GetProperty("gcash").GetDecimal());
    }

    [Fact]
    public async Task Tpc_balance_inquiry_deducts_convenience_from_tpc()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "tpc-inq@example.com", "tpcinq");

        var created = await client.PostAsJsonAsync("/v1/transactions", new
        {
            method = "tpc_balance_inquiry",
            convenienceCharge = 15,
            clientRequestId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal(0, row.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal(0, row.RootElement.GetProperty("profit").GetDecimal());
        Assert.Equal(15, row.RootElement.GetProperty("convenienceCharge").GetDecimal());
        Assert.Equal(-15, row.RootElement.GetProperty("deltas").GetProperty("tpc").GetDecimal());

        var wallets = await client.GetFromJsonAsync<JsonElement>("/v1/wallets");
        Assert.Equal(-15, wallets.GetProperty("tpc").GetDecimal());
    }

    [Fact]
    public async Task Tpc_balance_inquiry_requires_convenience_charge()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "tpc-inq0@example.com", "tpcinq0");

        var created = await client.PostAsJsonAsync("/v1/transactions", new
        {
            method = "tpc_balance_inquiry",
            clientRequestId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal("VALIDATION_ERROR", body.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Staff_remittance_deducts_cash()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "remit@example.com", "remit");
        await SeedOpeningViaAdmin(factory, cash: 500, gcash: 0);

        var created = await client.PostAsJsonAsync("/v1/transactions", new
        {
            method = "staff_remittance",
            amount = 200,
            profit = 99,
            clientRequestId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal(0, row.RootElement.GetProperty("profit").GetDecimal());
        Assert.Equal(-200, row.RootElement.GetProperty("deltas").GetProperty("cashOnHand").GetDecimal());

        var wallets = await client.GetFromJsonAsync<JsonElement>("/v1/wallets");
        Assert.Equal(300, wallets.GetProperty("cashOnHand").GetDecimal());
    }

    [Fact]
    public async Task Top_up_gcash_and_tpc_do_not_require_cash()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "topup@example.com", "topup");

        var gcash = await client.PostAsJsonAsync("/v1/transactions", new
        {
            method = "top_up_gcash",
            amount = 250,
            clientRequestId = Guid.NewGuid()
        });
        gcash.EnsureSuccessStatusCode();

        var tpc = await client.PostAsJsonAsync("/v1/transactions", new
        {
            method = "top_up_tpc",
            amount = 80,
            clientRequestId = Guid.NewGuid()
        });
        tpc.EnsureSuccessStatusCode();

        var wallets = await client.GetFromJsonAsync<JsonElement>("/v1/wallets");
        Assert.Equal(0, wallets.GetProperty("cashOnHand").GetDecimal());
        Assert.Equal(250, wallets.GetProperty("gcash").GetDecimal());
        Assert.Equal(80, wallets.GetProperty("tpc").GetDecimal());
    }

    [Fact]
    public async Task Top_up_tpc_paid_in_gcash_deducts_gcash()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "tpc-gcash@example.com", "tpcgcash");
        await SeedOpeningViaAdmin(factory, cash: 0, gcash: 200);

        var created = await client.PostAsJsonAsync("/v1/transactions", new
        {
            method = "top_up_tpc",
            amount = 80,
            gcashAmount = 50,
            clientRequestId = Guid.NewGuid()
        });
        created.EnsureSuccessStatusCode();
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal(50, row.RootElement.GetProperty("gcashAmount").GetDecimal());
        Assert.Equal(0, row.RootElement.GetProperty("deltas").GetProperty("cashOnHand").GetDecimal());
        Assert.Equal(-50, row.RootElement.GetProperty("deltas").GetProperty("gcash").GetDecimal());
        Assert.Equal(80, row.RootElement.GetProperty("deltas").GetProperty("tpc").GetDecimal());

        var wallets = await client.GetFromJsonAsync<JsonElement>("/v1/wallets");
        Assert.Equal(0, wallets.GetProperty("cashOnHand").GetDecimal());
        Assert.Equal(150, wallets.GetProperty("gcash").GetDecimal());
        Assert.Equal(80, wallets.GetProperty("tpc").GetDecimal());
    }

    [Fact]
    public async Task Gcash_amount_is_ignored_unless_top_up_tpc()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "gcash-ign@example.com", "gcashign");
        await SeedOpeningViaAdmin(factory, cash: 1000, gcash: 2000);

        var created = await client.PostAsJsonAsync("/v1/transactions", new
        {
            method = "cash_in",
            amount = 500,
            profit = 10,
            gcashAmount = 80,
            clientRequestId = Guid.NewGuid()
        });
        created.EnsureSuccessStatusCode();
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal(0, row.RootElement.GetProperty("gcashAmount").GetDecimal());
        Assert.Equal(-500, row.RootElement.GetProperty("deltas").GetProperty("gcash").GetDecimal());
    }

    [Fact]
    public async Task Top_up_tpc_paid_in_gcash_rejects_insufficient_gcash()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "tpc-broke@example.com", "tpcbroke");

        var created = await client.PostAsJsonAsync("/v1/transactions", new
        {
            method = "top_up_tpc",
            amount = 80,
            gcashAmount = 80,
            clientRequestId = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.Conflict, created.StatusCode);
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal("INSUFFICIENT_WALLET", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Staff_can_update_top_up_tpc_gcash_amount()
    {
        await using var factory = new ApiFactory();
        var staff = factory.CreateClient();
        await AuthorizeStaffAsync(staff, "tpc-gcash-edit@example.com", "tpcgcashedit");
        await SeedOpeningViaAdmin(factory, cash: 0, gcash: 200);

        var created = await staff.PostAsJsonAsync("/v1/transactions", new
        {
            method = "top_up_tpc",
            amount = 80,
            clientRequestId = Guid.NewGuid()
        });
        created.EnsureSuccessStatusCode();
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = row.RootElement.GetProperty("id").GetGuid();

        var updated = await staff.PutAsJsonAsync($"/v1/transactions/{id}", new
        {
            method = "top_up_tpc",
            amount = 80,
            gcashAmount = 50
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var updatedRow = JsonDocument.Parse(await updated.Content.ReadAsStringAsync());
        Assert.Equal(50, updatedRow.RootElement.GetProperty("gcashAmount").GetDecimal());
        Assert.Equal(-50, updatedRow.RootElement.GetProperty("deltas").GetProperty("gcash").GetDecimal());
        Assert.Equal(80, updatedRow.RootElement.GetProperty("deltas").GetProperty("tpc").GetDecimal());

        var wallets = await staff.GetFromJsonAsync<JsonElement>("/v1/wallets");
        Assert.Equal(150, wallets.GetProperty("gcash").GetDecimal());
        Assert.Equal(80, wallets.GetProperty("tpc").GetDecimal());
    }

    [Fact]
    public async Task Staff_and_admin_see_all_transactions()
    {
        await using var factory = new ApiFactory();
        await SeedOpeningViaAdmin(factory, cash: 5000, gcash: 5000);

        var a = factory.CreateClient();
        await AuthorizeStaffAsync(a, "aaa@example.com", "staffaaa");
        var b = factory.CreateClient();
        await AuthorizeStaffAsync(b, "bbb@example.com", "staffbbb");
        var admin = factory.CreateClient();
        await AuthorizeAdminAsync(admin);

        await a.PostAsJsonAsync("/v1/transactions", new
        {
            method = "add_cash_capital",
            amount = 10,
            clientRequestId = Guid.NewGuid()
        });
        await b.PostAsJsonAsync("/v1/transactions", new
        {
            method = "add_cash_capital",
            amount = 20,
            clientRequestId = Guid.NewGuid()
        });

        var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd");
        var listA = await a.GetFromJsonAsync<JsonElement>($"/v1/transactions?date={today}");
        var listB = await b.GetFromJsonAsync<JsonElement>($"/v1/transactions?date={today}");
        var listAdmin = await admin.GetFromJsonAsync<JsonElement>($"/v1/transactions?date={today}");
        Assert.Equal(2, listA.GetProperty("items").GetArrayLength());
        Assert.Equal(2, listB.GetProperty("items").GetArrayLength());
        Assert.Equal(2, listAdmin.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Staff_cannot_void_admin_can()
    {
        await using var factory = new ApiFactory();
        await SeedOpeningViaAdmin(factory, cash: 1000, gcash: 0);
        var staff = factory.CreateClient();
        await AuthorizeStaffAsync(staff, "voider@example.com", "voider");
        var created = await staff.PostAsJsonAsync("/v1/transactions", new
        {
            method = "add_cash_capital",
            amount = 100,
            clientRequestId = Guid.NewGuid()
        });
        created.EnsureSuccessStatusCode();
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = row.RootElement.GetProperty("id").GetGuid();

        var staffVoid = await staff.PostAsync($"/v1/transactions/{id}/void", null);
        Assert.Equal(HttpStatusCode.Forbidden, staffVoid.StatusCode);

        var admin = factory.CreateClient();
        await AuthorizeAdminAsync(admin);
        var adminVoid = await admin.PostAsync($"/v1/transactions/{id}/void", null);
        adminVoid.EnsureSuccessStatusCode();
        var wallets = await admin.GetFromJsonAsync<JsonElement>("/v1/wallets");
        Assert.Equal(1000, wallets.GetProperty("cashOnHand").GetDecimal());
    }

    [Fact]
    public async Task Staff_can_update_own_transaction()
    {
        await using var factory = new ApiFactory();
        var staff = factory.CreateClient();
        await AuthorizeStaffAsync(staff, "editme@example.com", "editme");
        await SeedOpeningViaAdmin(factory, cash: 1000, gcash: 2000);

        var created = await staff.PostAsJsonAsync("/v1/transactions", new
        {
            method = "cash_in",
            amount = 500,
            profit = 10,
            profitInGcash = false,
            clientRequestId = Guid.NewGuid()
        });
        created.EnsureSuccessStatusCode();
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = row.RootElement.GetProperty("id").GetGuid();

        var updated = await staff.PutAsJsonAsync($"/v1/transactions/{id}", new
        {
            method = "cash_in",
            amount = 400,
            profit = 10,
            profitInGcash = false
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var updatedRow = JsonDocument.Parse(await updated.Content.ReadAsStringAsync());
        Assert.Equal(400, updatedRow.RootElement.GetProperty("amount").GetDecimal());

        var wallets = await staff.GetFromJsonAsync<JsonElement>("/v1/wallets");
        Assert.Equal(1410, wallets.GetProperty("cashOnHand").GetDecimal());
        Assert.Equal(1600, wallets.GetProperty("gcash").GetDecimal());
    }

    [Fact]
    public async Task Staff_can_delete_own_transaction()
    {
        await using var factory = new ApiFactory();
        var staff = factory.CreateClient();
        await AuthorizeStaffAsync(staff, "delme@example.com", "delme");
        await SeedOpeningViaAdmin(factory, cash: 1000, gcash: 0);

        var created = await staff.PostAsJsonAsync("/v1/transactions", new
        {
            method = "add_cash_capital",
            amount = 100,
            clientRequestId = Guid.NewGuid()
        });
        created.EnsureSuccessStatusCode();
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = row.RootElement.GetProperty("id").GetGuid();

        var deleted = await staff.DeleteAsync($"/v1/transactions/{id}");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);

        var wallets = await staff.GetFromJsonAsync<JsonElement>("/v1/wallets");
        Assert.Equal(1000, wallets.GetProperty("cashOnHand").GetDecimal());
        var list = await staff.GetFromJsonAsync<JsonElement>("/v1/transactions");
        Assert.Equal(0, list.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Staff_can_delete_another_users_transaction()
    {
        await using var factory = new ApiFactory();
        var a = factory.CreateClient();
        await AuthorizeStaffAsync(a, "ownera@example.com", "ownera");
        await SeedOpeningViaAdmin(factory, cash: 1000, gcash: 0);
        var created = await a.PostAsJsonAsync("/v1/transactions", new
        {
            method = "add_cash_capital",
            amount = 50,
            clientRequestId = Guid.NewGuid()
        });
        created.EnsureSuccessStatusCode();
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = row.RootElement.GetProperty("id").GetGuid();

        var b = factory.CreateClient();
        await AuthorizeStaffAsync(b, "ownerb@example.com", "ownerb");
        var deleted = await b.DeleteAsync($"/v1/transactions/{id}");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);

        var wallets = await b.GetFromJsonAsync<JsonElement>("/v1/wallets");
        Assert.Equal(1000, wallets.GetProperty("cashOnHand").GetDecimal());
    }

    [Fact]
    public async Task Staff_can_update_another_users_transaction()
    {
        await using var factory = new ApiFactory();
        var a = factory.CreateClient();
        await AuthorizeStaffAsync(a, "edita@example.com", "edita");
        await SeedOpeningViaAdmin(factory, cash: 1000, gcash: 0);
        var created = await a.PostAsJsonAsync("/v1/transactions", new
        {
            method = "add_cash_capital",
            amount = 50,
            clientRequestId = Guid.NewGuid()
        });
        created.EnsureSuccessStatusCode();
        using var row = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = row.RootElement.GetProperty("id").GetGuid();

        var b = factory.CreateClient();
        await AuthorizeStaffAsync(b, "editb@example.com", "editb");
        var updated = await b.PutAsJsonAsync($"/v1/transactions/{id}", new
        {
            method = "add_cash_capital",
            amount = 25
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var updatedRow = JsonDocument.Parse(await updated.Content.ReadAsStringAsync());
        Assert.Equal(25, updatedRow.RootElement.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Date_filter_uses_manila()
    {
        await using var factory = new ApiFactory();
        var staff = factory.CreateClient();
        await AuthorizeStaffAsync(staff, "tz@example.com", "tzuser");
        await SeedOpeningViaAdmin(factory, cash: 1000, gcash: 0);

        var occurredAt = new DateTimeOffset(2026, 9, 14, 0, 30, 0, TimeSpan.FromHours(8));
        await staff.PostAsJsonAsync("/v1/transactions", new
        {
            method = "add_cash_capital",
            amount = 15,
            occurredAt,
            clientRequestId = Guid.NewGuid()
        });

        var onDate = await staff.GetFromJsonAsync<JsonElement>("/v1/transactions?date=2026-09-14");
        var previous = await staff.GetFromJsonAsync<JsonElement>("/v1/transactions?date=2026-09-13");
        Assert.Equal(1, onDate.GetProperty("items").GetArrayLength());
        Assert.Equal(0, previous.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Range_week_and_month_include_matching_days()
    {
        await using var factory = new ApiFactory();
        var staff = factory.CreateClient();
        await AuthorizeStaffAsync(staff, "range@example.com", "rangeuser");
        await SeedOpeningViaAdmin(factory, cash: 1000, gcash: 0);

        await staff.PostAsJsonAsync("/v1/transactions", new
        {
            method = "add_cash_capital",
            amount = 10,
            occurredAt = new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.FromHours(8)),
            clientRequestId = Guid.NewGuid()
        });
        await staff.PostAsJsonAsync("/v1/transactions", new
        {
            method = "add_cash_capital",
            amount = 20,
            occurredAt = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.FromHours(8)),
            clientRequestId = Guid.NewGuid()
        });
        await staff.PostAsJsonAsync("/v1/transactions", new
        {
            method = "add_cash_capital",
            amount = 30,
            occurredAt = new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.FromHours(8)),
            clientRequestId = Guid.NewGuid()
        });

        var day = await staff.GetFromJsonAsync<JsonElement>("/v1/transactions?date=2026-09-14&range=day");
        var week = await staff.GetFromJsonAsync<JsonElement>("/v1/transactions?date=2026-09-14&range=week");
        var month = await staff.GetFromJsonAsync<JsonElement>("/v1/transactions?date=2026-09-14&range=month");

        Assert.Equal(1, day.GetProperty("items").GetArrayLength());
        Assert.Equal("day", day.GetProperty("range").GetString());
        Assert.Equal(2, week.GetProperty("items").GetArrayLength());
        Assert.Equal(20, week.GetProperty("items")[0].GetProperty("amount").GetDecimal());
        Assert.Equal(10, week.GetProperty("items")[1].GetProperty("amount").GetDecimal());
        Assert.Equal("2026-09-14", week.GetProperty("from").GetString());
        Assert.Equal("2026-09-20", week.GetProperty("to").GetString());
        Assert.Equal(2, month.GetProperty("items").GetArrayLength());
        Assert.Equal("2026-09-01", month.GetProperty("from").GetString());
        Assert.Equal("2026-09-30", month.GetProperty("to").GetString());
    }

    private static async Task SeedOpeningViaAdmin(ApiFactory factory, decimal cash, decimal gcash, decimal tpc = 0)
    {
        var admin = factory.CreateClient();
        await AuthorizeAdminAsync(admin);
        await SetOpeningAsync(admin, cash, gcash, tpc);
    }

    private static async Task SetOpeningAsync(HttpClient client, decimal cash, decimal gcash, decimal tpc)
    {
        var response = await client.PutAsJsonAsync("/v1/wallets/opening", new
        {
            cashOnHand = cash,
            gcash,
            tpc
        });
        response.EnsureSuccessStatusCode();
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
}
