using Backend.Services;
using ClosedXML.Excel;
using Xunit;

namespace Backend.Tests;

public class JobPostImportWorkbookTests
{
    [Fact]
    public void TemplateContainsEveryJobPostFieldExceptId()
    {
        using var stream = new MemoryStream(JobPostImportWorkbook.CreateTemplate());
        using var workbook = new XLWorkbook(stream);
        var headers = workbook.Worksheet("پست‌های سازمانی").Row(1).CellsUsed()
            .Select(cell => cell.GetString())
            .ToArray();

        Assert.Equal(
            [
                "نوع پست", "واحد سازمانی", "کد نوع پست", "شماره پست", "شناسه یکتا پست",
                "عنوان پست", "نوع مدیر حرفه‌ای", "شکسته‌شده"
            ],
            headers);
        Assert.Single(workbook.Worksheet("پست‌های سازمانی").DataValidations);
    }

    [Fact]
    public void ReadParsesAllColumnsAndSkipsBlankRows()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = CreateWorksheet(workbook);
            worksheet.Cell(2, 1).Value = "نوع پست";
            worksheet.Cell(2, 2).Value = "واحد سازمانی";
            worksheet.Cell(2, 3).Value = "001";
            worksheet.Cell(2, 4).Value = "شماره ۰۰۱";
            worksheet.Cell(2, 5).Value = "شناسه-۰۰۱";
            worksheet.Cell(2, 6).Value = "عنوان";
            worksheet.Cell(2, 7).Value = "مدیر پایه";
            worksheet.Cell(2, 8).Value = "بله";
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var row = Assert.Single(JobPostImportWorkbook.Read(stream));

        Assert.Equal(2, row.RowNumber);
        Assert.Equal("نوع پست", row.JobPostTypeTitle);
        Assert.Equal("واحد سازمانی", row.OrgUnitTitle);
        Assert.Equal("001", row.TypeCode);
        Assert.Equal("شماره ۰۰۱", row.JobPostCode);
        Assert.Equal("شناسه-۰۰۱", row.JobPostUniqueId);
        Assert.Equal("عنوان", row.Title);
        Assert.Equal("مدیر پایه", row.ManagerType);
        Assert.Equal("بله", row.BreakOut);
    }

    private static IXLWorksheet CreateWorksheet(XLWorkbook workbook)
    {
        var worksheet = workbook.Worksheets.Add("پست‌های سازمانی");
        worksheet.Cell(1, 1).Value = "نوع پست";
        worksheet.Cell(1, 2).Value = "واحد سازمانی";
        worksheet.Cell(1, 3).Value = "کد نوع پست";
        worksheet.Cell(1, 4).Value = "شماره پست";
        worksheet.Cell(1, 5).Value = "شناسه یکتا پست";
        worksheet.Cell(1, 6).Value = "عنوان پست";
        worksheet.Cell(1, 7).Value = "نوع مدیر حرفه‌ای";
        worksheet.Cell(1, 8).Value = "شکسته‌شده";
        return worksheet;
    }
}
