namespace Hospitality.Api.Ledger;

public static class TransactionMethods
{
    public const string CashIn = "cash_in";
    public const string CashOut = "cash_out";
    public const string CashOutWithCharge = "cash_out_with_charge";
    public const string LoadViaGcash = "load_via_gcash";
    public const string LoadViaTpc = "load_via_tpc";
    public const string TopUpGcash = "top_up_gcash";
    public const string TopUpTpc = "top_up_tpc";
    public const string AddCashCapital = "add_cash_capital";
    public const string Expenses = "expenses";
    public const string BankCashDeposit = "bank_cash_deposit";

    public static readonly IReadOnlyList<string> All =
    [
        CashIn, CashOut, CashOutWithCharge, LoadViaGcash, LoadViaTpc,
        TopUpGcash, TopUpTpc, AddCashCapital, Expenses, BankCashDeposit
    ];

    public static readonly IReadOnlySet<string> ForcesZeroProfit = new HashSet<string>(StringComparer.Ordinal)
    {
        TopUpGcash, TopUpTpc, AddCashCapital, Expenses, BankCashDeposit
    };

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [CashIn] = "Cash-in",
        [CashOut] = "Cash-out",
        [CashOutWithCharge] = "Cash-out w/ charge",
        [LoadViaGcash] = "Load via GCash",
        [LoadViaTpc] = "Load via TPC",
        [TopUpGcash] = "Top-up GCash",
        [TopUpTpc] = "Top-up TPC",
        [AddCashCapital] = "Add cash capital",
        [Expenses] = "Expenses",
        [BankCashDeposit] = "Bank cash deposit"
    };

    public static bool IsKnown(string? method) =>
        method is not null && All.Contains(method);
}
