using Xunit;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Entities.Finance.Exceptions;
using RESQ.Domain.Entities.Exceptions;
using System;

namespace RESQ.Tests.Domain.Finance;

public class DepotFundModelTests
{
    private DepotFundModel CreateFund(decimal balance = 0)
    {
        return DepotFundModel.Reconstitute(
            id: 1,
            depotId: 10,
            balance: balance,
            lastUpdatedAt: DateTime.UtcNow
        );
    }

    [Fact]
    public void Credit_AmountGreaterThanZero_IncreasesBalance()
    {
        // Arrange
        var fund = CreateFund(balance: 1000);

        // Act
        fund.Credit(500);

        // Assert
        Assert.Equal(1500, fund.Balance);
    }

    [Fact]
    public void Credit_AmountLessThanOrEqualZero_ThrowsNegativeMoneyException()
    {
        var fund = CreateFund();
        Action actZero = () => fund.Credit(0);
        Action actNegative = () => fund.Credit(-10);

        Assert.Throws<NegativeMoneyException>(actZero);
        Assert.Throws<NegativeMoneyException>(actNegative);
    }

    [Fact]
    public void Debit_SufficientFund_DecreasesBalance()
    {
        // Arrange
        var fund = CreateFund(balance: 1000);

        // Act
        fund.Debit(500);

        // Assert
        Assert.Equal(500, fund.Balance);
    }

    [Fact]
    public void Debit_InsufficientFund_ThrowsInsufficientDepotFundException()
    {
        // Arrange
        var fund = CreateFund(balance: 1000);

        // Act
        Action act = () => fund.Debit(1500);

        // Assert
        Assert.Throws<InsufficientDepotFundException>(act);
    }

    [Fact]
    public void Advance_AmountGreaterThanZero_IncreasesBalance()
    {
        // Arrange
        var fund = CreateFund(balance: 0);

        // Act
        fund.Advance(2000);

        // Assert
        Assert.Equal(2000, fund.Balance);
    }

    [Fact]
    public void Advance_AmountLessThanOrEqualZero_ThrowsNegativeMoneyException()
    {
        var fund = CreateFund(balance: 0);
        Action actZero = () => fund.Advance(0);
        Action actNegative = () => fund.Advance(-10);

        Assert.Throws<NegativeMoneyException>(actZero);
        Assert.Throws<NegativeMoneyException>(actNegative);
    }

    [Fact]
    public void Repay_AmountGreaterThanZero_DecreasesBalance()
    {
        // Arrange
        var fund = CreateFund(balance: 3000);

        // Act
        fund.Repay(1000);

        // Assert
        Assert.Equal(2000, fund.Balance);
    }

    [Fact]
    public void Repay_ExceedsBalance_ThrowsInsufficientDepotFundException()
    {
        // Arrange
        var fund = CreateFund(balance: 2000);

        // Act
        Action act = () => fund.Repay(2500);

        // Assert
        Assert.Throws<InsufficientDepotFundException>(act);
    }

    [Fact]
    public void Repay_AmountLessThanOrEqualZero_ThrowsNegativeMoneyException()
    {
        var fund = CreateFund(balance: 1000);
        Action actZero = () => fund.Repay(0);
        Action actNegative = () => fund.Repay(-10);

        Assert.Throws<NegativeMoneyException>(actZero);
        Assert.Throws<NegativeMoneyException>(actNegative);
    }
}
