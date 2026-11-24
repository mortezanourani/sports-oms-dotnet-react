using Backend.Services;
using ClosedXML.Excel;
using Xunit;

namespace Backend.Tests;

public class JobPostTypeImportWorkbookTests
{
    [Fact]
    public void TemplateContainsTitleColumn()
    {
        using var stream = new MemoryStream(JobPostTypeImportWorkbook.CreateTemplate());
        using var workbook = new XLWorkbook(stream);

        Assert.Equal("عنوان نوع پست", workbook.Worksheet("انواع پست").Cell(1, 1).GetString());
    }

    [Fact]
    public void ReadParsesTitlesAndSkipsBlankRows()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("انواع پست");
            worksheet.Cell(1, 1).Value = "عنوان نوع پست";
            worksheet.Cell(2, 1).Value = " کارشناس ";
            worksheet.Cell(4, 1).Value = "مدیر";
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var rows = JobPostTypeImportWorkbook.Read(stream);

        Assert.Collection(
            rows,
            row =>
            {
                Assert.Equal(2, row.RowNumber);
                Assert.Equal("کارشناس", row.Title);
            },
            row =>
            {
                Assert.Equal(4, row.RowNumber);
                Assert.Equal("مدیر", row.Title);
            });
    }

    [Fact]
    public void ReadRequiresOneTitleColumn()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("انواع پست");
            worksheet.Cell(1, 1).Value = "عنوان نوع پست";
            worksheet.Cell(1, 2).Value = "عنوان نوع پست";
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var error = Assert.Throws<InvalidDataException>(() => JobPostTypeImportWorkbook.Read(stream));

        Assert.Contains("بیش از یک بار", error.Message);
    }
}
