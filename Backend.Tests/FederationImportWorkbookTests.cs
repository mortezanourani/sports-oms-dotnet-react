using Backend.Models;
using Backend.Services;
using ClosedXML.Excel;
using Xunit;

namespace Backend.Tests;

public class FederationImportWorkbookTests
{
    [Fact]
    public void TemplateContainsExpectedHeaders()
    {
        using var stream = new MemoryStream(FederationImportWorkbook.CreateTemplate());
        using var workbook = new XLWorkbook(stream);

        var headers = workbook.Worksheet("فدراسیون‌ها").Row(1).CellsUsed()
            .Select(cell => cell.GetString())
            .ToArray();

        Assert.Equal(
            ["فدراسیون", "استان", "شهرستان", "کد ملی", "نام", "نام خانوادگی",
                "جنسیت", "شماره همراه", "سمت", "تاریخ انتصاب", "تاریخ اتمام"],
            headers);
    }

    [Fact]
    public void ReadParsesAllColumnsAndSkipsBlankRows()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("فدراسیون‌ها");
            var headers = new[]
            {
                "فدراسیون", "استان", "شهرستان", "کد ملی", "نام", "نام خانوادگی",
                "جنسیت", "شماره همراه", "سمت", "تاریخ انتصاب", "تاریخ اتمام"
            };
            for (var column = 0; column < headers.Length; column++)
                worksheet.Cell(1, column + 1).Value = headers[column];

            worksheet.Cell(3, 1).Value = "فدراسیون نمونه";
            worksheet.Cell(3, 2).Value = "استان نمونه";
            worksheet.Cell(3, 3).Value = "شهر نمونه";
            worksheet.Cell(3, 4).Value = "001234";
            worksheet.Cell(3, 5).Value = "نام";
            worksheet.Cell(3, 6).Value = "نام خانوادگی";
            worksheet.Cell(3, 7).Value = "مرد";
            worksheet.Cell(3, 8).Value = "09120000000";
            worksheet.Cell(3, 9).Value = "رئیس";
            worksheet.Cell(3, 10).Value = "۱۴۰۳/۰۱/۱۵";
            worksheet.Cell(3, 11).Value = "۱۴۰۳/۰۶/۱۵";
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var row = Assert.Single(FederationImportWorkbook.Read(stream));

        Assert.Equal(3, row.RowNumber);
        Assert.Equal("فدراسیون نمونه", row.FederationName);
        Assert.Equal("001234", row.Username);
        Assert.Equal("رئیس", row.Position);
        Assert.Equal("۱۴۰۳/۰۱/۱۵", row.StartDate);
        Assert.Equal("۱۴۰۳/۰۶/۱۵", row.EndDate);
    }

    [Fact]
    public void ParsesPersianPositionGenderDatesAndPhoneNumbers()
    {
        Assert.True(FederationImportWorkbook.TryParsePosition("نایب رئیس", out var position));
        Assert.Equal(FederationPositionType.Miss, position);
        Assert.True(FederationImportWorkbook.TryParsePosition("دبیر", out position));
        Assert.Equal(FederationPositionType.Vice, position);

        Assert.True(FederationImportWorkbook.TryParseGender("زن", out var gender));
        Assert.Equal(Gender.Female, gender);

        Assert.True(FederationImportWorkbook.TryParseSolarDate("۱۴۰۳/۰۱/۱۵", out var date));
        Assert.Equal(new DateOnly(2024, 4, 3), date);
        Assert.False(FederationImportWorkbook.TryParseSolarDate("۱۴۰۲/۱۲/۳۰", out _));

        Assert.True(FederationImportWorkbook.TryParsePhoneNumber("+۹۸ ۹۱۲-۰۰۰-۰۰۰۰", out var phone));
        Assert.Equal("+98 912-000-0000", phone);
        Assert.False(FederationImportWorkbook.TryParsePhoneNumber("phone", out _));
    }
}
