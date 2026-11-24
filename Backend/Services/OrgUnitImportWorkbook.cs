using ClosedXML.Excel;

namespace Backend.Services;

public sealed record OrgUnitImportRow(
    int RowNumber,
    string? Title,
    string? Type,
    string? Scope,
    string? ParentTitle);

public static class OrgUnitImportWorkbook
{
    public const string ContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly string[] Headers =
    [
        "عنوان واحد سازمانی",
        "نوع واحد سازمانی",
        "دامنه",
        "واحد سازمانی والد"
    ];

    public static byte[] CreateTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("واحدهای سازمانی");
        for (var column = 0; column < Headers.Length; column++)
            worksheet.Cell(1, column + 1).Value = Headers[column];

        worksheet.Row(1).Style.Font.Bold = true;
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns(1, Headers.Length).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static IReadOnlyList<OrgUnitImportRow> Read(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidDataException("فایل اکسل هیچ کاربرگی ندارد.");
        var columns = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var cell in worksheet.Row(1).CellsUsed())
        {
            var header = cell.GetString().Trim();
            if (!Headers.Contains(header, StringComparer.Ordinal))
                continue;
            if (!columns.TryAdd(header, cell.Address.ColumnNumber))
                throw new InvalidDataException($"ستون «{header}» بیش از یک بار تکرار شده است.");
        }

        var missingHeaders = Headers.Where(header => !columns.ContainsKey(header)).ToArray();
        if (missingHeaders.Length > 0)
            throw new InvalidDataException($"ستون‌های الزامی در فایل وجود ندارند: {string.Join("، ", missingHeaders)}.");

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        var rows = new List<OrgUnitImportRow>();
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (row.IsEmpty())
                continue;

            string? ReadCell(string header)
            {
                var value = row.Cell(columns[header]).GetString().Trim();
                return string.IsNullOrEmpty(value) ? null : value;
            }

            rows.Add(new OrgUnitImportRow(
                rowNumber,
                ReadCell(Headers[0]),
                ReadCell(Headers[1]),
                ReadCell(Headers[2]),
                ReadCell(Headers[3])));
        }

        return rows;
    }
}
