using System.Text;
using System.Text.Json;
using SaviaUp.PrintAgent.Domain.Contracts;
using SaviaUp.PrintAgent.Domain.Ports;
using SaviaUp.PrintAgent.Shared.Results;

namespace SaviaUp.PrintAgent.Infrastructure.Printing;

public sealed class EscPosTicketRenderer : ITicketRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public Result<byte[]> Render(string payloadJson, int paperWidth)
    {
        KitchenOrderPrintPayload? payload;
        try { payload = JsonSerializer.Deserialize<KitchenOrderPrintPayload>(payloadJson, JsonOptions); }
        catch (JsonException) { return Result<byte[]>.Failure("INVALID_PAYLOAD", "The print payload is not valid JSON."); }
        if (payload is null) return Result<byte[]>.Failure("INVALID_PAYLOAD", "The print payload is empty.");

        var template = Normalize(payload.Template);
        var columns = paperWidth == 58 ? 32 : 48;
        var separator = new string('-', columns);
        using var output = new MemoryStream();
        output.Write([0x1B, 0x40]);
        var isTestPrint = string.Equals(payload.DocumentType, "TEST_PRINT", StringComparison.OrdinalIgnoreCase);

        SetAlignment(output, template.HeaderAlignment);
        SetScale(output, template.HeaderFontScale);
        SetBold(output, true);
        WriteLine(output, "SAVIA UP");
        if (payload.IsReprint) WriteLine(output, "*** REIMPRESIÓN ***");
        WriteLine(output, isTestPrint ? "PRUEBA DE IMPRESIÓN" : $"COMANDA {payload.OrderNumber}");
        SetBold(output, false);
        SetScale(output, 1);
        SetAlignment(output, "LEFT");
        WriteLine(output);

        if (isTestPrint)
        {
            SetScale(output, template.MetadataFontScale);
            AppendWrapped(output, $"Impresora: {payload.PrinterName ?? payload.OrderNumber}", EffectiveColumns(columns, template.MetadataFontScale));
            AppendWrapped(output, $"Organización: {payload.OrganizationName ?? "Savia Up"}", EffectiveColumns(columns, template.MetadataFontScale));
            if (template.ShowTimestamp)
                AppendWrapped(output, $"Prueba realizada en: {payload.CreatedAt:yyyy-MM-dd HH:mm}", EffectiveColumns(columns, template.MetadataFontScale));
            SetScale(output, 1);
            WriteLine(output, separator);
            SetAlignment(output, "CENTER");
            WriteLine(output, payload.FooterMessage ?? "Prueba de impresión de Savia Up");
        }
        else
        {
            SetScale(output, template.MetadataFontScale);
            var metadataColumns = EffectiveColumns(columns, template.MetadataFontScale);
            if (template.ShowTable && !string.IsNullOrWhiteSpace(payload.Table))
                AppendWrapped(output, $"Mesa: {payload.Table}", metadataColumns);
            if (template.ShowWaiter) AppendWrapped(output, $"Mesero: {payload.Waiter}", metadataColumns);
            if (template.ShowTimestamp) AppendWrapped(output, $"Hora: {payload.CreatedAt:yyyy-MM-dd HH:mm}", metadataColumns);
            SetScale(output, 1);
            WriteLine(output, separator);

            var itemColumns = EffectiveColumns(columns, template.ItemFontScale);
            foreach (var item in payload.Items)
            {
                SetScale(output, template.ItemFontScale);
                SetBold(output, true);
                var itemName = template.UppercaseItemNames ? item.Name.ToUpperInvariant() : item.Name;
                var line = $"{item.Quantity} x {itemName}";
                if (template.WrapLongItemNames)
                    AppendWrapped(output, line, itemColumns, template.MaxItemNameLines);
                else
                    WriteLine(output, Truncate(line, itemColumns));
                SetBold(output, false);
                SetScale(output, 1);
                foreach (var modifier in item.Modifiers)
                    AppendWrapped(output, $"  - {modifier}", columns);
                if (!string.IsNullOrWhiteSpace(item.Notes))
                {
                    SetScale(output, template.NotesFontScale);
                    AppendWrapped(output, $"  Nota: {item.Notes}", EffectiveColumns(columns, template.NotesFontScale));
                    SetScale(output, 1);
                }
                for (var lineIndex = 0; lineIndex < ItemSpacing(template.Layout); lineIndex++) WriteLine(output);
            }
            if (!string.IsNullOrWhiteSpace(payload.Notes))
            {
                WriteLine(output, separator);
                SetScale(output, template.NotesFontScale);
                SetBold(output, true);
                WriteLine(output, "OBSERVACIONES:");
                SetBold(output, false);
                AppendWrapped(output, payload.Notes.ToUpperInvariant(), EffectiveColumns(columns, template.NotesFontScale));
                SetScale(output, 1);
            }
        }

        SetAlignment(output, "LEFT");
        SetScale(output, 1);
        SetBold(output, false);
        WriteLine(output, separator);
        WriteLine(output);
        WriteLine(output);
        output.Write([0x1D, 0x56, 0x41, 0x03]);
        return Result<byte[]>.Success(output.ToArray());
    }

    private static KitchenPrintTemplate Normalize(KitchenPrintTemplate? value)
    {
        // Jobs created before printing templates existed did not enlarge the header.
        // Keep that visual behavior while new jobs carry their explicit template snapshot.
        var template = value ?? new KitchenPrintTemplate(HeaderFontScale: 1);
        return template with
        {
            HeaderFontScale = Math.Clamp(template.HeaderFontScale, 1, 2),
            MetadataFontScale = Math.Clamp(template.MetadataFontScale, 1, 2),
            ItemFontScale = Math.Clamp(template.ItemFontScale, 1, 2),
            NotesFontScale = Math.Clamp(template.NotesFontScale, 1, 2),
            HeaderAlignment = Choice(template.HeaderAlignment, "CENTER", "LEFT", "RIGHT"),
            Layout = Choice(template.Layout, "STANDARD", "COMPACT", "SPACIOUS"),
            MaxItemNameLines = Math.Clamp(template.MaxItemNameLines, 1, 3)
        };
    }

    private static int EffectiveColumns(int columns, int scale) => Math.Max(8, columns / Math.Clamp(scale, 1, 2));
    private static int ItemSpacing(string layout) => layout == "COMPACT" ? 0 : layout == "SPACIOUS" ? 2 : 1;

    private static void SetAlignment(Stream target, string alignment)
        => target.Write([0x1B, 0x61, alignment == "RIGHT" ? (byte)2 : alignment == "CENTER" ? (byte)1 : (byte)0]);

    private static void SetScale(Stream target, int scale)
        => target.Write([0x1D, 0x21, scale >= 2 ? (byte)0x11 : (byte)0x00]);

    private static void SetBold(Stream target, bool enabled)
        => target.Write([0x1B, 0x45, enabled ? (byte)1 : (byte)0]);

    private static void WriteLine(Stream target, string value = "")
    {
        var content = Encoding.UTF8.GetBytes(value + "\n");
        target.Write(content);
    }

    private static void AppendWrapped(Stream target, string value, int columns, int maxLines = int.MaxValue)
    {
        var remaining = value.Trim();
        var lines = 0;
        while (remaining.Length > columns && lines < maxLines - 1)
        {
            var split = remaining.LastIndexOf(' ', columns);
            if (split <= 0) split = columns;
            WriteLine(target, remaining[..split].TrimEnd());
            remaining = remaining[split..].TrimStart();
            lines++;
        }
        if (remaining.Length > 0 && lines < maxLines)
            WriteLine(target, remaining.Length > columns ? Truncate(remaining, columns) : remaining);
    }

    private static string Truncate(string value, int columns)
    {
        if (value.Length <= columns) return value;
        return columns <= 3 ? value[..columns] : value[..(columns - 3)].TrimEnd() + "...";
    }

    private static string Choice(string? value, string fallback, params string[] allowed)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        return normalized is not null && allowed.Contains(normalized, StringComparer.Ordinal)
            ? normalized
            : fallback;
    }
}
