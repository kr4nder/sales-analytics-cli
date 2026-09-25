using System.Text;
using System.Text.Json;
using SalesAnalytics.Core.Analytics;
using SalesAnalytics.Core.Exceptions;
using SalesAnalytics.Core.Models;
using SalesAnalytics.Core.Output;

namespace SalesAnalytics.Tests.Output;

/// <summary>
/// Тесты вывода результата. Файлы не создаются: консольный писатель проверяется
/// через <see cref="StringWriter"/>, JSON-писатель — через <see cref="MemoryStream"/>.
/// </summary>
public sealed class ResultWriterTests
{
    private static readonly AnalyticsReport Report =
        new LinqAnalyticsService().Analyze(TestData.SampleSales, TestData.Request());

    [Fact]
    public void ConsoleWriter_RendersEveryAnalyticsBlock()
    {
        using var output = new StringWriter();

        new ConsoleResultWriter(output).Write(Report);
        var text = output.ToString();

        Assert.Contains("Общая сумма продаж по категориям", text);
        Assert.Contains("категорий по количеству проданных единиц", text);
        Assert.Contains("Средняя цена за единицу товара по месяцам", text);
        Assert.Contains("покупателей по рейтингу", text);
        Assert.Contains("Время доставки", text);
        Assert.Contains("Средняя скидка по категориям", text);
    }

    [Fact]
    public void ConsoleWriter_FormatsNumbersInRussianCulture()
    {
        using var output = new StringWriter();

        new ConsoleResultWriter(output).Write(Report);
        var text = output.ToString();

        // 3549.39 в русской культуре — «3 549,39» с неразрывным пробелом между разрядами.
        Assert.Contains("549,39", text);
        Assert.Contains("Январь 2022", text);
    }

    [Fact]
    public void ConsoleWriter_IncludesMetadata()
    {
        using var output = new StringWriter();

        new ConsoleResultWriter(output).Write(Report);
        var text = output.ToString();

        Assert.Contains("sample.csv", text);
        Assert.Contains("весь файл", text);
    }

    [Fact]
    public void ConsoleWriter_RowLimitReached_AddsTruncationNote()
    {
        var sales = TestData.GenerateSales(600);
        var report = new LinqAnalyticsService().Analyze(sales, TestData.Request());
        using var output = new StringWriter();

        new ConsoleResultWriter(output, new ConsoleReportOptions { MaxRowsPerBlock = 3 }).Write(report);
        var text = output.ToString();

        Assert.Contains("показаны первые 3", text);
    }

    [Fact]
    public void ConsoleWriter_UnlimitedRows_OmitsTruncationNote()
    {
        using var output = new StringWriter();

        new ConsoleResultWriter(output, new ConsoleReportOptions { MaxRowsPerBlock = 0 }).Write(Report);

        Assert.DoesNotContain("показаны первые", output.ToString());
    }

    [Fact]
    public async Task ConsoleWriter_WriteAsync_ProducesSameTextAsWrite()
    {
        using var syncOutput = new StringWriter();
        using var asyncOutput = new StringWriter();

        new ConsoleResultWriter(syncOutput).Write(Report);
        await new ConsoleResultWriter(asyncOutput).WriteAsync(Report);

        Assert.Equal(syncOutput.ToString(), asyncOutput.ToString());
    }

    [Fact]
    public void JsonWriter_UsesSnakeCasePropertyNames()
    {
        using var stream = new MemoryStream();

        new JsonResultWriter(() => stream, leaveOpen: true).Write(Report);
        var json = Encoding.UTF8.GetString(stream.ToArray());

        Assert.Contains("\"sales_by_category\"", json);
        Assert.Contains("\"top_categories_by_quantity\"", json);
        Assert.Contains("\"average_discount_by_category_and_month\"", json);
        Assert.Contains("\"gross_amount\"", json);
    }

    [Fact]
    public void JsonWriter_ProducesDocumentThatDeserialisesBack()
    {
        using var stream = new MemoryStream();

        new JsonResultWriter(() => stream, leaveOpen: true).Write(Report);
        var restored = JsonSerializer.Deserialize<AnalyticsReport>(stream.ToArray(), ReportJson.Options);

        Assert.NotNull(restored);
        Assert.Equal(Report.SalesByCategory, restored.SalesByCategory);
        Assert.Equal(Report.Delivery, restored.Delivery);
        Assert.Equal(Report.Metadata.SourceFile, restored.Metadata.SourceFile);
    }

    [Fact]
    public void JsonWriter_DoesNotEscapeCyrillic()
    {
        var report = Report with
        {
            Metadata = Report.Metadata with { SourceFile = "продажи.csv" },
        };
        using var stream = new MemoryStream();

        new JsonResultWriter(() => stream, leaveOpen: true).Write(report);
        var json = Encoding.UTF8.GetString(stream.ToArray());

        Assert.Contains("продажи.csv", json);
    }

    [Fact]
    public async Task JsonWriter_WriteAsync_MatchesWrite()
    {
        using var syncStream = new MemoryStream();
        using var asyncStream = new MemoryStream();

        new JsonResultWriter(() => syncStream, leaveOpen: true).Write(Report);
        await new JsonResultWriter(() => asyncStream, leaveOpen: true).WriteAsync(Report);

        Assert.Equal(syncStream.ToArray(), asyncStream.ToArray());
    }

    [Fact]
    public void Factory_WithoutOutputPath_CreatesConsoleWriter()
    {
        using var output = new StringWriter();

        var writer = new ResultWriterFactory(output).Create(outputPath: null);

        Assert.IsType<ConsoleResultWriter>(writer);
    }

    [Fact]
    public void Factory_WithOutputPath_CreatesJsonWriter()
    {
        using var output = new StringWriter();

        var writer = new ResultWriterFactory(output).Create(Path.Combine(Path.GetTempPath(), "report.json"));

        Assert.IsType<JsonResultWriter>(writer);
    }

    [Fact]
    public void Factory_MissingDirectory_ThrowsBeforeAnyWrite()
    {
        using var output = new StringWriter();
        var path = Path.Combine(Path.GetTempPath(), $"нет-каталога-{Guid.NewGuid():N}", "report.json");

        var exception = Assert.Throws<OutputPathException>(() => new ResultWriterFactory(output).Create(path));

        Assert.Contains("не существует", exception.Message);
    }

    [Fact]
    public void Factory_OutputPathIsDirectory_Throws()
    {
        using var output = new StringWriter();

        var exception = Assert.Throws<OutputPathException>(
            () => new ResultWriterFactory(output).Create(Path.GetTempPath()));

        Assert.Contains("каталог", exception.Message);
    }
}
