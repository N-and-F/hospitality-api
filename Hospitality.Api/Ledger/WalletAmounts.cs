namespace Hospitality.Api.Ledger;

public readonly record struct WalletAmounts(decimal CashOnHand, decimal Gcash, decimal Tpc)
{
    public static WalletAmounts Zero => new(0, 0, 0);

    public WalletAmounts Add(WalletAmounts other) =>
        new(CashOnHand + other.CashOnHand, Gcash + other.Gcash, Tpc + other.Tpc);
}
