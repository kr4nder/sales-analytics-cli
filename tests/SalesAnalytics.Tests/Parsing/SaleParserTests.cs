using SalesAnalytics.Core.Exceptions;
using SalesAnalytics.Core.Parsing;

namespace SalesAnalytics.Tests.Parsing;

/// <summary>
/// Тесты разбора отдельной строки CSV, включая граничные случаи.
/// </summary>
public sealed class SaleParserTests
{
    private readonly SaleParser _parser = new();

    [Fact]
    public void Parse_ValidRow_FillsAllFields()
    {
        var sale = _parser.Parse(TestData.SampleRows[0], lineNumber: 2, delimiter: ',');

        Assert.Equal(10001, sale.OrderId);
        Assert.Equal(new DateOnly(2022, 1, 1), sale.OrderDate);
        Assert.Equal(1102, sale.CustomerId);
        Assert.Equal("Beauty", sale.ProductCategory);
        Assert.Equal("South", sale.Region);
        Assert.Equal(7, sale.Quantity);
        Assert.Equal(373.65m, sale.UnitPrice);
        Assert.Equal(0.28m, sale.Discount);
        Assert.Equal("Wallet", sale.PaymentMethod);
        Assert.Equal(10, sale.DeliveryDays);
        Assert.Equal(4.7m, sale.CustomerRating);
        Assert.Equal(1883.2m, sale.Revenue);
    }

    [Fact]
    public void Parse_ValidRow_ComputesDerivedValues()
    {
        var sale = _parser.Parse(TestData.SampleRows[0], lineNumber: 2, delimiter: ',');

        // 7 * 373.65 — значение из примера ожидаемого вывода в задании.
        Assert.Equal(2615.55m, sale.GrossAmount);
        Assert.Equal(1883.2m, sale.NetRevenue);
        Assert.Equal(new DateOnly(2022, 1, 1), sale.MonthKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void TryParse_BlankLine_ReturnsFalse(string line)
    {
        var parsed = _parser.TryParse(line, lineNumber: 5, delimiter: ',', out var sale, out var error);

        Assert.False(parsed);
        Assert.Null(sale);
        Assert.Equal("строка пустая.", error);
    }

    [Fact]
    public void TryParse_InvalidDate_ReportsColumnName()
    {
        var line = "10001,31/31/2022,1102,Beauty,South,7,373.65,0.28,Wallet,10,4.7,1883.2";

        var parsed = _parser.TryParse(line, lineNumber: 2, delimiter: ',', out var sale, out var error);

        Assert.False(parsed);
        Assert.Null(sale);
        Assert.Contains("order_date", error);
        Assert.Contains("31/31/2022", error);
    }

    [Fact]
    public void TryParse_WrongColumnCount_ReportsExpectedAndActual()
    {
        var parsed = _parser.TryParse("10001,1/1/2022,1102", lineNumber: 2, delimiter: ',', out _, out var error);

        Assert.False(parsed);
        Assert.Equal("ожидалось 12 колонок, получено 3.", error);
    }

    [Theory]
    [InlineData("10001,1/1/2022,1102,Beauty,South,abc,373.65,0.28,Wallet,10,4.7,1883.2", "quantity")]
    [InlineData("10001,1/1/2022,1102,Beauty,South,7,цена,0.28,Wallet,10,4.7,1883.2", "unit_price")]
    [InlineData("abc,1/1/2022,1102,Beauty,South,7,373.65,0.28,Wallet,10,4.7,1883.2", "order_id")]
    [InlineData("10001,1/1/2022,1102,,South,7,373.65,0.28,Wallet,10,4.7,1883.2", "product_category")]
    public void TryParse_InvalidField_NamesTheOffendingColumn(string line, string expectedColumn)
    {
        var parsed = _parser.TryParse(line, lineNumber: 2, delimiter: ',', out _, out var error);

        Assert.False(parsed);
        Assert.Contains(expectedColumn, error);
    }

    [Theory]
    [InlineData("10001,1/1/2022,1102,Beauty,South,-1,373.65,0.28,Wallet,10,4.7,1883.2", "количество")]
    [InlineData("10001,1/1/2022,1102,Beauty,South,7,373.65,1.5,Wallet,10,4.7,1883.2", "скидка")]
    [InlineData("10001,1/1/2022,1102,Beauty,South,7,373.65,0.28,Wallet,-3,4.7,1883.2", "срок доставки")]
    [InlineData("10001,1/1/2022,1102,Beauty,South,7,373.65,0.28,Wallet,10,9.9,1883.2", "оценка")]
    public void TryParse_ValueOutOfRange_ReportsMeaningfulReason(string line, string expectedReason)
    {
        var parsed = _parser.TryParse(line, lineNumber: 2, delimiter: ',', out _, out var error);

        Assert.False(parsed);
        Assert.Contains(expectedReason, error);
    }

    [Fact]
    public void Parse_InvalidLine_ThrowsWithLineNumber()
    {
        var exception = Assert.Throws<CsvFormatException>(
            () => _parser.Parse("мусор", lineNumber: 17, delimiter: ','));

        Assert.Equal(17, exception.LineNumber);
        Assert.Contains("17", exception.Message);
    }

    [Fact]
    public void Parse_SemicolonDelimiterWithCommaDecimals_IsSupported()
    {
        var line = "10001;01.01.2022;1102;Beauty;South;7;373,65;0,28;Wallet;10;4,7;1883,2";

        var sale = _parser.Parse(line, lineNumber: 2, delimiter: ';');

        Assert.Equal(373.65m, sale.UnitPrice);
        Assert.Equal(0.28m, sale.Discount);
        Assert.Equal(new DateOnly(2022, 1, 1), sale.OrderDate);
    }

    [Fact]
    public void Parse_PaddedFields_TrimsWhitespace()
    {
        // В задании пример таблицы выровнен пробелами — такой ввод должен приниматься.
        var line = "10001,  1/1/2022,  1102,  Beauty,  South,  7,  373.65,  0.28,  Wallet,  10,  4.7,  1883.2";

        var sale = _parser.Parse(line, lineNumber: 2, delimiter: ',');

        Assert.Equal("Beauty", sale.ProductCategory);
        Assert.Equal(373.65m, sale.UnitPrice);
    }

    [Theory]
    [InlineData("1/1/2022")]
    [InlineData("01/01/2022")]
    [InlineData("2022-01-01")]
    [InlineData("1.1.2022")]
    [InlineData("01.01.2022")]
    public void Parse_SupportedDateFormats_AreAccepted(string date)
    {
        var line = $"10001,{date},1102,Beauty,South,7,373.65,0.28,Wallet,10,4.7,1883.2";

        var sale = _parser.Parse(line, lineNumber: 2, delimiter: ',');

        Assert.Equal(new DateOnly(2022, 1, 1), sale.OrderDate);
    }
}
