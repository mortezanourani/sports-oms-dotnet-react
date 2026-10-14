using ClosedXML.Excel;

namespace Backend.Services;

public sealed record AthleteCountImportRow(
    int RowNumber,
    string? FederationName,
    string? ProvinceName,
    string? CityName,
    string? Year,
    string? Month,
    string? MenCount,
    string? WomenCount);

public static class AthleteCountImportWorkbook
{
    public const string ContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly string[] Headers =
    [
        "فدراسیون", "استان", "شهرستان", "سال", "ماه", "آقایان", "بانوان"
    ];

    private static readonly string[] PersianMonths =
    [
        "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
        "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
    ];

    public static byte[] CreateTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("آمار ورزشکاران");

        for (var column = 0; column < Headers.Length; column++)
            worksheet.Cell(1, column + 1).Value = Headers[column];

        worksheet.Row(1).Style.Font.Bold = true;
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns(1, Headers.Length).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static IReadOnlyList<AthleteCountImportRow> Read(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidDataException("فایل اکسل هیچ کاربرگی ندارد.");
        var headerColumns = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var cell in worksheet.Row(1).CellsUsed())
        {
            var header = cell.GetString().Trim();
            if (!Headers.Contains(header, StringComparer.Ordinal))
                continue;

            if (!headerColumns.TryAdd(header, cell.Address.ColumnNumber))
                throw new InvalidDataException($"ستون «{header}» بیش از یک بار تکرار شده است.");
        }

        var missingHeaders = Headers.Where(header => !headerColumns.ContainsKey(header)).ToArray();
        if (missingHeaders.Length > 0)
            throw new InvalidDataException($"ستون‌های الزامی در فایل وجود ندارند: {string.Join("، ", missingHeaders)}.");

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        var rows = new List<AthleteCountImportRow>();
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (row.IsEmpty())
                continue;

            string? ReadCell(string header)
            {
                var value = row.Cell(headerColumns[header]).GetString().Trim();
                return string.IsNullOrEmpty(value) ? null : value;
            }

            rows.Add(new AthleteCountImportRow(
                rowNumber,
                ReadCell("فدراسیون"),
                ReadCell("استان"),
                ReadCell("شهرستان"),
                ReadCell("سال"),
                ReadCell("ماه"),
                ReadCell("آقایان"),
                ReadCell("بانوان")));
        }

        return rows;
    }

    public static string NormalizeName(string value) =>
        NormalizeDigits(value.Trim()).Replace('ي', 'ی').Replace('ك', 'ک');

    public static bool TryParseInteger(string? value, out int result) =>
        int.TryParse(NormalizeDigits(value?.Trim() ?? string.Empty), out result);

    public static bool TryParseMonth(string? value, out int month)
    {
        month = 0;
        if (TryParseInteger(value, out var numericMonth) && numericMonth is >= 1 and <= 12)
        {
            month = numericMonth;
            return true;
        }

        var monthIndex = Array.FindIndex(PersianMonths, name => name == value?.Trim());
        if (monthIndex < 0)
            return false;

        month = monthIndex + 1;
        return true;
    }

    private static string NormalizeDigits(string value)
    {
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is >= '۰' and <= '۹')
                chars[i] = (char)('0' + chars[i] - '۰');
            else if (chars[i] is >= '٠' and <= '٩')
                chars[i] = (char)('0' + chars[i] - '٠');
        }

        return new string(chars);
    }
}
