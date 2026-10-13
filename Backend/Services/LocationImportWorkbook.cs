using ClosedXML.Excel;

namespace Backend.Services;

public sealed record LocationImportRow(int RowNumber, string? ProvinceName, string? CityName);

public static class LocationImportWorkbook
{
    public const string ContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly string[] Headers = ["استان", "شهرستان"];

    public static byte[] CreateTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Locations");
        worksheet.Cell(1, 1).Value = Headers[0];
        worksheet.Cell(1, 2).Value = Headers[1];
        worksheet.Row(1).Style.Font.Bold = true;
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns(1, 2).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static IReadOnlyList<LocationImportRow> Read(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidDataException("فایل اکسل هیچ کاربرگی ندارد.");
        var columns = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var cell in worksheet.Row(1).CellsUsed())
        {
            var header = cell.GetString().Trim();
            if (header is not ("استان" or "شهرستان"))
                continue;

            if (!columns.TryAdd(header, cell.Address.ColumnNumber))
                throw new InvalidDataException($"ستون «{header}» بیش از یک بار تکرار شده است.");
        }

        var missingHeaders = Headers.Where(header => !columns.ContainsKey(header)).ToArray();
        if (missingHeaders.Length > 0)
            throw new InvalidDataException($"ستون‌های الزامی در فایل وجود ندارند: {string.Join("، ", missingHeaders)}.");

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        var rows = new List<LocationImportRow>();

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

            rows.Add(new LocationImportRow(rowNumber, ReadCell("استان"), ReadCell("شهرستان")));
        }

        return rows;
    }

    public static string NormalizeName(string name) =>
        name.Trim().Replace('ي', 'ی').Replace('ك', 'ک');
}
