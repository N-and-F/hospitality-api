using Hospitality.Api.Ledger;

namespace Hospitality.Api.Tests;

public sealed class ProfitSuggestionTests
{
    [Theory]
    [InlineData(50, 5)]
    [InlineData(99.99, 5)]
    [InlineData(100, 10)]
    public void Load_bands(decimal amount, decimal expected)
    {
        Assert.Equal(expected, ProfitSuggestion.For(TransactionMethods.LoadViaTpc, amount));
    }

    [Fact]
    public void Cash_in_two_percent_rounded_to_ten()
    {
        Assert.Equal(10, ProfitSuggestion.For(TransactionMethods.CashIn, 500));
        Assert.Equal(340, ProfitSuggestion.For(TransactionMethods.CashIn, 17000));
    }
}
