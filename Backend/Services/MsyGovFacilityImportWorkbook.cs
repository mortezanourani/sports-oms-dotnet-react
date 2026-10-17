using ClosedXML.Excel;

namespace Backend.Services;

public sealed record MsyGovFacilityImportRow(
    int RowNumber,
    string? ProvinceName,
    string? CityName,
    string? FacilityType,
    string? Name,
    string? Address,
    string? SportType,
    string? OwnerName,
    string? OwnerContact,
    string? ContractNumber,
    string? ContractThirdPartyName,
    string? ContractThirdPartyContact,
    string? ContractStartDate,
    string? ContractEndDate);

public static class MsyGovFacilityImportWorkbook
{
    public const string ContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly string[] Headers =
    [
        "استان", "شهر", "نوع مکان", "نام مکان", "آدرس", "نوع ورزش", "نام مالک", "تماس مالک",
        "شماره قرارداد", "نام طرف قرارداد", "تماس طرف قرارداد", "تاریخ شروع قرارداد", "تاریخ پایان قرارداد"
    ];

    public static byte[] CreateTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("اماکن MSY و دولتی");
        for (var column = 0; column < Headers.Length; column++)
            worksheet.Cell(1, column + 1).Value = Headers[column];

        worksheet.Row(1).Style.Font.Bold = true;
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns(1, Headers.Length).AdjustToContents();

        var instructions = workbook.Worksheets.Add("راهنما");
        var rows = new[]
        {
            ("ستون", "مقدار معتبر"),
            ("استان و شهر", "نام استان و شهر باید با هم مطابقت داشته باشند."),
            ("نوع مکان", "فقط Msy یا Gov پذیرفته می‌شود."),
            ("نام مکان و آدرس", "برای ثبت مکان الزامی هستند."),
            ("نوع ورزش، نام مالک و تماس مالک", "اختیاری هستند."),
            ("شماره قرارداد", "اختیاری است."),
            ("تاریخ شروع قرارداد", "برای ثبت قرارداد الزامی است؛ تاریخ شمسی به‌شکل سال/ماه/روز وارد شود."),
            ("نام و تماس طرف قرارداد", "اختیاری هستند."),
            ("تاریخ پایان قرارداد", "اختیاری است و در صورت ورود باید برابر یا بعد از تاریخ شروع باشد."),
            ("اطلاعات قرارداد نامعتبر", "مکان معتبر ثبت می‌شود، اما قرارداد آن ردیف ثبت نخواهد شد.")
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

    public static IReadOnlyList<MsyGovFacilityImportRow> Read(Stream stream)
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
        var rows = new List<MsyGovFacilityImportRow>();
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

            rows.Add(new MsyGovFacilityImportRow(
                rowNumber,
                ReadCell("استان"),
                ReadCell("شهر"),
                ReadCell("نوع مکان"),
                ReadCell("نام مکان"),
                ReadCell("آدرس"),
                ReadCell("نوع ورزش"),
                ReadCell("نام مالک"),
                ReadCell("تماس مالک"),
                ReadCell("شماره قرارداد"),
                ReadCell("نام طرف قرارداد"),
                ReadCell("تماس طرف قرارداد"),
                ReadCell("تاریخ شروع قرارداد"),
                ReadCell("تاریخ پایان قرارداد")));
        }

        return rows;
    }
}
