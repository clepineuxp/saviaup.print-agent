using System.Text;
using System.Text.Json;
using SaviaUp.PrintAgent.Domain.Contracts;
using SaviaUp.PrintAgent.Infrastructure.Printing;

namespace SaviaUp.PrintAgent.IntegrationTests;

public sealed class EscPosTicketRendererTests
{
    [Theory]
    [InlineData(58)]
    [InlineData(80)]
    public void Render_CreatesEscPosTicketForBothPaperWidths(int width)
    {
        var payload = new KitchenOrderPrintPayload(
            "KitchenOrder", "CMD-000542", "12", "Juan",
            new DateTimeOffset(2026, 9, 19, 19, 35, 0, TimeSpan.Zero),
            [new KitchenOrderPrintItem(2, "Hamburguesa Especial", ["Sin cebolla", "Carne 3/4"], null)],
            "Alergia al maní", true);

        var result = new EscPosTicketRenderer().Render(JsonSerializer.Serialize(payload), width);

        Assert.True(result.IsSuccess);
        var text = Encoding.UTF8.GetString(result.Value!);
        Assert.Contains("SAVIA UP", text);
        Assert.Contains("*** REIMPRESIÓN ***", text);
        Assert.Contains("CMD-000542", text);
        Assert.Contains("Sin cebolla", text);
        Assert.Equal(0x1B, result.Value![0]);
        Assert.Equal(0x1D, result.Value[^4]);
    }
}
