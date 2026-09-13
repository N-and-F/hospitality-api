using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hospitality.Api.Tests;

public sealed class CurrentUserTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Current_user_without_token_returns_401()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/v1/users/current");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("UNAUTHORIZED", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Current_user_after_register_returns_staff_profile()
    {
        var client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/v1/auth/register", new
        {
            email = "me@example.com",
            username = "meuser",
            password = "StaffPass1!"
        });
        register.EnsureSuccessStatusCode();
        using var tokenDoc = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        var token = tokenDoc.RootElement.GetProperty("access_token").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/v1/users/current");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("me@example.com", doc.RootElement.GetProperty("email").GetString());
        Assert.Equal("meuser", doc.RootElement.GetProperty("username").GetString());
        var roles = doc.RootElement.GetProperty("roles").EnumerateArray()
            .Select(e => e.GetString())
            .Where(r => r is not null)
            .Cast<string>()
            .ToArray();
        Assert.Equal(new[] { "Staff" }, roles);
    }
}
