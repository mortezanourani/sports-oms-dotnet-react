using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace Backend.Services;

public sealed record PrivateFacilityImportRow(
    int RowNumber,
    string? ProvinceName,
    string? CityName,
    string? Name,
    string? Address,
    string? SportType,
    string? OwnerName,
    string? OwnerContact,
    string? LicenseNumber,
    string? LicenseHolderName,
    string? LicenseIssueDate,
    string? LicenseExpiryDate);

public static class PrivateFacilityImportWorkbook
{
    public const string ContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly string[] Headers =
    [
        "استان", "شهر", "نام مکان", "آدرس", "نوع ورزش", "نام مالک", "تماس مالک",
        "شماره مجوز", "نام دارنده مجوز", "تاریخ صدور مجوز", "تاریخ انقضای مجوز"
    ];

    public static byte[] CreateTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("اماکن خصوصی");
        for (var column = 0; column < Headers.Length; column++)
            worksheet.Cell(1, column + 1).Value = Headers[column];

        worksheet.Row(1).Style.Font.Bold = true;
        worksheet.Columns(10, 11).Style.NumberFormat.Format = "@";
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns(1, Headers.Length).AdjustToContents();

        var instructions = workbook.Worksheets.Add("راهنما");
        var rows = new[]
        {
            ("ستون", "مقدار معتبر"),
            ("استان و شهر", "نام استان و شهر باید با هم مطابقت داشته باشند."),
            ("نام مکان و آدرس", "برای ثبت مکان الزامی هستند."),
            ("شماره مجوز، نام دارنده و تاریخ صدور مجوز", "برای ثبت مجوز الزامی هستند؛ تاریخ به‌شکل شمسی سال/ماه/روز وارد شود."),
            ("تاریخ انقضای مجوز", "اختیاری است و در صورت ورود باید برابر یا بعد از تاریخ صدور باشد."),
            ("اطلاعات مجوز نامعتبر", "مکان معتبر ثبت می‌شود، اما مجوز آن ردیف ثبت نخواهد شد.")
        };
        for (var row = 0; row < rows.Length; row++)
        {
            instructions.Cell(row + 1, 1).Value = rows[row].Item1;
            instructions.Cell(row + 1, 2).Value = rows[row].Item2;
        }
        instructions.Row(1).Style.Font.Bold = true;
        instructions.Columns(1, 2).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static IReadOnlyList<PrivateFacilityImportRow> Read(Stream stream)
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
        var rows = new List<PrivateFacilityImportRow>();
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (row.IsEmpty())
                continue;

            string? ReadCell(string header)
            {
                var value = row.Cell(columns[header]).GetFormattedString().Trim();
                return string.IsNullOrEmpty(value) ? null : value;
            }

            rows.Add(new PrivateFacilityImportRow(
                rowNumber,
                ReadCell("استان"),
                ReadCell("شهر"),
                ReadCell("نام مکان"),
                ReadCell("آدرس"),
                ReadCell("نوع ورزش"),
                ReadCell("نام مالک"),
                ReadCell("تماس مالک"),
                ReadCell("شماره مجوز"),
                ReadCell("نام دارنده مجوز"),
                ReadCell("تاریخ صدور مجوز"),
                ReadCell("تاریخ انقضای مجوز")));
        }

        return rows;
    }

    public static string NormalizeName(string value)
    {
        var normalized = NormalizeDigits(value.Trim())
            .Replace('ي', 'ی')
            .Replace('ك', 'ک');
        return string.Join(' ', normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    public static bool TryParseSolarDate(string? value, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = NormalizeDigits(value.Trim()).Replace('-', '/');
        var parts = normalized.Split('/');
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var year) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var month) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var day))
            return false;

        try
        {
            date = DateOnly.FromDateTime(new PersianCalendar().ToDateTime(year, month, day, 0, 0, 0, 0));
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static string NormalizeDigits(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is >= '۰' and <= '۹')
                builder.Append((char)('0' + character - '۰'));
            else if (character is >= '٠' and <= '٩')
                builder.Append((char)('0' + character - '٠'));
            else
                builder.Append(character);
        }
        return builder.ToString();
    }
}
