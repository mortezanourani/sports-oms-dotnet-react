using ClosedXML.Excel;
using Backend.Models;

namespace Backend.Services;

public sealed record UserImportRow(
    int RowNumber,
    string? Username,
    string? FirstName,
    string? LastName,
    string? Scope,
    string? Role,
    string? ProvinceName,
    string? CityName,
    string? FatherName,
    string? Gender,
    string? PhoneNumber);

public static class UserImportWorkbook
{
    public const string ContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly (string Key, string Header)[] Columns =
    [
        ("username", "کد ملی"),
        ("firstname", "نام"),
        ("lastname", "نام خانوادگی"),
        ("fathername", "نام پدر"),
        ("gender", "جنسیت"),
        ("phonenumber", "شماره همراه"),
        ("scope", "سطح دسترسی"),
        ("role", "سمت"),
        ("provincename", "استان"),
        ("cityname", "شهرستان")
    ];

    private static readonly IReadOnlyDictionary<string, string> LegacyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["username"] = "username",
            ["firstname"] = "firstname",
            ["lastname"] = "lastname",
            ["fathername"] = "fathername",
            ["gender"] = "gender",
            ["phonenumber"] = "phonenumber",
            ["scope"] = "scope",
            ["role"] = "role",
            ["provincename"] = "provincename",
            ["cityname"] = "cityname"
        };

    private static readonly IReadOnlyDictionary<string, string> RoleLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(UserRole.Admin)] = "مدیر سامانه",
            [nameof(UserRole.Manager)] = "مدیر",
            [nameof(UserRole.HumanResourceManager)] = "مسئول امور اداری",
            [nameof(UserRole.HumanResourceOfficer)] = "کارشناس امور اداری",
            [nameof(UserRole.SportsManager)] = "معاون ورزشی",
            [nameof(UserRole.SportsOfficer)] = "کارشناس ورزش",
            [nameof(UserRole.M88Manager)] = "مسئول ماده 88",
            [nameof(UserRole.M88Officer)] = "کارشناس ماده 88",
            [nameof(UserRole.M5Manager)] = "مسئول امور باشگاه‌ها",
            [nameof(UserRole.M5Officer)] = "کارشناس امور باشگاه‌ها",
            [nameof(UserRole.InsuranceOfficer)] = "کارشناس بیمه ورزشی"
        };

    public static byte[] CreateTemplate()
    {
        using var workbook = new XLWorkbook();
        var users = workbook.Worksheets.Add("Users");

        for (var column = 0; column < Columns.Length; column++)
            users.Cell(1, column + 1).Value = Columns[column].Header;

        users.Row(1).Style.Font.Bold = true;
        users.Column(1).Style.NumberFormat.Format = "@";
        users.Column(6).Style.NumberFormat.Format = "@";
        users.SheetView.FreezeRows(1);
        users.Columns(1, Columns.Length).AdjustToContents();

        var instructions = workbook.Worksheets.Add("Instructions");
        var rows = new[]
        {
            ("ستون", "مقادیر مجاز"),
            ("سطح دسترسی", "وزارت، اداره کل، شهرستان"),
            ("سمت", string.Join("، ", RoleLabels.Values)),
            ("جنسیت", "مرد یا زن؛ اختیاری"),
            ("استان", "نام استان ثبت‌شده؛ برای سطح دسترسی اداره کل و شهرستان الزامی است"),
            ("شهرستان", "نام شهر ثبت‌شده؛ برای سطح دسترسی شهرستان الزامی است"),
            ("رمز عبور", "رمز پیش‌فرض حساب‌ها P@ssw0rd است؛ این فایل نباید ستون رمز عبور داشته باشد."),
            ("رفتار ورود اطلاعات", "ردیف‌های بدون کد ملی، نام، نام خانوادگی، سطح دسترسی یا سمت و نام‌های کاربری موجود نادیده گرفته می‌شوند.")
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

    public static IReadOnlyList<UserImportRow> Read(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidDataException("The workbook does not contain a worksheet.");
        var headerColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var cell in worksheet.Row(1).CellsUsed())
        {
            var header = cell.GetString().Trim();
            if (string.Equals(header, "password", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(header, "رمز عبور", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("ستون رمز عبور نباید در فایل اکسل وجود داشته باشد.");

            var columnKey = ResolveColumnKey(header);
            if (columnKey != null && !headerColumns.TryAdd(columnKey, cell.Address.ColumnNumber))
                throw new InvalidDataException($"The header '{header}' appears more than once.");
        }

        var missingHeaders = Columns
            .Where(column => !headerColumns.ContainsKey(column.Key))
            .Select(column => column.Header)
            .ToArray();
        if (missingHeaders.Length > 0)
            throw new InvalidDataException($"ستون‌های الزامی در فایل وجود ندارند: {string.Join("، ", missingHeaders)}.");

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        var rows = new List<UserImportRow>();

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

            rows.Add(new UserImportRow(
                rowNumber,
                ReadCell("username"),
                ReadCell("firstname"),
                ReadCell("lastname"),
                ReadCell("scope"),
                ReadCell("role"),
                ReadCell("provincename"),
                ReadCell("cityname"),
                ReadCell("fathername"),
                ReadCell("gender"),
                ReadCell("phonenumber")));
        }

        return rows;
    }

    public static bool TryParseScope(string? value, out ScopeLevel scope)
    {
        scope = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        if (normalized is "وزارت")
        {
            scope = ScopeLevel.Mastery;
            return true;
        }
        if (normalized is "اداره کل")
        {
            scope = ScopeLevel.Province;
            return true;
        }
        if (normalized is "شهرستان")
        {
            scope = ScopeLevel.City;
            return true;
        }

        return Enum.TryParse(normalized, true, out scope) &&
               Enum.IsDefined(scope) &&
               Enum.GetNames<ScopeLevel>().Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    public static bool TryParseRole(string? value, out UserRole role)
    {
        role = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        var roleName = RoleLabels.FirstOrDefault(pair =>
            string.Equals(pair.Value, normalized, StringComparison.OrdinalIgnoreCase)).Key;
        if (roleName != null)
            normalized = roleName;

        return Enum.TryParse(normalized, true, out role) &&
               Enum.IsDefined(role) &&
               Enum.GetNames<UserRole>().Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    public static bool TryParseGender(string? value, out Gender gender)
    {
        gender = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
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

        return Enum.TryParse(normalized, true, out gender) &&
               Enum.IsDefined(gender) &&
               Enum.GetNames<Gender>().Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    private static string? ResolveColumnKey(string header)
    {
        if (string.IsNullOrEmpty(header))
            return null;

        var legacyColumn = LegacyHeaders.FirstOrDefault(pair =>
            string.Equals(pair.Key, header, StringComparison.OrdinalIgnoreCase));
        if (legacyColumn.Key != null)
            return legacyColumn.Value;

        return Columns.FirstOrDefault(column => column.Header == header).Key;
    }
}
