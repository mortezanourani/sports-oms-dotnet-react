using Backend.Services;
using ClosedXML.Excel;
using Xunit;

namespace Backend.Tests;

public class UserImportWorkbookTests
{
    [Fact]
    public void TemplateContainsImportFieldsWithoutPasswordColumn()
    {
        using var stream = new MemoryStream(UserImportWorkbook.CreateTemplate());
        using var workbook = new XLWorkbook(stream);

        var headers = workbook.Worksheet("Users").Row(1).CellsUsed()
            .Select(cell => cell.GetString())
            .ToArray();

        Assert.Equal(
            ["کد ملی", "نام", "نام خانوادگی", "نام پدر", "جنسیت",
                "شماره همراه", "سطح دسترسی", "سمت", "استان", "شهرستان"],
            headers);
        Assert.DoesNotContain("رمز عبور", headers);
    }

    [Fact]
    public void ReadReturnsRowsAndSkipsBlankRows()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Users");
            var headers = new[]
            {
                "username", "firstname", "lastname", "scope", "role",
                    "provincename", "cityname", "fathername", "gender", "phonenumber"
            };

            for (var column = 0; column < headers.Length; column++)
                worksheet.Cell(1, column + 1).Value = headers[column];

            worksheet.Cell(3, 1).Value = " user-1 ";
            worksheet.Cell(3, 2).Value = "First";
            worksheet.Cell(3, 3).Value = "Last";
            worksheet.Cell(3, 4).Value = "Mastery";
            worksheet.Cell(3, 5).Value = "Admin";
            worksheet.Cell(3, 8).Value = "Father";
            worksheet.Cell(3, 9).Value = "Male";
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var rows = UserImportWorkbook.Read(stream);

        var row = Assert.Single(rows);
        Assert.Equal(3, row.RowNumber);
        Assert.Equal("user-1", row.Username);
        Assert.Equal("First", row.FirstName);
        Assert.Equal("Last", row.LastName);
        Assert.Equal("Mastery", row.Scope);
        Assert.Equal("Admin", row.Role);
        Assert.Null(row.ProvinceName);
        Assert.Null(row.CityName);
        Assert.Equal("Father", row.FatherName);
        Assert.Equal("Male", row.Gender);
        Assert.Null(row.PhoneNumber);
    }

    [Fact]
    public void ReadParsesPersianTemplateHeadersAndPhoneNumber()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Users");
            var headers = new[]
            {
                "کد ملی", "نام", "نام خانوادگی", "نام پدر", "جنسیت",
                "شماره همراه", "سطح دسترسی", "سمت", "استان", "شهرستان"
            };

            for (var column = 0; column < headers.Length; column++)
                worksheet.Cell(1, column + 1).Value = headers[column];

            worksheet.Cell(2, 1).Value = "1234567891";
            worksheet.Cell(2, 2).Value = "رضا";
            worksheet.Cell(2, 3).Value = "پروانه";
            worksheet.Cell(2, 4).Value = "پدر";
            worksheet.Cell(2, 5).Value = "مرد";
            worksheet.Cell(2, 6).Value = "09120000000";
            worksheet.Cell(2, 7).Value = "اداره کل";
            worksheet.Cell(2, 8).Value = "مدیر سامانه";
            worksheet.Cell(2, 9).Value = "گیلان";
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var row = Assert.Single(UserImportWorkbook.Read(stream));

        Assert.Equal("1234567891", row.Username);
        Assert.Equal("پروانه", row.LastName);
        Assert.Equal("09120000000", row.PhoneNumber);
        Assert.True(UserImportWorkbook.TryParseScope(row.Scope, out var scope));
        Assert.Equal(Backend.Models.ScopeLevel.Province, scope);
        Assert.True(UserImportWorkbook.TryParseRole(row.Role, out var role));
        Assert.Equal(Backend.Models.UserRole.Admin, role);
        Assert.True(UserImportWorkbook.TryParseGender(row.Gender, out var gender));
        Assert.Equal(Backend.Models.Gender.Male, gender);
    }

    [Fact]
    public void ParsesPersianAndEnumValuesForAllScopeAndGenderChoices()
    {
        Assert.True(UserImportWorkbook.TryParseScope("وزارت", out var mastery));
        Assert.Equal(Backend.Models.ScopeLevel.Mastery, mastery);
        Assert.True(UserImportWorkbook.TryParseScope("شهرستان", out var city));
        Assert.Equal(Backend.Models.ScopeLevel.City, city);
        Assert.True(UserImportWorkbook.TryParseScope("Province", out var province));
        Assert.Equal(Backend.Models.ScopeLevel.Province, province);

        Assert.True(UserImportWorkbook.TryParseGender("زن", out var female));
        Assert.Equal(Backend.Models.Gender.Female, female);
        Assert.True(UserImportWorkbook.TryParseGender("Female", out female));
        Assert.Equal(Backend.Models.Gender.Female, female);

        Assert.False(UserImportWorkbook.TryParseRole("نماینده فدراسیون/هیات", out _));
    }

    [Fact]
    public void ImportRoleAuthorizationFollowsUploaderRoleRules()
    {
        Assert.False(UserImportAuthorization.CanAssignRole(
            Backend.Models.UserRole.Manager, Backend.Models.UserRole.Admin));
        Assert.False(UserImportAuthorization.CanAssignRole(
            Backend.Models.UserRole.HumanResourceManager, Backend.Models.UserRole.Manager));
        Assert.False(UserImportAuthorization.CanAssignRole(
            Backend.Models.UserRole.HumanResourceOfficer, Backend.Models.UserRole.Admin));
        Assert.True(UserImportAuthorization.CanAssignRole(
            Backend.Models.UserRole.HumanResourceOfficer, Backend.Models.UserRole.HumanResourceManager));
        Assert.True(UserImportAuthorization.CanAssignRole(
            Backend.Models.UserRole.Admin, Backend.Models.UserRole.M5Officer));
        Assert.True(UserImportAuthorization.CanAssignRole(
            Backend.Models.UserRole.Manager, Backend.Models.UserRole.HumanResourceManager));
        Assert.True(UserImportAuthorization.CanAssignRole(
            Backend.Models.UserRole.HumanResourceManager, Backend.Models.UserRole.M5Officer));
        Assert.False(UserImportAuthorization.CanAssignRole(
            Backend.Models.UserRole.SportsManager, Backend.Models.UserRole.M5Officer));
        Assert.False(UserImportAuthorization.CanAssignRole(
            Backend.Models.UserRole.Admin, Backend.Models.UserRole.FederationRep));
    }

    [Fact]
    public void ReadRejectsPasswordColumn()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Users");
            var headers = new[]
            {
                "username", "firstname", "lastname", "scope", "role",
                "provincename", "cityname", "fathername", "gender", "password"
            };

            for (var column = 0; column < headers.Length; column++)
                worksheet.Cell(1, column + 1).Value = headers[column];

            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var error = Assert.Throws<InvalidDataException>(() => UserImportWorkbook.Read(stream));

        Assert.Contains("رمز عبور", error.Message);
    }
}
