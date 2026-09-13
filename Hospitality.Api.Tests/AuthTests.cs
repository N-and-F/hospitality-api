using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hospitality.Api.Tests;

public sealed class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{

    [Fact]
    public async Task Register_returns_bearer_token()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/auth/register", new
        {
            email = "jane@example.com",
            username = "jane",
            password = "StaffPass1!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("access_token").GetString()));
        Assert.Equal("Bearer", doc.RootElement.GetProperty("token_type").GetString());
    }

    [Fact]
    public async Task Register_duplicate_email_returns_conflict()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/register", new
        {
            email = "dup@example.com",
            username = "dupuser1",
            password = "StaffPass1!"
        });

        var response = await client.PostAsJsonAsync("/v1/auth/register", new
        {
            email = "dup@example.com",
            username = "dupuser2",
            password = "StaffPass1!"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("EMAIL_TAKEN", await ReadErrorCode(response));
    }

    [Fact]
    public async Task Register_duplicate_username_returns_conflict()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/register", new
        {
            email = "a1@example.com",
            username = "sameuser",
            password = "StaffPass1!"
        });

        var response = await client.PostAsJsonAsync("/v1/auth/register", new
        {
            email = "a2@example.com",
            username = "sameuser",
            password = "StaffPass1!"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("USERNAME_TAKEN", await ReadErrorCode(response));
    }

    [Fact]
    public async Task Login_with_username_returns_token()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/register", new
        {
            email = "login@example.com",
            username = "loginuser",
            password = "StaffPass1!"
        });

        var response = await client.PostAsJsonAsync("/v1/auth/login", new
        {
            username = "loginuser",
            password = "StaffPass1!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("access_token").GetString()));
    }

    [Fact]
    public async Task Login_wrong_password_returns_invalid_credentials()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/v1/auth/register", new
        {
            email = "wrong@example.com",
            username = "wrongpass",
            password = "StaffPass1!"
        });

        var response = await client.PostAsJsonAsync("/v1/auth/login", new
        {
            username = "wrongpass",
            password = "NopeNope1!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", await ReadErrorCode(response));
    }

    private static async Task<string?> ReadErrorCode(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("error").GetProperty("code").GetString();
    }
}
