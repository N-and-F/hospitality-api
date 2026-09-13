namespace Hospitality.Api.Ledger;

public static class WalletMath
{
    public static WalletAmounts Deltas(string method, decimal amount, decimal profit, bool profitInGcash)
    {
        if (TransactionMethods.ForcesZeroProfit.Contains(method))
        {
            profit = 0;
        }

        var feeCash = profit > 0 && !profitInGcash ? profit : 0;
        var feeGcash = profit > 0 && profitInGcash ? profit : 0;

        return method switch
        {
            TransactionMethods.CashIn => new(amount + feeCash, -amount + feeGcash, 0),
            TransactionMethods.CashOut => new(-amount + feeCash, amount + feeGcash, 0),
            TransactionMethods.CashOutWithCharge => new(-amount + feeCash, amount + feeGcash, 0),
            TransactionMethods.LoadViaGcash => new(amount + feeCash, -amount + feeGcash, 0),
            TransactionMethods.LoadViaTpc => new(amount + feeCash, feeGcash, -amount),
            TransactionMethods.TopUpGcash => new(-amount, amount, 0),
            TransactionMethods.TopUpTpc => new(-amount, 0, amount),
            TransactionMethods.AddCashCapital => new(amount, 0, 0),
            TransactionMethods.Expenses => new(-amount, 0, 0),
            TransactionMethods.BankCashDeposit => new(-amount, 0, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown method.")
        };
    }

    public static bool MakesCashOrGcashNegative(WalletAmounts current, WalletAmounts delta)
    {
        var next = current.Add(delta);
        return next.CashOnHand < 0 || next.Gcash < 0;
    }
}
