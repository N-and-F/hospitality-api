using System.Net.Http.Json;
using System.Text.Json;

namespace Hospitality.Api.Tests;

public sealed class HealthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_returns_ok_without_auth()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Swagger_json_is_at_standard_path()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Home_page_is_the_counter_app()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Guesthouse Counter", html);
        Assert.Contains("New transaction", html);
        Assert.Contains("id=\"txn-date\"", html);
        Assert.Contains("Transaction history", html);
        Assert.Contains("Daily", html);
        Assert.Contains("Weekly", html);
        Assert.Contains("Monthly", html);
        Assert.Contains("Settings", html);
        Assert.Contains("Wallet Balances", html);
        Assert.Contains("Add account", html);
        Assert.Contains("Accounts &amp; roles", html);
        Assert.Contains("data-settings-menu", html);
    }

    [Fact]
    public async Task App_css_honors_hidden_on_flex_and_grid()
    {
        var client = factory.CreateClient();
        var css = await client.GetStringAsync("/app.css");
        Assert.Contains("[hidden]", css);
        Assert.Contains("display: none !important", css);
        Assert.Contains(".serving-line:nth-child(odd)", css);
        Assert.DoesNotContain("border-left-color: var(--accent)", css);
        Assert.Contains("text-align: center;", css);
        Assert.Contains(".stepper input::-webkit-inner-spin-button", css);
        Assert.DoesNotContain(".settings-options-label", css);
        Assert.Contains("[data-felogan-settings-menu]", css);
        Assert.Contains("[data-catalog-section=\"menu-items\"]:not([hidden])", css);
        Assert.Contains("#felogan-rooms-list .item,\n#felogan-cooks-list .item {\n  display: flex;", css);
        Assert.Contains(".history-table .text-link {\n  color: var(--accent);", css);
        Assert.Contains(".history-date {\n  display: flex;", css);
        Assert.Contains(".menu-quantity-fields legend {\n  display: block;\n  width: 100%;\n  margin-bottom: 0.15rem;\n  text-align: center;", css);
        Assert.Contains(".toast {\n  display: flex;\n  align-items: center;", css);
        Assert.Contains(".toast.toast-error {\n  background: var(--danger);", css);
        Assert.Contains(".cook-share-form {\n  display: flex;\n  align-items: center;", css);
        Assert.Contains(".menu-form {\n  display: flex;\n  flex-wrap: nowrap;", css);
        Assert.Contains(".menu-form input,\n.menu-form select {\n  flex: 1;", css);
        Assert.Contains(".paid-cook-cell {\n  display: inline-flex;", css);
        Assert.Contains(".history-table .icon-btn {\n  color: var(--accent);", css);
    }
}
