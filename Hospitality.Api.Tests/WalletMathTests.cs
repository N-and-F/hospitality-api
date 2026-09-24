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

    [Fact]
    public void Load_via_gcash_convenience_deducts_gcash()
    {
        var deltas = WalletMath.Deltas(
            TransactionMethods.LoadViaGcash,
            100,
            5,
            profitInGcash: false,
            convenienceCharge: 2);
        Assert.Equal(105, deltas.CashOnHand);
        Assert.Equal(-102, deltas.Gcash);
        Assert.Equal(0, deltas.Tpc);
    }

    [Fact]
    public void Tpc_balance_inquiry_deducts_tpc()
    {
        var deltas = WalletMath.Deltas(
            TransactionMethods.TpcBalanceInquiry,
            0,
            99,
            profitInGcash: false,
            convenienceCharge: 15);
        Assert.Equal(0, deltas.CashOnHand);
        Assert.Equal(0, deltas.Gcash);
        Assert.Equal(-15, deltas.Tpc);
    }

    [Fact]
    public void Staff_remittance_deducts_cash()
    {
        var deltas = WalletMath.Deltas(TransactionMethods.StaffRemittance, 200, 99, profitInGcash: true);
        Assert.Equal(-200, deltas.CashOnHand);
        Assert.Equal(0, deltas.Gcash);
        Assert.Equal(0, deltas.Tpc);
    }

    [Fact]
    public void Top_up_tpc_paid_in_gcash_deducts_gcash()
    {
        var deltas = WalletMath.Deltas(
            TransactionMethods.TopUpTpc,
            80,
            0,
            profitInGcash: false,
            gcashAmount: 80);
        Assert.Equal(0, deltas.CashOnHand);
        Assert.Equal(-80, deltas.Gcash);
        Assert.Equal(80, deltas.Tpc);
    }
}
