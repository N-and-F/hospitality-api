using System.Net;

namespace Hospitality.Api.Tests;

public sealed class WebUiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Home_includes_chooser_felogan_shell_and_cache_busted_assets()
    {
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/");

        Assert.Contains("id=\"chooser-view\"", html);
        Assert.Contains("id=\"choose-felogan\"", html);
        Assert.Contains("id=\"choose-counter\"", html);
        Assert.Contains("id=\"felogan-view\"", html);
        Assert.Contains("id=\"switch-app\"", html);
        Assert.Contains("/app.css?v=settlement", html);
        Assert.Contains("/app.js?v=settlement", html);
        Assert.Contains("/felogan.js?v=settlement", html);
        Assert.Contains("id=\"felogan-additional-profit\"", html);
        Assert.Contains("id=\"felogan-settlement\"", html);
        Assert.DoesNotContain("Sign in to the counter", html);
    }

    [Fact]
    public async Task Scripts_include_app_selection_and_felogan_workflows()
    {
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/");
        var app = await client.GetStringAsync("/app.js");
        var feloganResponse = await client.GetAsync("/felogan.js");

        Assert.Equal(HttpStatusCode.OK, feloganResponse.StatusCode);
        var felogan = await feloganResponse.Content.ReadAsStringAsync();
        Assert.Contains("id=\"chooser-error\"", html);
        Assert.Contains("hospitality.app", app);
        Assert.Contains("openFeloganApp", app);
        Assert.Contains("showError(chooserError, error.message)", app);
        Assert.Contains(
            "await loadApp();\n    sessionStorage.setItem(appKey, \"counter\");",
            app);
        Assert.Contains(
            "await window.openFeloganApp();\n    sessionStorage.setItem(appKey, \"felogan\");",
            app);
        Assert.Contains("window.bootHospitality", app);
        Assert.DoesNotContain("if (token())", app);
        Assert.Contains("window.openFeloganApp", felogan);
        Assert.Contains("window.bootHospitality()", felogan);
        Assert.Contains("/v1/felogan/servings", felogan);
        Assert.Contains("includeArchived=true", felogan);
        Assert.Contains("<th>Profit</th><th>Cook</th><th>Paid cook</th><th>Note</th><th>Edit</th><th>Delete</th>", felogan);
        Assert.Contains(".join(\"<br>\")", felogan);
        Assert.Contains("${item.qty}× ${escapeHtml(item.name)}", felogan);
        Assert.DoesNotContain("data-label=\"Cost\"", felogan);
    }

    [Fact]
    public async Task Felogan_script_preserves_snapshots_and_guards_serving_mutations()
    {
        using var client = factory.CreateClient();

        var felogan = await client.GetStringAsync("/felogan.js");

        Assert.Contains("itemsById.set(item.id, item)", felogan);
        Assert.Contains("if (submit.disabled)", felogan);
        Assert.Contains("submit.disabled = true", felogan);
        Assert.Contains("finally", felogan);
        Assert.Contains("submit.disabled = false", felogan);
        Assert.Contains(
            "await api(`/v1/felogan/servings/${id}`, { method: \"DELETE\" });\n"
            + "    refreshHistory().catch((error) => showHistoryError(error.message));",
            felogan);
    }
}
