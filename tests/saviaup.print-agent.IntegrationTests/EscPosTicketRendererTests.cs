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

    [Fact]
    public void Render_TestPrint_IncludesPrinterOrganizationTimestampAndFooter()
    {
        var payload = new KitchenOrderPrintPayload(
            "TEST_PRINT", "PRUEBA", null, "Savia Up",
            new DateTimeOffset(2026, 9, 20, 10, 30, 0, TimeSpan.Zero), [], null, false,
            "Cocina Epson", "Restaurante Savia", "Prueba de impresión de Savia Up");

        var result = new EscPosTicketRenderer().Render(JsonSerializer.Serialize(payload), 80);

        Assert.True(result.IsSuccess);
        var text = Encoding.UTF8.GetString(result.Value!);
        Assert.Contains("PRUEBA DE IMPRESIÓN", text);
        Assert.Contains("Impresora: Cocina Epson", text);
        Assert.Contains("Organización: Restaurante Savia", text);
        Assert.Contains("Prueba realizada en: 2026-09-20 10:30", text);
        Assert.Contains("Prueba de impresión de Savia Up", text);
    }

    [Fact]
    public void Render_AppliesConfiguredScaleAndLimitsLongItemNamesToTwoLines()
    {
        var payload = new KitchenOrderPrintPayload(
            "KitchenOrder", "CMD-42", "7", "Ana", DateTimeOffset.UtcNow,
            [new KitchenOrderPrintItem(1, "Hamburguesa artesanal con queso tocineta cebolla caramelizada y salsa de la casa", [], null)],
            null,
            false,
            Template: new KitchenPrintTemplate(ItemFontScale: 2, MaxItemNameLines: 2));

        var result = new EscPosTicketRenderer().Render(JsonSerializer.Serialize(payload), 58);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.AsSpan().IndexOf(new byte[] { 0x1D, 0x21, 0x11 }) >= 0);
        var text = Encoding.UTF8.GetString(result.Value!);
        var itemLines = text.Split('\n').Where(line => line.Contains("Hamburguesa") || line.EndsWith("...", StringComparison.Ordinal)).ToArray();
        Assert.InRange(itemLines.Length, 1, 2);
    }
}
