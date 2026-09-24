using Hospitality.Api.Ledger;

namespace Hospitality.Api.Tests;

public sealed class WalletMathTests
{
    [Fact]
    public void Cash_in_adds_cash_and_subtracts_gcash()
    {
        var deltas = WalletMath.Deltas(TransactionMethods.CashIn, 500, 10, profitInGcash: false);
        Assert.Equal(510, deltas.CashOnHand);
        Assert.Equal(-500, deltas.Gcash);
        Assert.Equal(0, deltas.Tpc);
    }

    [Fact]
    public void Cash_in_fee_in_gcash_credits_gcash()
    {
        var deltas = WalletMath.Deltas(TransactionMethods.CashIn, 500, 10, profitInGcash: true);
        Assert.Equal(500, deltas.CashOnHand);
        Assert.Equal(-490, deltas.Gcash);
    }

    [Fact]
    public void Top_up_gcash_credits_gcash_only()
    {
        var deltas = WalletMath.Deltas(TransactionMethods.TopUpGcash, 1000, 99, profitInGcash: false);
        Assert.Equal(0, deltas.CashOnHand);
        Assert.Equal(1000, deltas.Gcash);
        Assert.Equal(0, deltas.Tpc);
    }

    [Fact]
    public void Top_up_tpc_credits_tpc_only()
    {
        var deltas = WalletMath.Deltas(TransactionMethods.TopUpTpc, 400, 99, profitInGcash: true);
        Assert.Equal(0, deltas.CashOnHand);
        Assert.Equal(0, deltas.Gcash);
        Assert.Equal(400, deltas.Tpc);
    }

    [Fact]
    public void Load_via_tpc_cashback_credits_tpc()
    {
        var deltas = WalletMath.Deltas(TransactionMethods.LoadViaTpc, 50, 5, profitInGcash: false, cashback: 1);
        Assert.Equal(55, deltas.CashOnHand);
        Assert.Equal(0, deltas.Gcash);
        Assert.Equal(-49, deltas.Tpc);
    }
}
