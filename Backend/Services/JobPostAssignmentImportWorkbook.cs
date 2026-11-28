using System.Globalization;
using ClosedXML.Excel;

namespace Backend.Services;

public sealed record JobPostAssignmentImportRow(
    int RowNumber,
    string? Username,
    string? JobPostUniqueId,
    string? StartDate,
    string? EndDate);

public static class JobPostAssignmentImportWorkbook
{
    public const string ContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly string[] Headers =
    [
        "نام کاربری",
        "شناسه یکتا پست",
        "تاریخ شروع",
        "تاریخ پایان"
    ];

    public static byte[] CreateTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("تخصیص پست");
        for (var column = 0; column < Headers.Length; column++)
            worksheet.Cell(1, column + 1).Value = Headers[column];

        worksheet.Row(1).Style.Font.Bold = true;
        worksheet.Column(1).Style.NumberFormat.Format = "@";
        worksheet.Column(2).Style.NumberFormat.Format = "@";
        worksheet.Column(3).Style.NumberFormat.Format = "@";
        worksheet.Column(4).Style.NumberFormat.Format = "@";
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns(1, Headers.Length).AdjustToContents();

        var instructions = workbook.Worksheets.Add("راهنما");
        instructions.Cell(1, 1).Value = "ستون";
        instructions.Cell(1, 2).Value = "توضیحات";
        instructions.Cell(2, 1).Value = "نام کاربری";
        instructions.Cell(2, 2).Value = "نام کاربری حساب موجود در سامانه.";
        instructions.Cell(3, 1).Value = "شناسه یکتا پست";
        instructions.Cell(3, 2).Value = "شناسه یکتای پست شغلی موجود در سامانه.";
        instructions.Cell(4, 1).Value = "تاریخ شروع / تاریخ پایان";
        instructions.Cell(4, 2).Value = "اختیاری؛ تاریخ شمسی به شکل سال/ماه/روز. خالی بودن شروع یعنی تاریخ نامشخص و خالی بودن پایان یعنی تخصیص جاری.";
        instructions.Row(1).Style.Font.Bold = true;
        instructions.Columns(1, 2).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static IReadOnlyList<JobPostAssignmentImportRow> Read(Stream stream)
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

        var rows = new List<JobPostAssignmentImportRow>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (row.IsEmpty())
                continue;

            string? ReadCell(string header)
            {
                var cell = row.Cell(columns[header]);
                if (cell.IsEmpty())
                    return null;
                var value = cell.DataType == XLDataType.DateTime
                    ? cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : cell.GetFormattedString().Trim();
                return string.IsNullOrEmpty(value) ? null : value;
            }

            rows.Add(new JobPostAssignmentImportRow(
                rowNumber,
                ReadCell(Headers[0]),
                ReadCell(Headers[1]),
                ReadCell(Headers[2]),
                ReadCell(Headers[3])));
        }

        return rows;
    }

    public static bool TryParseDate(string? value, out DateOnly? date)
    {
        date = null;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var normalized = value.Trim();
        if (DateOnly.TryParseExact(
                normalized,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var gregorianDate))
        {
            date = gregorianDate;
            return true;
        }

        if (FederationImportWorkbook.TryParseSolarDate(normalized, out var solarDate))
        {
            date = solarDate;
            return true;
        }

        return false;
    }
}
