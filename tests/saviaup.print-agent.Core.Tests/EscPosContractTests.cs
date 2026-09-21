using SaviaUp.PrintAgent.Domain.Contracts;

namespace SaviaUp.PrintAgent.Core.Tests;

public sealed class EscPosContractTests
{
    [Fact]
    public void ReprintContract_PreservesExplicitAuditFlag()
    {
        var payload = new KitchenOrderPrintPayload(
            "KitchenOrder", "CMD-000001", "12", "Ana", DateTimeOffset.UtcNow,
            [new KitchenOrderPrintItem(1, "Hamburguesa", [], null)], null, true);

        Assert.True(payload.IsReprint);
        Assert.Single(payload.Items);
    }
}
