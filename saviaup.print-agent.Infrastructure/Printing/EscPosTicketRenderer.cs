using System.Text;
using System.Text.Json;
using SaviaUp.PrintAgent.Domain.Contracts;
using SaviaUp.PrintAgent.Domain.Ports;
using SaviaUp.PrintAgent.Shared.Results;

namespace SaviaUp.PrintAgent.Infrastructure.Printing;

public sealed class EscPosTicketRenderer : ITicketRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public Result<byte[]> Render(string payloadJson, int paperWidth)
    {
        KitchenOrderPrintPayload? payload;
        try { payload = JsonSerializer.Deserialize<KitchenOrderPrintPayload>(payloadJson, JsonOptions); }
        catch (JsonException) { return Result<byte[]>.Failure("INVALID_PAYLOAD", "The print payload is not valid JSON."); }
        if (payload is null) return Result<byte[]>.Failure("INVALID_PAYLOAD", "The print payload is empty.");

        var columns = paperWidth == 58 ? 32 : 48;
        var separator = new string('-', columns);
        var text = new StringBuilder();
        text.AppendLine(Center("SAVIA UP", columns));
        if (payload.IsReprint) text.AppendLine(Center("*** REIMPRESIÓN ***", columns));
        text.AppendLine(Center(payload.DocumentType == "TestPage" ? "PRUEBA DE IMPRESIÓN" : $"COMANDA {payload.OrderNumber}", columns));
        text.AppendLine();
        if (!string.IsNullOrWhiteSpace(payload.Table)) text.AppendLine($"Mesa: {payload.Table}");
        text.AppendLine($"Mesero: {payload.Waiter}");
        text.AppendLine($"Hora UTC: {payload.CreatedAt:yyyy-MM-dd HH:mm}");
        text.AppendLine(separator);
        foreach (var item in payload.Items)
        {
            AppendWrapped(text, $"{item.Quantity} x {item.Name}", columns);
            foreach (var modifier in item.Modifiers) AppendWrapped(text, $"  - {modifier}", columns);
            if (!string.IsNullOrWhiteSpace(item.Notes)) AppendWrapped(text, $"  Nota: {item.Notes}", columns);
            text.AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(payload.Notes))
        {
            text.AppendLine(separator);
            text.AppendLine("OBSERVACIONES:");
            AppendWrapped(text, payload.Notes.ToUpperInvariant(), columns);
        }
        text.AppendLine(separator);
        text.AppendLine();
        text.AppendLine();

        var content = Encoding.UTF8.GetBytes(text.ToString());
        var result = new byte[content.Length + 6];
        result[0] = 0x1B; result[1] = 0x40;
        Buffer.BlockCopy(content, 0, result, 2, content.Length);
        result[^4] = 0x1D; result[^3] = 0x56; result[^2] = 0x41; result[^1] = 0x03;
        return Result<byte[]>.Success(result);
    }

    private static string Center(string value, int columns)
    {
        if (value.Length >= columns) return value;
        return new string(' ', (columns - value.Length) / 2) + value;
    }

    private static void AppendWrapped(StringBuilder target, string value, int columns)
    {
        var remaining = value.Trim();
        while (remaining.Length > columns)
        {
            var split = remaining.LastIndexOf(' ', columns);
            if (split <= 0) split = columns;
            target.AppendLine(remaining[..split].TrimEnd());
            remaining = remaining[split..].TrimStart();
        }
        if (remaining.Length > 0) target.AppendLine(remaining);
    }
}
