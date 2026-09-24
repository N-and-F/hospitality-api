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
        Assert.Contains("/app.css?v=charge-gcash", html);
        Assert.Contains("/app.js?v=paid-gcash", html);
        Assert.Contains("/felogan.js?v=paid-gcash", html);
        Assert.Contains("id=\"wallet-switch\"", html);
        Assert.Contains("id=\"wallet-switch-menu\"", html);
        Assert.Contains("id=\"wallet-label\"", html);
        Assert.Contains("data-settings-page=\"wallets\"", html);
        Assert.Contains("id=\"account-wallet\"", html);
        Assert.Contains("id=\"cashback\"", html);
        Assert.Contains("id=\"cashback-field\"", html);
        Assert.Contains("id=\"amount-hint\"", html);
        Assert.Contains("(Do not include Profit in here)", html);
        Assert.Contains("id=\"convenience\"", html);
        Assert.Contains("id=\"convenience-field\"", html);
        Assert.Contains("id=\"paid-gcash\"", html);
        Assert.Contains("id=\"paid-gcash-field\"", html);
        Assert.Contains("id=\"gcash-amount\"", html);
        Assert.Contains("id=\"gcash-amount-field\"", html);
        Assert.Contains("Paid in GCash", html);
        Assert.Contains("id=\"app-toast\"", html);
        Assert.Contains("id=\"app-toast-copy\"", html);
        Assert.DoesNotContain("id=\"felogan-toast\"", html);
        Assert.Contains("M7.4 12.4 10.5 15.4 16.6 8.8", html);
        Assert.DoesNotContain("id=\"felogan-additional-profit\"", html);
        Assert.Contains("data-nav=\"settings\"", html);
        Assert.Contains("M12.22 2h-.44a2 2 0 0 0-2 2v.18", html);
        Assert.DoesNotContain("M12 3.5v2.2M12 18.3v2.2", html);
        Assert.DoesNotContain("id=\"hospitality-settings\"", html);
        Assert.DoesNotContain("id=\"felogan-settings-host\"", html);
        Assert.Contains("data-felogan-nav=\"catalog\">Settings</button>", html);
        Assert.Contains("data-felogan-settings-menu", html);
        Assert.Contains("data-felogan-settings-back", html);
        Assert.Contains("class=\"menu-item\" data-felogan-catalog=\"menu-items\"", html);
        Assert.Contains("class=\"menu-item\" data-felogan-catalog=\"rooms\"", html);
        Assert.Contains("class=\"menu-item\" data-felogan-catalog=\"cooks\"", html);
        Assert.True(
            html.IndexOf("data-felogan-catalog=\"menu-items\"", StringComparison.Ordinal)
            < html.IndexOf("data-felogan-catalog=\"rooms\"", StringComparison.Ordinal));
        Assert.True(
            html.IndexOf("data-felogan-catalog=\"rooms\"", StringComparison.Ordinal)
            < html.IndexOf("data-felogan-catalog=\"cooks\"", StringComparison.Ordinal));
        Assert.Contains("data-catalog-section=\"rooms\" hidden", html);
        Assert.Contains("data-catalog-section=\"cooks\" hidden", html);
        Assert.Contains("data-catalog-section=\"menu-items\" hidden", html);
        Assert.Contains("for=\"felogan-menu-name\">Name</label>", html);
        Assert.Contains("for=\"felogan-menu-kind\">Kind</label>", html);
        Assert.Contains("for=\"felogan-menu-sell\">Sell</label>", html);
        Assert.Contains("for=\"felogan-menu-cost\">Cost</label>", html);
        Assert.DoesNotContain("placeholder=\"Item name\"", html);
        Assert.DoesNotContain("placeholder=\"Sell price\"", html);
        Assert.Contains("id=\"felogan-stock\"", html);
        Assert.Contains("data-felogan-nav=\"stock\"", html);
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
        Assert.Contains("window.hospitalityToast = showToast", app);
        Assert.Contains("showToast(wasEdit ? \"Transaction updated.\" : \"Transaction saved.\")", app);
        Assert.Contains("showToast(\"Wallets saved.\")", app);
        Assert.Contains("showToast(\"Account created.\")", app);
        Assert.Contains("showToast(error.message, \"error\")", app);
        Assert.Contains("window.hospitalityToast(message, kind)", felogan);
        Assert.Contains("showToast(\"Purchase saved.\")", felogan);
        Assert.Contains("showToast(\"Cook share saved.\")", felogan);
        Assert.Contains("showToast(id ? `${labels[type]} updated.` : `${labels[type]} saved.`)", felogan);
        Assert.Contains("showToast(error.message, \"error\")", felogan);
        Assert.Contains("window.bootHospitality", app);
        Assert.Contains("hospitality.walletId", app);
        Assert.Contains("/v1/wallet-sets", app);
        Assert.Contains("ledgerQuery", app);
        Assert.Contains("method === \"load_via_tpc\"", app);
        Assert.Contains("method === \"tpc_balance_inquiry\"", app);
        Assert.Contains("\"staff_remittance\"].includes(method)", app);
        Assert.Contains("isInquiry || amountOnly", app);
        Assert.Contains("profit-gcash-field\").hidden = isInquiry || amountOnly", app);
        Assert.Contains("profitGcash.disabled = isCharge", app);
        Assert.Contains("lastTxnMethod === \"cash_out_with_charge\"", app);
        Assert.Contains("method === \"load_via_gcash\"", app);
        Assert.Contains("body.cashback", app);
        Assert.Contains("body.convenienceCharge", app);
        Assert.Contains("method === \"top_up_tpc\"", app);
        Assert.Contains("paid-gcash-field", app);
        Assert.Contains("gcash-amount-field", app);
        Assert.Contains("body.gcashAmount", app);
        Assert.Contains("row.gcashAmount", app);
        Assert.Contains("const deleteBtn = deleteButton(`data-delete=\"${row.id}\"`)", app);
        Assert.DoesNotContain("const deleteBtn = isAdmin()", app);
        Assert.Contains("<th>Username</th>", app);
        Assert.Contains("<th>Wallet</th>", app);
        Assert.Contains("<th>Role</th>", app);
        Assert.Contains("<th>Actions</th>", app);
        Assert.Contains("wallet-switch-menu", app);
        Assert.DoesNotContain("window.openHospitalitySettings", app);
        Assert.DoesNotContain("window.openHospitalitySettings();", felogan);
        Assert.DoesNotContain("if (token())", app);
        Assert.Contains("window.openFeloganApp", felogan);
        Assert.Contains("window.bootHospitality()", felogan);
        Assert.Contains("item.kind === \"plate\" || item.kind === \"extra\"", felogan);
        Assert.Contains("[\"plate\", \"Silog\"]", felogan);
        Assert.Contains("if (kind === \"plate\") return \"Silog\"", felogan);
        Assert.Contains("item.kind === \"stock\"", felogan);
        Assert.Contains("fields.contains(note)", felogan);
        Assert.Contains("plates.appendChild(note)", felogan);
        Assert.Contains("data-step", felogan);
        Assert.Contains("Cook ${cookPercent}%", felogan);
        Assert.Contains("let catalogType = \"\"", felogan);
        Assert.Contains("data-felogan-settings-back", felogan);
        Assert.Contains("showCatalogSection", felogan);
        Assert.Contains("class=\"heading-row purchase-history-heading\"", html);
        Assert.Contains("function extraItems", felogan);
        Assert.Contains("item.kind === \"extra\"", felogan);
        Assert.Contains("<th>Item</th><th>Remaining</th>", felogan);
        Assert.Contains("range=day", felogan);
        Assert.DoesNotContain("data-felogan-purchase-range", html);
        Assert.DoesNotContain("felogan-purchase-history-summary", html);
        Assert.DoesNotContain("felogan-purchase-history-summary", felogan);
        Assert.Contains("includeArchived=true", felogan);
        Assert.Contains("<th>Profit</th><th>Cook</th><th>Paid cook</th><th>Note</th><th>Edit</th><th>Delete</th>", felogan);
        Assert.Contains("data-paid-cook-edit=\"${serving.id}\"", felogan);
        Assert.Contains("aria-label=\"Edit paid cook\"", felogan);
        Assert.Contains("data-paid-cook=\"${serving.id}\"", felogan);
        Assert.Contains("[\"yes\", \"na\", \"no\"]", felogan);
        Assert.Contains("value === \"na\" ? \"N/A\"", felogan);
        Assert.Contains("function servingUpdateBody", felogan);
        Assert.Contains("paidCook: serving.paidCook || \"no\"", felogan);
        Assert.Contains("additionalProfit: serving.additionalProfit ?? 0", felogan);
        Assert.Contains("excessPaid: serving.excessPaid ?? 0", felogan);
        Assert.DoesNotContain("paidSham: serving.paidSham || \"na\"", felogan);
        Assert.Contains("closest(\"[data-paid-cook-edit]\")", felogan);
        Assert.Contains("function menuTableHtml", felogan);
        Assert.Contains("<th>Name</th><th>Kind</th><th>Sell</th><th>Cost</th><th>Profit</th><th>Cook ${cookPercent}%</th><th>Left</th>", felogan);
        Assert.Contains("id=\"felogan-cook-percent\"", html);
        Assert.Contains("/v1/felogan/settings", felogan);
        Assert.Contains(".join(\"<br>\")", felogan);
        Assert.Contains("${item.qty}× ${escapeHtml(item.name)}", felogan);
    }

    [Fact]
    public async Task Felogan_script_preserves_snapshots_and_guards_serving_mutations()
    {
        using var client = factory.CreateClient();

        var felogan = await client.GetStringAsync("/felogan.js");

        Assert.Contains("itemsById.set(item.id, { ...current, ...item })", felogan);
        Assert.Contains("if (submit.disabled)", felogan);
        Assert.Contains("submit.disabled = true", felogan);
        Assert.Contains("Serving saved.", felogan);
        Assert.Contains("Serving updated.", felogan);
        Assert.Contains("showToast(savedEdit ? \"Serving updated.\" : \"Serving saved.\")", felogan);
        Assert.Contains("finally", felogan);
        Assert.Contains("submit.disabled = false", felogan);
        Assert.Contains(
            "await api(`/v1/felogan/servings/${id}`, { method: \"DELETE\" });\n"
            + "    refreshHistory().catch((error) => showHistoryError(error.message));",
            felogan);
    }
}
