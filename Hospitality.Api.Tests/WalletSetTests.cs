using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hospitality.Api.Tests;

public sealed class WalletSetTests
{
    [Fact]
    public async Task Get_wallet_sets_includes_seeded_main()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);

        var response = await client.GetAsync("/v1/wallet-sets");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var names = doc.RootElement.GetProperty("items").EnumerateArray()
            .Select(e => e.GetProperty("name").GetString()).ToArray();
        Assert.Contains("Main", names);
    }

    [Fact]
    public async Task Register_staff_is_assigned_main()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/v1/auth/register", new
        {
            email = "mainstaff@example.com",
            username = "mainstaff",
            password = "StaffPass1!"
        });
        register.EnsureSuccessStatusCode();
        using var tokenDoc = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenDoc.RootElement.GetProperty("access_token").GetString());

        var me = await client.GetFromJsonAsync<JsonElement>("/v1/users/current");
        Assert.Equal("Main", me.GetProperty("assignedWalletName").GetString());
        Assert.False(string.IsNullOrWhiteSpace(me.GetProperty("assignedWalletId").GetString()));
    }

    [Fact]
    public async Task Staff_unknown_wallet_query_is_forbidden()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "scope@example.com", "scopestaff");
        var response = await client.GetAsync($"/v1/wallets?walletId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Spare_wallet_is_independent_of_main()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);
        await client.PutAsJsonAsync("/v1/wallets/opening", new { cashOnHand = 1000, gcash = 2000, tpc = 0 });

        var created = await client.PostAsJsonAsync("/v1/wallet-sets", new { name = "Spare" });
        created.EnsureSuccessStatusCode();
        using var createdDoc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var spareId = createdDoc.RootElement.GetProperty("id").GetGuid();

        await client.PostAsJsonAsync($"/v1/transactions?walletId={spareId}", new
        {
            method = "add_cash_capital",
            amount = 50,
            clientRequestId = Guid.NewGuid()
        });

        var spareWallets = await client.GetFromJsonAsync<JsonElement>($"/v1/wallets?walletId={spareId}");
        Assert.Equal(50, spareWallets.GetProperty("cashOnHand").GetDecimal());

        var mainWallets = await client.GetFromJsonAsync<JsonElement>("/v1/wallets");
        Assert.Equal(1000, mainWallets.GetProperty("cashOnHand").GetDecimal());
    }

    [Fact]
    public async Task Duplicate_wallet_name_is_conflict()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);
        var response = await client.PostAsJsonAsync("/v1/wallet-sets", new { name = "main" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Staff_cannot_create_wallet_set()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeStaffAsync(client, "nowalletcreate@example.com", "nowalletcreate");
        var response = await client.PostAsJsonAsync("/v1/wallet-sets", new { name = "Spare" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_staff_without_wallet_is_validation_error()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);
        var created = await client.PostAsJsonAsync("/v1/staff", new
        {
            username = "nowallet",
            password = "StaffPass1!"
        });
        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal("WALLET_REQUIRED", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Reassign_staff_keeps_old_transactions_on_old_wallet()
    {
        await using var factory = new ApiFactory();
        var admin = factory.CreateClient();
        await AuthorizeAdminAsync(admin);
        var spare = await admin.PostAsJsonAsync("/v1/wallet-sets", new { name = "Spare" });
        spare.EnsureSuccessStatusCode();
        var spareId = (await spare.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var mainId = await MainIdAsync(admin);

        var created = await admin.PostAsJsonAsync("/v1/staff", new
        {
            email = "mover@example.com",
            username = "mover",
            password = "StaffPass1!",
            walletId = mainId
        });
        created.EnsureSuccessStatusCode();
        var staffId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        var staff = factory.CreateClient();
        await LoginAsync(staff, "mover", "StaffPass1!");
        await staff.PostAsJsonAsync("/v1/transactions", new
        {
            method = "add_cash_capital",
            amount = 10,
            clientRequestId = Guid.NewGuid()
        });

        var patched = await admin.PatchAsJsonAsync($"/v1/users/{staffId}/wallet", new { walletId = spareId });
        patched.EnsureSuccessStatusCode();

        var mainList = await admin.GetFromJsonAsync<JsonElement>($"/v1/transactions?walletId={mainId}&range=month");
        Assert.Equal(10, mainList.GetProperty("items")[0].GetProperty("amount").GetDecimal());

        await staff.PostAsJsonAsync("/v1/transactions", new
        {
            method = "add_cash_capital",
            amount = 7,
            clientRequestId = Guid.NewGuid()
        });
        var spareList = await admin.GetFromJsonAsync<JsonElement>($"/v1/transactions?walletId={spareId}&range=month");
        Assert.Equal(7, spareList.GetProperty("items")[0].GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Cannot_delete_main()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await AuthorizeAdminAsync(client);
        var mainId = await MainIdAsync(client);
        var response = await client.DeleteAsync($"/v1/wallet-sets/{mainId}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("CANNOT_DELETE_MAIN", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Delete_spare_reassigns_staff_and_drops_its_history()
    {
        await using var factory = new ApiFactory();
        var admin = factory.CreateClient();
        await AuthorizeAdminAsync(admin);
        var spareId = (await (await admin.PostAsJsonAsync("/v1/wallet-sets", new { name = "Spare" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var mainId = await MainIdAsync(admin);

        var created = await admin.PostAsJsonAsync("/v1/staff", new
        {
            username = "orphan",
            password = "StaffPass1!",
            walletId = spareId
        });
        created.EnsureSuccessStatusCode();

        await admin.PostAsJsonAsync($"/v1/transactions?walletId={spareId}", new
        {
            method = "add_cash_capital",
            amount = 25,
            clientRequestId = Guid.NewGuid()
        });
        await admin.PostAsJsonAsync("/v1/transactions", new
        {
            method = "add_cash_capital",
            amount = 3,
            clientRequestId = Guid.NewGuid()
        });

        var deleted = await admin.DeleteAsync($"/v1/wallet-sets/{spareId}");
        deleted.EnsureSuccessStatusCode();

        var list = await admin.GetFromJsonAsync<JsonElement>("/v1/staff");
        var orphan = list.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("username").GetString() == "orphan");
        Assert.Equal(mainId.ToString(), orphan.GetProperty("assignedWalletId").GetString());

        var gone = await admin.GetAsync($"/v1/wallets?walletId={spareId}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);

        var mainList = await admin.GetFromJsonAsync<JsonElement>("/v1/transactions?range=month");
        Assert.Contains(mainList.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("amount").GetDecimal() == 3);
        Assert.DoesNotContain(mainList.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("amount").GetDecimal() == 25);
    }

    private static async Task<Guid> MainIdAsync(HttpClient client)
    {
        var sets = await client.GetFromJsonAsync<JsonElement>("/v1/wallet-sets");
        return sets.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == "Main")
            .GetProperty("id").GetGuid();
    }

    private static async Task LoginAsync(HttpClient client, string username, string password)
    {
        var login = await client.PostAsJsonAsync("/v1/auth/login", new { username, password });
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
}
