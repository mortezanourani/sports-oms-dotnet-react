using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Backend.Models;

namespace Backend.Services;

public sealed record FederationImportRow(
    int RowNumber,
    string? FederationName,
    string? ProvinceName,
    string? CityName,
    string? Username,
    string? FirstName,
    string? LastName,
    string? Gender,
    string? PhoneNumber,
    string? Position,
    string? StartDate,
    string? EndDate);

public static class FederationImportWorkbook
{
    public const string ContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly string[] Headers =
    [
        "فدراسیون", "استان", "شهرستان", "کد ملی", "نام", "نام خانوادگی",
        "جنسیت", "شماره همراه", "سمت", "تاریخ انتصاب", "تاریخ اتمام"
    ];

    private static readonly IReadOnlyDictionary<string, FederationPositionType> Positions =
        new Dictionary<string, FederationPositionType>(StringComparer.Ordinal)
        {
            ["رئیس"] = FederationPositionType.President,
            ["نایب رئیس"] = FederationPositionType.Miss,
            ["دبیر"] = FederationPositionType.Vice
        };

    public static byte[] CreateTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("فدراسیون‌ها");
        for (var column = 0; column < Headers.Length; column++)
            worksheet.Cell(1, column + 1).Value = Headers[column];

        worksheet.Row(1).Style.Font.Bold = true;
        worksheet.Column(4).Style.NumberFormat.Format = "@";
        worksheet.Column(8).Style.NumberFormat.Format = "@";
        worksheet.Column(10).Style.NumberFormat.Format = "@";
        worksheet.Column(11).Style.NumberFormat.Format = "@";
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns(1, Headers.Length).AdjustToContents();

        var instructions = workbook.Worksheets.Add("راهنما");
        var rows = new[]
        {
            ("ستون", "توضیحات"),
            ("فدراسیون", "نام فدراسیون کشوری؛ برای ردیف‌های استانی و شهری باید از قبل ثبت شده باشد."),
            ("استان / شهرستان", "خالی بودن هر دو یعنی فدراسیون کشوری؛ استان بدون شهرستان یعنی استانی؛ هر دو یعنی شهری."),
            ("سمت", "رئیس، نایب رئیس، دبیر"),
            ("جنسیت", "مرد یا زن؛ اختیاری"),
            ("تاریخ انتصاب / تاریخ اتمام", "تاریخ شمسی به شکل سال/ماه/روز، مانند ۱۴۰۳/۰۱/۱۵؛ تاریخ اتمام اختیاری است."),
            ("حساب جدید", "اگر کد ملی در سامانه موجود نباشد، حساب نماینده با رمز پیش‌فرض P@ssw0rd ساخته می‌شود."),
            ("ردیف نامعتبر نماینده", "اطلاعات معتبر فدراسیون ثبت می‌شود، اما انتصاب نماینده با داده‌های ناقص یا نامعتبر رد می‌شود.")
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

    public static IReadOnlyList<FederationImportRow> Read(Stream stream)
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
        var rows = new List<FederationImportRow>();
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (row.IsEmpty())
                continue;

            string? ReadCell(string header)
            {
                var cell = row.Cell(columns[header]);
                var value = cell.GetFormattedString().Trim();
                return string.IsNullOrEmpty(value) ? null : value;
            }

            rows.Add(new FederationImportRow(
                rowNumber,
                ReadCell("فدراسیون"),
                ReadCell("استان"),
                ReadCell("شهرستان"),
                ReadCell("کد ملی"),
                ReadCell("نام"),
                ReadCell("نام خانوادگی"),
                ReadCell("جنسیت"),
                ReadCell("شماره همراه"),
                ReadCell("سمت"),
                ReadCell("تاریخ انتصاب"),
                ReadCell("تاریخ اتمام")));
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

    public static bool TryParsePosition(string? value, out FederationPositionType position)
    {
        position = default;
        return value != null && Positions.TryGetValue(NormalizeName(value), out position);
    }

    public static bool TryParseGender(string? value, out Gender gender)
    {
        gender = default;
        var normalized = value == null ? null : NormalizeName(value);
        if (normalized == "مرد")
        {
            gender = Gender.Male;
            return true;
        }
        if (normalized == "زن")
        {
            gender = Gender.Female;
            return true;
        }
        return false;
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
            var gregorian = new PersianCalendar().ToDateTime(year, month, day, 0, 0, 0, 0);
            date = DateOnly.FromDateTime(gregorian);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    public static bool TryParsePhoneNumber(string? value, out string phoneNumber)
    {
        phoneNumber = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = NormalizeDigits(value.Trim());
        var digits = new string(normalized.Where(char.IsDigit).ToArray());
        if (digits.Length is < 7 or > 15 ||
            normalized.Any(character => !char.IsDigit(character) &&
                                        character is not ('+' or ' ' or '-' or '(' or ')')) ||
            normalized.Count(character => character == '+') > 1 ||
            normalized.Contains('+') && !normalized.StartsWith('+'))
            return false;

        phoneNumber = normalized;
        return true;
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
