using ClosedXML.Excel;

namespace Backend.Services;

public sealed record JobPostTypeImportRow(int RowNumber, string? Title);

public static class JobPostTypeImportWorkbook
{
    public const string ContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const string TitleHeader = "عنوان نوع پست";

    public static byte[] CreateTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("انواع پست");
        worksheet.Cell(1, 1).Value = TitleHeader;
        worksheet.Row(1).Style.Font.Bold = true;
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns(1, 1).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static IReadOnlyList<JobPostTypeImportRow> Read(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidDataException("فایل اکسل هیچ کاربرگی ندارد.");
        var titleColumns = worksheet.Row(1).CellsUsed()
            .Where(cell => string.Equals(cell.GetString().Trim(), TitleHeader, StringComparison.Ordinal))
            .Select(cell => cell.Address.ColumnNumber)
            .ToArray();

        if (titleColumns.Length == 0)
            throw new InvalidDataException($"ستون الزامی «{TitleHeader}» در فایل وجود ندارد.");
        if (titleColumns.Length > 1)
            throw new InvalidDataException($"ستون «{TitleHeader}» بیش از یک بار تکرار شده است.");

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        var rows = new List<JobPostTypeImportRow>();
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (row.IsEmpty())
                continue;

            var title = row.Cell(titleColumns[0]).GetString().Trim();
            rows.Add(new JobPostTypeImportRow(
                rowNumber,
                string.IsNullOrEmpty(title) ? null : title));
        }

        return rows;
    }
}
