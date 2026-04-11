using Xunit;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Entities.Finance.Exceptions;
using RESQ.Domain.Entities.Exceptions;
using System;

namespace RESQ.Tests.Domain.Finance;

public class DepotFundModelTests
{
    private DepotFundModel CreateFund(decimal balance = 0, decimal advanceLimit = 0, decimal outstandingAdvance = 0)
    {
        return DepotFundModel.Reconstitute(
            id: 1,
            depotId: 10,
            balance: balance,
            advanceLimit: advanceLimit,
            outstandingAdvanceAmount: outstandingAdvance,
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
    public void Advance_WithinLimit_IncreasesBalanceAndOutstanding()
    {
        // Arrange
        var fund = CreateFund(balance: 0, advanceLimit: 5000, outstandingAdvance: 1000);

        // Act
        fund.Advance(2000);

        // Assert
        Assert.Equal(2000, fund.Balance); // 0 + 2000
        Assert.Equal(3000, fund.OutstandingAdvanceAmount); // 1000 + 2000
    }

    [Fact]
    public void Advance_ExceedsLimit_ThrowsAdvanceLimitExceededException()
    {
        // Arrange
        var fund = CreateFund(balance: 0, advanceLimit: 5000, outstandingAdvance: 4000);

        // Act
        Action act = () => fund.Advance(1500);

        // Assert
        Assert.Throws<AdvanceLimitExceededException>(act);
    }

    [Fact]
    public void Repay_ValidAmount_DecreasesBalanceAndOutstanding()
    {
        // Arrange
        var fund = CreateFund(balance: 3000, advanceLimit: 5000, outstandingAdvance: 2000);

        // Act
        fund.Repay(1000);

        // Assert
        Assert.Equal(2000, fund.Balance);
        Assert.Equal(1000, fund.OutstandingAdvanceAmount);
    }

    [Fact]
    public void Repay_ExceedsOutstanding_ThrowsOverRepaymentException()
    {
        // Arrange
        var fund = CreateFund(balance: 3000, advanceLimit: 5000, outstandingAdvance: 2000);

        // Act
        Action act = () => fund.Repay(2500);

        // Assert
        Assert.Throws<OverRepaymentException>(act);
    }

    [Fact]
    public void Repay_ExceedsBalance_ThrowsInsufficientDepotFundException()
    {
        // Arrange
        // Outstanding is 3000, but only 2000 in balance (maybe some money was spent on imports)
        var fund = CreateFund(balance: 2000, advanceLimit: 5000, outstandingAdvance: 3000);

        // Act
        Action act = () => fund.Repay(2500);

        // Assert
        Assert.Throws<InsufficientDepotFundException>(act);
    }

    [Fact]
    public void SetAdvanceLimit_ValidLimit_UpdatesLimit()
    {
        // Arrange
        var fund = CreateFund(balance: 0, advanceLimit: 5000, outstandingAdvance: 2000);

        // Act
        fund.SetAdvanceLimit(3000);

        // Assert
        Assert.Equal(3000, fund.AdvanceLimit);
    }

    [Fact]
    public void SetAdvanceLimit_LowerThanOutstanding_ThrowsInvalidAdvanceLimitException()
    {
        // Arrange
        var fund = CreateFund(balance: 0, advanceLimit: 5000, outstandingAdvance: 3000);

        // Act
        Action act = () => fund.SetAdvanceLimit(2000);

        // Assert
        Assert.Throws<InvalidAdvanceLimitException>(act);
    }

    [Fact]
    public void SetAdvanceLimit_NegativeAmount_ThrowsNegativeMoneyException()
    {
        // Arrange
        var fund = CreateFund();

        // Act
        Action act = () => fund.SetAdvanceLimit(-100);

        // Assert
        Assert.Throws<NegativeMoneyException>(act);
    }
}
