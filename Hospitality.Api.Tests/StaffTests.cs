using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hospitality.Api.Tests;

public sealed class StaffTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Staff_cannot_list_staff()
    {
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "staffer@example.com", "staffer");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/v1/staff");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("FORBIDDEN", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Admin_can_create_and_list_staff()
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/v1/auth/login", new
        {
            username = "admin",
            password = "AdminPass1!"
        });
        login.EnsureSuccessStatusCode();
        using var tokenDoc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = tokenDoc.RootElement.GetProperty("access_token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var created = await client.PostAsJsonAsync("/v1/staff", new
        {
            email = "newstaff@example.com",
            username = "newstaff",
            password = "StaffPass1!",
            walletId = await MainWalletIdAsync(client)
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var createdDoc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal("newstaff", createdDoc.RootElement.GetProperty("username").GetString());
        Assert.Equal("Staff", createdDoc.RootElement.GetProperty("roles")[0].GetString());

        var list = await client.GetAsync("/v1/staff");
        list.EnsureSuccessStatusCode();
        using var listDoc = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var usernames = listDoc.RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(e => e.GetProperty("username").GetString())
            .ToArray();
        Assert.Contains("newstaff", usernames);
        Assert.Contains("admin", usernames);
    }

    [Fact]
    public async Task Admin_can_create_staff_without_email()
    {
        var client = factory.CreateClient();
        await SignInAdminAsync(client);

        var created = await client.PostAsJsonAsync("/v1/staff", new
        {
            username = "noemail",
            password = "StaffPass1!",
            walletId = await MainWalletIdAsync(client)
        });

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal("noemail", doc.RootElement.GetProperty("username").GetString());
        Assert.Equal("", doc.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Admin_can_create_staff_with_simple_password()
    {
        var client = factory.CreateClient();
        await SignInAdminAsync(client);

        var created = await client.PostAsJsonAsync("/v1/staff", new
        {
            username = "simplepass",
            password = "counter",
            walletId = await MainWalletIdAsync(client)
        });

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
    }

    [Fact]
    public async Task Admin_can_create_admin_account()
    {
        var client = factory.CreateClient();
        await SignInAdminAsync(client);

        var created = await client.PostAsJsonAsync("/v1/staff", new
        {
            email = "secondadmin@example.com",
            username = "secondadmin",
            password = "AdminPass1!",
            role = "Admin"
        });

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal("Admin", doc.RootElement.GetProperty("roles")[0].GetString());
    }

    [Fact]
    public async Task Admin_can_change_staff_role()
    {
        var client = factory.CreateClient();
        await SignInAdminAsync(client);

        var created = await client.PostAsJsonAsync("/v1/staff", new
        {
            email = "promoteme@example.com",
            username = "promoteme",
            password = "StaffPass1!",
            walletId = await MainWalletIdAsync(client)
        });
        created.EnsureSuccessStatusCode();
        using var createdDoc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdDoc.RootElement.GetProperty("id").GetString();

        var patched = await client.PatchAsJsonAsync($"/v1/users/{id}/role", new { role = "Admin" });
        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);
        using var patchedDoc = JsonDocument.Parse(await patched.Content.ReadAsStringAsync());
        Assert.Equal("Admin", patchedDoc.RootElement.GetProperty("roles")[0].GetString());
    }

    [Fact]
    public async Task Admin_cannot_change_own_role()
    {
        var client = factory.CreateClient();
        await SignInAdminAsync(client);

        await client.PostAsJsonAsync("/v1/staff", new
        {
            email = "otheradmin@example.com",
            username = "otheradmin",
            password = "AdminPass1!",
            role = "Admin"
        });

        var me = await client.GetAsync("/v1/users/current");
        me.EnsureSuccessStatusCode();
        using var meDoc = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        var id = meDoc.RootElement.GetProperty("id").GetString();

        var patched = await client.PatchAsJsonAsync($"/v1/users/{id}/role", new { role = "Staff" });
        Assert.Equal(HttpStatusCode.BadRequest, patched.StatusCode);
        using var doc = JsonDocument.Parse(await patched.Content.ReadAsStringAsync());
        Assert.Equal("CANNOT_CHANGE_OWN_ROLE", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Admin_cannot_demote_last_admin()
    {
        await using var isolated = new ApiFactory();
        var client = isolated.CreateClient();
        await SignInAdminAsync(client);

        var me = await client.GetAsync("/v1/users/current");
        me.EnsureSuccessStatusCode();
        using var meDoc = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        var id = meDoc.RootElement.GetProperty("id").GetString();

        var patched = await client.PatchAsJsonAsync($"/v1/users/{id}/role", new { role = "Staff" });
        Assert.Equal(HttpStatusCode.BadRequest, patched.StatusCode);
        using var doc = JsonDocument.Parse(await patched.Content.ReadAsStringAsync());
        Assert.Equal("LAST_ADMIN", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Admin_can_delete_staff_account()
    {
        var client = factory.CreateClient();
        await SignInAdminAsync(client);

        var created = await client.PostAsJsonAsync("/v1/staff", new
        {
            username = "deleteme",
            password = "StaffPass1!",
            walletId = await MainWalletIdAsync(client)
        });
        created.EnsureSuccessStatusCode();
        using var createdDoc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdDoc.RootElement.GetProperty("id").GetString();

        var deleted = await client.DeleteAsync($"/v1/users/{id}");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);

        var list = await client.GetFromJsonAsync<JsonElement>("/v1/staff");
        var usernames = list.GetProperty("items")
            .EnumerateArray()
            .Select(e => e.GetProperty("username").GetString())
            .ToArray();
        Assert.DoesNotContain("deleteme", usernames);
    }

    [Fact]
    public async Task Admin_cannot_delete_own_account()
    {
        var client = factory.CreateClient();
        await SignInAdminAsync(client);

        var me = await client.GetAsync("/v1/users/current");
        me.EnsureSuccessStatusCode();
        using var meDoc = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        var id = meDoc.RootElement.GetProperty("id").GetString();

        var deleted = await client.DeleteAsync($"/v1/users/{id}");
        Assert.Equal(HttpStatusCode.BadRequest, deleted.StatusCode);
        using var doc = JsonDocument.Parse(await deleted.Content.ReadAsStringAsync());
        Assert.Equal("CANNOT_DELETE_OWN_ACCOUNT", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Staff_cannot_delete_account()
    {
        var adminClient = factory.CreateClient();
        await SignInAdminAsync(adminClient);
        var created = await adminClient.PostAsJsonAsync("/v1/staff", new
        {
            username = "keepme",
            password = "StaffPass1!",
            walletId = await MainWalletIdAsync(adminClient)
        });
        created.EnsureSuccessStatusCode();
        using var createdDoc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdDoc.RootElement.GetProperty("id").GetString();

        var staff = factory.CreateClient();
        var token = await RegisterAsync(staff, "nodelete@example.com", "nodelete");
        staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var deleted = await staff.DeleteAsync($"/v1/users/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, deleted.StatusCode);
    }

    [Fact]
    public async Task Staff_cannot_change_role()
    {
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "norole@example.com", "norole");
        var adminClient = factory.CreateClient();
        await SignInAdminAsync(adminClient);
        var me = await adminClient.GetAsync("/v1/users/current");
        me.EnsureSuccessStatusCode();
        using var meDoc = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        var adminId = meDoc.RootElement.GetProperty("id").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var patched = await client.PatchAsJsonAsync($"/v1/users/{adminId}/role", new { role = "Staff" });
        Assert.Equal(HttpStatusCode.Forbidden, patched.StatusCode);
    }

    private static async Task SignInAdminAsync(HttpClient client)
    {
        var login = await client.PostAsJsonAsync("/v1/auth/login", new
        {
            username = "admin",
            password = "AdminPass1!"
        });
        login.EnsureSuccessStatusCode();
        using var tokenDoc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = tokenDoc.RootElement.GetProperty("access_token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<Guid> MainWalletIdAsync(HttpClient client)
    {
        var sets = await client.GetFromJsonAsync<JsonElement>("/v1/wallet-sets");
        return sets.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == "Main")
            .GetProperty("id").GetGuid();
    }

    private static async Task<string> RegisterAsync(HttpClient client, string email, string username)
    {
        var register = await client.PostAsJsonAsync("/v1/auth/register", new
        {
            email,
            username,
            password = "StaffPass1!"
        });
        register.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }
}
