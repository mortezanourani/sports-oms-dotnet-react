using System.Globalization;
using System.Text;
using Backend.Models;
using ClosedXML.Excel;

namespace Backend.Services;

public sealed record ChampionImportRow(
    int RowNumber,
    string? FullName,
    string? Gender,
    string? BirthDate,
    string? FederationName,
    string? ProvinceName,
    string? CityName,
    string? MedalType,
    string? AgeGroup,
    string? TournamentLevel,
    string? Year,
    string? Count,
    string? CompetitionName,
    string? Field);

public static class ChampionImportWorkbook
{
    public const string ContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly string[] Headers =
    [
        "نام کامل", "جنسیت", "تاریخ تولد", "فدراسیون", "استان", "شهرستان",
        "مدال", "رده سنی", "سطح مسابقات", "سال", "تعداد", "عنوان مسابقات", "رشته ورزشی"
    ];

    private static readonly IReadOnlyDictionary<string, MedalType> MedalTypes =
        new Dictionary<string, MedalType>(StringComparer.Ordinal)
        {
            ["طلا"] = MedalType.Gold,
            ["نقره"] = MedalType.Silver,
            ["برنز"] = MedalType.Bronze,
            ["طلای تیمی"] = MedalType.TeamGold,
            ["نقره تیمی"] = MedalType.TeamSilver,
            ["برنز تیمی"] = MedalType.TeamBronze
        };

    private static readonly IReadOnlyDictionary<string, AgeGroup> AgeGroups =
        new Dictionary<string, AgeGroup>(StringComparer.Ordinal)
        {
            ["زیر 14 سال"] = AgeGroup.Under14,
            ["زیر 18 سال"] = AgeGroup.Under18,
            ["زیر 21 سال"] = AgeGroup.Under21,
            ["بزرگسالان"] = AgeGroup.Adult,
            ["پیشکسوتان"] = AgeGroup.Senior
        };

    private static readonly IReadOnlyDictionary<string, TournamentLevel> TournamentLevels =
        new Dictionary<string, TournamentLevel>(StringComparer.Ordinal)
        {
            ["استانی"] = TournamentLevel.Provincial,
            ["کشوری"] = TournamentLevel.National,
            ["بین‌المللی"] = TournamentLevel.International,
            ["بین المللی"] = TournamentLevel.International
        };

    public static byte[] CreateTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("قهرمانان و مدال‌ها");
        for (var column = 0; column < Headers.Length; column++)
            worksheet.Cell(1, column + 1).Value = Headers[column];

        worksheet.Row(1).Style.Font.Bold = true;
        worksheet.Column(3).Style.NumberFormat.Format = "@";
        worksheet.Column(10).Style.NumberFormat.Format = "@";
        worksheet.Column(11).Style.NumberFormat.Format = "@";
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns(1, Headers.Length).AdjustToContents();

        var instructions = workbook.Worksheets.Add("راهنما");
        var rows = new[]
        {
            ("ستون", "مقدار معتبر"),
            ("جنسیت", "مرد یا زن"),
            ("تاریخ تولد", "تاریخ شمسی سال/ماه/روز؛ اختیاری"),
            ("استان و شهرستان", "خالی بودن هر دو یعنی سطح کشوری؛ شهرستان بدون استان یا استان بدون شهرستان پذیرفته نمی‌شود."),
            ("مدال", "طلا، نقره، برنز، طلای تیمی، نقره تیمی یا برنز تیمی"),
            ("رده سنی", "زیر ۱۴ سال، زیر ۱۸ سال، زیر ۲۱ سال، بزرگسالان یا پیشکسوتان"),
            ("سطح مسابقات", "استانی، کشوری یا بین‌المللی"),
            ("سال / تعداد / عنوان مسابقات", "برای ثبت مدال الزامی هستند؛ تعداد باید حداقل ۱ باشد."),
            ("رشته ورزشی", "اختیاری")
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

    public static IReadOnlyList<ChampionImportRow> Read(Stream stream)
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
        var rows = new List<ChampionImportRow>();
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

            rows.Add(new ChampionImportRow(
                rowNumber,
                ReadCell("نام کامل"),
                ReadCell("جنسیت"),
                ReadCell("تاریخ تولد"),
                ReadCell("فدراسیون"),
                ReadCell("استان"),
                ReadCell("شهرستان"),
                ReadCell("مدال"),
                ReadCell("رده سنی"),
                ReadCell("سطح مسابقات"),
                ReadCell("سال"),
                ReadCell("تعداد"),
                ReadCell("عنوان مسابقات"),
                ReadCell("رشته ورزشی")));
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

    public static bool TryParseGender(string? value, out Gender gender)
    {
        gender = default;
        switch (value == null ? null : NormalizeName(value))
        {
            case "مرد":
                gender = Gender.Male;
                return true;
            case "زن":
                gender = Gender.Female;
                return true;
            default:
                return false;
        }
    }

    public static bool TryParseMedalType(string? value, out MedalType medalType) =>
        TryParseValue(value, MedalTypes, out medalType);

    public static bool TryParseAgeGroup(string? value, out AgeGroup ageGroup) =>
        TryParseValue(value, AgeGroups, out ageGroup);

    public static bool TryParseTournamentLevel(string? value, out TournamentLevel level) =>
        TryParseValue(value, TournamentLevels, out level);

    public static bool TryParseInteger(string? value, out int number) =>
        int.TryParse(value == null ? null : NormalizeDigits(value.Trim()),
            NumberStyles.Integer, CultureInfo.InvariantCulture, out number);

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

    private static bool TryParseValue<T>(
        string? value,
        IReadOnlyDictionary<string, T> values,
        out T parsed)
    {
        parsed = default!;
        return value != null && values.TryGetValue(NormalizeName(value), out parsed!);
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
