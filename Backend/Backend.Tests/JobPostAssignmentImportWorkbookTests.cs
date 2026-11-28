using Backend.Services;
using ClosedXML.Excel;
using Xunit;

namespace Backend.Tests;

public class JobPostAssignmentImportWorkbookTests
{
    [Fact]
    public void TemplateIncludesUsernamePostUniqueIdAndOptionalDateColumns()
    {
        using var stream = new MemoryStream(JobPostAssignmentImportWorkbook.CreateTemplate());
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet("تخصیص پست");

        Assert.Equal(
            ["نام کاربری", "شناسه یکتا پست", "تاریخ شروع", "تاریخ پایان"],
            worksheet.Row(1).CellsUsed().Select(cell => cell.GetString()));
    }

    [Fact]
    public void ReadTreatsBlankDatesAsNullAndParsesDateCell()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = CreateWorksheet(workbook);
            worksheet.Cell(2, 1).Value = "user.one";
            worksheet.Cell(2, 2).Value = "post-001";
            worksheet.Cell(3, 1).Value = "user.two";
            worksheet.Cell(3, 2).Value = "post-002";
            worksheet.Cell(3, 3).Value = new DateTime(2026, 11, 28);
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var rows = JobPostAssignmentImportWorkbook.Read(stream);

        Assert.Collection(
            rows,
            row =>
            {
                Assert.Equal("user.one", row.Username);
                Assert.Equal("post-001", row.JobPostUniqueId);
                Assert.Null(row.StartDate);
                Assert.Null(row.EndDate);
            },
            row => Assert.Equal("2026-11-28", row.StartDate));
    }

    [Theory]
    [InlineData("1403/01/15", true)]
    [InlineData("۲۰۲۶-11-28", true)]
    [InlineData("2026-11-28", true)]
    public void TryParseDateSupportsSolarAndIsoFormats(string value, bool expected)
    {
        var result = JobPostAssignmentImportWorkbook.TryParseDate(value, out var date);

        Assert.Equal(expected, result);
        if (value == "1403/01/15")
            Assert.Equal(new DateOnly(2024, 4, 3), date);
        if (value == "2026-11-28")
            Assert.Equal(new DateOnly(2026, 11, 28), date);
    }

    private static IXLWorksheet CreateWorksheet(XLWorkbook workbook)
    {
        var worksheet = workbook.Worksheets.Add("تخصیص پست");
        worksheet.Cell(1, 1).Value = "نام کاربری";
        worksheet.Cell(1, 2).Value = "شناسه یکتا پست";
        worksheet.Cell(1, 3).Value = "تاریخ شروع";
        worksheet.Cell(1, 4).Value = "تاریخ پایان";
        return worksheet;
    }
}
