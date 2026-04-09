using System;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;
using Xunit;

public class FundTransactionModelTests
{
    [Fact]
    public void CanCreateFundTransactionModel_WithValidData()
    {
        var model = new FundTransactionModel();
        Assert.NotNull(model);
    }
    // Add more tests for each method/property if logic exists
}
