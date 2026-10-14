using Backend.Models;
using Backend.Services;
using ClosedXML.Excel;
using Xunit;

namespace Backend.Tests;

public class ChampionImportWorkbookTests
{
    [Fact]
    public void TemplateContainsExpectedHeaders()
    {
        using var stream = new MemoryStream(ChampionImportWorkbook.CreateTemplate());
        using var workbook = new XLWorkbook(stream);

        var headers = workbook.Worksheet("قهرمانان و مدال‌ها").Row(1).CellsUsed()
            .Select(cell => cell.GetString())
            .ToArray();

        Assert.Equal(
            ["نام کامل", "جنسیت", "تاریخ تولد", "فدراسیون", "استان", "شهرستان",
                "مدال", "رده سنی", "سطح مسابقات", "سال", "تعداد", "عنوان مسابقات", "رشته ورزشی"],
            headers);
    }

    [Fact]
    public void ReadParsesRowsAndSkipsBlankRows()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("قهرمانان و مدال‌ها");
            var headers = new[]
            {
                "نام کامل", "جنسیت", "تاریخ تولد", "فدراسیون", "استان", "شهرستان",
                "مدال", "رده سنی", "سطح مسابقات", "سال", "تعداد", "عنوان مسابقات", "رشته ورزشی"
            };
            for (var column = 0; column < headers.Length; column++)
                worksheet.Cell(1, column + 1).Value = headers[column];

            worksheet.Cell(3, 1).Value = "قهرمان نمونه";
            worksheet.Cell(3, 2).Value = "زن";
            worksheet.Cell(3, 3).Value = "۱۴۰۳/۰۱/۱۵";
            worksheet.Cell(3, 4).Value = "فدراسیون نمونه";
            worksheet.Cell(3, 7).Value = "طلای تیمی";
            worksheet.Cell(3, 8).Value = "زیر ۱۸ سال";
            worksheet.Cell(3, 9).Value = "بین‌المللی";
            worksheet.Cell(3, 10).Value = "۱۴۰۲";
            worksheet.Cell(3, 11).Value = "۲";
            worksheet.Cell(3, 12).Value = "مسابقات نمونه";
            worksheet.Cell(3, 13).Value = "شنا";
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var row = Assert.Single(ChampionImportWorkbook.Read(stream));

        Assert.Equal(3, row.RowNumber);
        Assert.Equal("قهرمان نمونه", row.FullName);
        Assert.Equal("طلای تیمی", row.MedalType);
        Assert.Equal("۱۴۰۲", row.Year);
        Assert.Equal("شنا", row.Field);
    }

    [Fact]
    public void ParsesPersianLabelsAndSolarDatesButRejectsLocalLevel()
    {
        Assert.True(ChampionImportWorkbook.TryParseGender("مرد", out var gender));
        Assert.Equal(Gender.Male, gender);

        Assert.True(ChampionImportWorkbook.TryParseMedalType("طلای تیمی", out var medalType));
        Assert.Equal(MedalType.TeamGold, medalType);
        Assert.True(ChampionImportWorkbook.TryParseAgeGroup("زیر ۱۸ سال", out var ageGroup));
        Assert.Equal(AgeGroup.Under18, ageGroup);
        Assert.True(ChampionImportWorkbook.TryParseTournamentLevel("بین‌المللی", out var level));
        Assert.Equal(TournamentLevel.International, level);
        Assert.False(ChampionImportWorkbook.TryParseTournamentLevel("محلی", out _));

        Assert.True(ChampionImportWorkbook.TryParseSolarDate("۱۴۰۳/۰۱/۱۵", out var date));
        Assert.Equal(new DateOnly(2024, 4, 3), date);
        Assert.False(ChampionImportWorkbook.TryParseSolarDate("۱۴۰۲/۱۲/۳۰", out _));
        Assert.False(ChampionImportWorkbook.TryParseInteger("نامعتبر", out _));
        Assert.True(ChampionImportWorkbook.TryParseInteger("۱۲۳۴", out var number));
        Assert.Equal(1234, number);
    }
}
