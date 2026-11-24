using Backend.Services;
using ClosedXML.Excel;
using Xunit;

namespace Backend.Tests;

public class OrgUnitImportWorkbookTests
{
    [Fact]
    public void TemplateContainsAllFieldsExceptId()
    {
        using var stream = new MemoryStream(OrgUnitImportWorkbook.CreateTemplate());
        using var workbook = new XLWorkbook(stream);
        var headers = workbook.Worksheet("واحدهای سازمانی").Row(1).CellsUsed()
            .Select(cell => cell.GetString())
            .ToArray();

        Assert.Equal(
            ["عنوان واحد سازمانی", "نوع واحد سازمانی", "دامنه", "واحد سازمانی والد"],
            headers);
    }

    [Fact]
    public void ReadParsesRowsAndTreatsEmptyParentAsNull()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("واحدهای سازمانی");
            worksheet.Cell(1, 1).Value = "عنوان واحد سازمانی";
            worksheet.Cell(1, 2).Value = "نوع واحد سازمانی";
            worksheet.Cell(1, 3).Value = "دامنه";
            worksheet.Cell(1, 4).Value = "واحد سازمانی والد";
            worksheet.Cell(2, 1).Value = "اداره ورزش";
            worksheet.Cell(2, 2).Value = "اداره";
            worksheet.Cell(2, 3).Value = "Provincial (استانی)";
            worksheet.Cell(3, 1).Value = "گروه ورزش";
            worksheet.Cell(3, 2).Value = "Group";
            worksheet.Cell(3, 3).Value = "City (شهرستانی)";
            worksheet.Cell(3, 4).Value = "اداره ورزش";
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var rows = OrgUnitImportWorkbook.Read(stream);

        Assert.Collection(
            rows,
            row =>
            {
                Assert.Equal(2, row.RowNumber);
                Assert.Equal("اداره ورزش", row.Title);
                Assert.Equal("اداره", row.Type);
                Assert.Equal("Provincial (استانی)", row.Scope);
                Assert.Null(row.ParentTitle);
            },
            row =>
            {
                Assert.Equal(3, row.RowNumber);
                Assert.Equal("اداره ورزش", row.ParentTitle);
            });
    }
}
