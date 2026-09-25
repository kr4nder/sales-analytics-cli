using Moq;
using SalesAnalytics.Core.Abstractions;
using SalesAnalytics.Core.Exceptions;
using SalesAnalytics.Core.Models;
using SalesAnalytics.Core.Parsing;
using SalesAnalytics.Tests.Fakes;

namespace SalesAnalytics.Tests.Parsing;

/// <summary>
/// Тесты обхода файла: заголовок, пустые строки, разделители и асинхронное чтение.
/// Реальные файлы не используются — источником служит <see cref="StringReader"/>.
/// </summary>
public sealed class CsvReaderTests
{
    private readonly CsvReader _reader = new(new SaleParser());

    [Fact]
    public void Read_SampleFile_SkipsHeaderAndReturnsAllRows()
    {
        using var source = new StringReader(TestData.SampleCsv);

        var sales = _reader.Read(source);

        Assert.Equal(5, sales.Count);
        Assert.Equal(10001, sales[0].OrderId);
        Assert.Equal(10005, sales[4].OrderId);
    }

    [Fact]
    public void Read_BlankLinesBetweenRows_AreIgnored()
    {
        var csv = string.Join(
            Environment.NewLine,
            TestData.Header,
            TestData.SampleRows[0],
            string.Empty,
            "   ",
            TestData.SampleRows[1],
            string.Empty);

        using var source = new StringReader(csv);

        var sales = _reader.Read(source);

        Assert.Equal(2, sales.Count);
    }

    [Fact]
    public void Read_FileWithoutHeader_ParsesFirstLineAsData()
    {
        using var source = new StringReader(TestData.SampleRows[0]);

        var sales = _reader.Read(source);

        Assert.Single(sales);
        Assert.Equal(10001, sales[0].OrderId);
    }

    [Fact]
    public void Read_EmptySource_ReturnsEmptyList()
    {
        using var source = new StringReader(string.Empty);

        Assert.Empty(_reader.Read(source));
    }

    [Fact]
    public void Read_HeaderOnly_ReturnsEmptyList()
    {
        using var source = new StringReader(TestData.Header);

        Assert.Empty(_reader.Read(source));
    }

    [Fact]
    public void Read_SemicolonDelimiter_IsDetectedFromHeader()
    {
        var csv = string.Join(
            Environment.NewLine,
            TestData.Header.Replace(',', ';'),
            TestData.SampleRows[0].Replace(',', ';'));

        using var source = new StringReader(csv);

        var sales = _reader.Read(source);

        Assert.Single(sales);
        Assert.Equal("Beauty", sales[0].ProductCategory);
    }

    [Fact]
    public void Read_InvalidRow_ThrowsWithPhysicalLineNumber()
    {
        var csv = string.Join(
            Environment.NewLine,
            TestData.Header,
            TestData.SampleRows[0],
            "10002,не дата,1435,Clothing,South,7,47.74,0.09,Card,6,3.9,304.1");

        using var source = new StringReader(csv);

        var exception = Assert.Throws<CsvFormatException>(() => _reader.Read(source));

        // Заголовок — строка 1, первая запись — строка 2, ошибка — в строке 3.
        Assert.Equal(3, exception.LineNumber);
    }

    [Fact]
    public void Read_SkipInvalidRowsEnabled_DropsBadRowsInsteadOfThrowing()
    {
        var reader = new CsvReader(new SaleParser(), new CsvReaderOptions { SkipInvalidRows = true });
        var csv = string.Join(
            Environment.NewLine,
            TestData.Header,
            TestData.SampleRows[0],
            "битая строка",
            TestData.SampleRows[1]);

        using var source = new StringReader(csv);

        var sales = reader.Read(source);

        Assert.Equal(2, sales.Count);
    }

    [Fact]
    public async Task ReadAsync_ReturnsSameResultAsRead()
    {
        using var syncSource = new StringReader(TestData.SampleCsv);
        using var asyncSource = new StringReader(TestData.SampleCsv);

        var expected = _reader.Read(syncSource);
        var actual = await _reader.ReadAsync(asyncSource);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ReadAsync_UsesAsynchronousReadsOnly()
    {
        using var source = new TrackingTextReader(new StringReader(TestData.SampleCsv));

        await _reader.ReadAsync(source);

        Assert.Equal(0, source.SyncReadCount);
        // Пять записей, заголовок и завершающее чтение, вернувшее null.
        Assert.Equal(7, source.AsyncReadCount);
    }

    [Fact]
    public async Task ReadAsync_CancelledToken_StopsReading()
    {
        using var source = new StringReader(TestData.SampleCsv);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _reader.ReadAsync(source, cancellation.Token));
    }

    [Fact]
    public void Read_DelegatesEveryDataLineToParser()
    {
        // Мок подтверждает, что читатель не разбирает строки сам
        // и не передаёт парсеру заголовок.
        var parser = new Mock<ISaleParser>(MockBehavior.Strict);
        var sale = TestData.Sale();

        parser
            .Setup(p => p.TryParse(It.IsAny<string>(), It.IsAny<int>(), ',', out It.Ref<Sale?>.IsAny, out It.Ref<string?>.IsAny))
            .Returns(new TryParseCallback((string _, int _, char _, out Sale? result, out string? error) =>
            {
                result = sale;
                error = null;
                return true;
            }));

        var reader = new CsvReader(parser.Object);
        using var source = new StringReader(TestData.SampleCsv);

        var sales = reader.Read(source);

        Assert.Equal(5, sales.Count);
        parser.Verify(
            p => p.TryParse(It.IsAny<string>(), It.IsAny<int>(), ',', out It.Ref<Sale?>.IsAny, out It.Ref<string?>.IsAny),
            Times.Exactly(5));
        parser.Verify(
            p => p.TryParse(TestData.Header, It.IsAny<int>(), It.IsAny<char>(), out It.Ref<Sale?>.IsAny, out It.Ref<string?>.IsAny),
            Times.Never);
    }

    /// <summary>Делегат под сигнатуру <see cref="ISaleParser.TryParse"/> для настройки мока.</summary>
    private delegate bool TryParseCallback(string line, int lineNumber, char delimiter, out Sale? sale, out string? error);
}
