using Backend.Services;
using ClosedXML.Excel;
using Xunit;

namespace Backend.Tests;

public class LocationImportWorkbookTests
{
    [Fact]
    public void TemplateContainsProvinceAndCityColumns()
    {
        using var stream = new MemoryStream(LocationImportWorkbook.CreateTemplate());
        using var workbook = new XLWorkbook(stream);

        var headers = workbook.Worksheet("Locations").Row(1).CellsUsed()
            .Select(cell => cell.GetString())
            .ToArray();

        Assert.Equal(["استان", "شهرستان"], headers);
    }

    [Fact]
    public void ReadParsesLocationRowsAndSkipsBlankRows()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Locations");
            worksheet.Cell(1, 1).Value = "استان";
            worksheet.Cell(1, 2).Value = "شهرستان";
            worksheet.Cell(3, 1).Value = " گیلان ";
            worksheet.Cell(3, 2).Value = "رشت";
            worksheet.Cell(4, 1).Value = "مازندران";
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var rows = LocationImportWorkbook.Read(stream);

        Assert.Collection(
            rows,
            row =>
            {
                Assert.Equal(3, row.RowNumber);
                Assert.Equal("گیلان", row.ProvinceName);
                Assert.Equal("رشت", row.CityName);
            },
            row =>
            {
                Assert.Equal(4, row.RowNumber);
                Assert.Equal("مازندران", row.ProvinceName);
                Assert.Null(row.CityName);
            });
    }

    [Fact]
    public void ReadRequiresBothHeaders()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            workbook.Worksheets.Add("Locations").Cell(1, 1).Value = "استان";
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var error = Assert.Throws<InvalidDataException>(() => LocationImportWorkbook.Read(stream));

        Assert.Contains("شهرستان", error.Message);
    }

    [Fact]
    public void NormalizeNameUnifiesCommonArabicCharacters()
    {
        Assert.Equal(
            LocationImportWorkbook.NormalizeName("كردستان يزد"),
            LocationImportWorkbook.NormalizeName("کردستان یزد"));
    }
}
