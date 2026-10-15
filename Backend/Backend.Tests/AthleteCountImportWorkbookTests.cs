using Backend.Services;
using ClosedXML.Excel;
using Xunit;

namespace Backend.Tests;

public class AthleteCountImportWorkbookTests
{
    [Fact]
    public void TemplateContainsAllRequestedColumnsInOrder()
    {
        using var stream = new MemoryStream(AthleteCountImportWorkbook.CreateTemplate());
        using var workbook = new XLWorkbook(stream);

        var headers = workbook.Worksheet("آمار ورزشکاران").Row(1).CellsUsed()
            .Select(cell => cell.GetString())
            .ToArray();

        Assert.Equal(
            ["فدراسیون", "استان", "شهرستان", "سال", "ماه", "آقایان", "بانوان"],
            headers);
    }

    [Fact]
    public void ReadParsesRowsAndSkipsEmptyRows()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("آمار ورزشکاران");
            var headers = new[]
            {
                "فدراسیون", "استان", "شهرستان", "سال", "ماه", "آقایان", "بانوان"
            };

            for (var column = 0; column < headers.Length; column++)
                worksheet.Cell(1, column + 1).Value = headers[column];

            worksheet.Cell(3, 1).Value = "فدراسیون نمونه";
            worksheet.Cell(3, 2).Value = "استان نمونه";
            worksheet.Cell(3, 3).Value = "شهر نمونه";
            worksheet.Cell(3, 4).Value = 1403;
            worksheet.Cell(3, 5).Value = 1;
            worksheet.Cell(3, 6).Value = 120;
            worksheet.Cell(3, 7).Value = 80;
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var row = Assert.Single(AthleteCountImportWorkbook.Read(stream));

        Assert.Equal(3, row.RowNumber);
        Assert.Equal("فدراسیون نمونه", row.FederationName);
        Assert.Equal("استان نمونه", row.ProvinceName);
        Assert.Equal("شهر نمونه", row.CityName);
        Assert.Equal("1403", row.Year);
        Assert.Equal("1", row.Month);
        Assert.Equal("120", row.MenCount);
        Assert.Equal("80", row.WomenCount);
    }

    [Fact]
    public void ParsersSupportPersianAndArabicDigitsAndPersianMonthNames()
    {
        Assert.True(AthleteCountImportWorkbook.TryParseInteger("۱۴۰۳", out var year));
        Assert.Equal(1403, year);
        Assert.True(AthleteCountImportWorkbook.TryParseInteger("١٢٣", out var count));
        Assert.Equal(123, count);
        Assert.True(AthleteCountImportWorkbook.TryParseMonth("فروردین", out var month));
        Assert.Equal(1, month);
        Assert.True(AthleteCountImportWorkbook.TryParseMonth("۱۲", out month));
        Assert.Equal(12, month);
        Assert.False(AthleteCountImportWorkbook.TryParseMonth("اسفند ماه", out _));
    }
}
