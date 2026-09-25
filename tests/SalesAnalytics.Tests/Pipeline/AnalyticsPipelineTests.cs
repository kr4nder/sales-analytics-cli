using Moq;
using SalesAnalytics.Core.Abstractions;
using SalesAnalytics.Core.Application;
using SalesAnalytics.Core.Models;

namespace SalesAnalytics.Tests.Pipeline;

/// <summary>
/// Тесты конвейера на моках: проверяют порядок шагов и то, что асинхронный режим
/// действительно использует асинхронные методы зависимостей, а не синхронные под обёрткой.
/// </summary>
public sealed class AnalyticsPipelineTests
{
    private readonly Mock<ISaleLoader> _loader = new(MockBehavior.Strict);
    private readonly Mock<IAnalyticsService> _analytics = new(MockBehavior.Strict);
    private readonly Mock<IResultWriterFactory> _writerFactory = new(MockBehavior.Strict);
    private readonly Mock<IResultWriter> _writer = new(MockBehavior.Strict);
    private readonly AnalyticsReport _report = CreateReport();

    public AnalyticsPipelineTests() =>
        _writerFactory.Setup(factory => factory.Create(It.IsAny<string?>())).Returns(_writer.Object);

    private AnalyticsPipeline CreatePipeline() =>
        new(_loader.Object, _analytics.Object, _writerFactory.Object);

    [Fact]
    public async Task ExecuteAsync_SyncMode_UsesSynchronousDependencies()
    {
        _loader.Setup(loader => loader.Load("in.csv")).Returns(TestData.SampleSales);
        _analytics.Setup(service => service.Analyze(TestData.SampleSales, It.IsAny<AnalyticsRequest>())).Returns(_report);
        _writer.Setup(writer => writer.Write(_report));

        var result = await CreatePipeline().ExecuteAsync(Request(useAsyncIo: false));

        Assert.Same(_report, result);
        _loader.Verify(loader => loader.Load("in.csv"), Times.Once);
        _loader.Verify(loader => loader.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _writer.Verify(writer => writer.Write(_report), Times.Once);
        _writer.Verify(writer => writer.WriteAsync(It.IsAny<AnalyticsReport>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_AsyncMode_UsesAsynchronousDependencies()
    {
        // Task.FromResult достаточно: проверяется, какие методы вызваны,
        // а не сколько времени они выполняются.
        _loader
            .Setup(loader => loader.LoadAsync("in.csv", It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(TestData.SampleSales));
        _analytics
            .Setup(service => service.AnalyzeAsync(TestData.SampleSales, It.IsAny<AnalyticsRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(_report));
        _writer
            .Setup(writer => writer.WriteAsync(_report, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreatePipeline().ExecuteAsync(Request(useAsyncIo: true));

        Assert.Same(_report, result);
        _loader.Verify(loader => loader.LoadAsync("in.csv", It.IsAny<CancellationToken>()), Times.Once);
        _loader.Verify(loader => loader.Load(It.IsAny<string>()), Times.Never);
        _analytics.Verify(service => service.Analyze(It.IsAny<IReadOnlyList<Sale>>(), It.IsAny<AnalyticsRequest>()), Times.Never);
        _writer.Verify(writer => writer.Write(It.IsAny<AnalyticsReport>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_CreatesWriterBeforeReadingInput()
    {
        // Каталог результата должен проверяться до долгого чтения и расчёта.
        var sequence = new List<string>();

        _writerFactory
            .Setup(factory => factory.Create("out.json"))
            .Callback(() => sequence.Add("create-writer"))
            .Returns(_writer.Object);
        _loader
            .Setup(loader => loader.Load("in.csv"))
            .Callback(() => sequence.Add("load"))
            .Returns(TestData.SampleSales);
        _analytics
            .Setup(service => service.Analyze(It.IsAny<IReadOnlyList<Sale>>(), It.IsAny<AnalyticsRequest>()))
            .Callback(() => sequence.Add("analyze"))
            .Returns(_report);
        _writer
            .Setup(writer => writer.Write(_report))
            .Callback(() => sequence.Add("write"));

        await CreatePipeline().ExecuteAsync(Request(useAsyncIo: false) with { OutputPath = "out.json" });

        Assert.Equal(["create-writer", "load", "analyze", "write"], sequence);
    }

    [Fact]
    public async Task ExecuteAsync_PassesModeAndPeriodIntoAnalyticsRequest()
    {
        var period = new AnalyticsPeriod(new DateOnly(2022, 1, 1), new DateOnly(2023, 1, 1));
        AnalyticsRequest? captured = null;

        _loader.Setup(loader => loader.Load("in.csv")).Returns(TestData.SampleSales);
        _analytics
            .Setup(service => service.Analyze(It.IsAny<IReadOnlyList<Sale>>(), It.IsAny<AnalyticsRequest>()))
            .Callback<IReadOnlyList<Sale>, AnalyticsRequest>((_, request) => captured = request)
            .Returns(_report);
        _writer.Setup(writer => writer.Write(_report));

        await CreatePipeline().ExecuteAsync(Request(useAsyncIo: false) with { Period = period });

        Assert.NotNull(captured);
        Assert.Equal("in.csv", captured.SourceFile);
        Assert.Equal("unit-test", captured.Mode);
        Assert.Equal(period, captured.Period);
    }

    [Fact]
    public async Task ExecuteAsync_NullRequest_Throws() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreatePipeline().ExecuteAsync(null!));

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new AnalyticsPipeline(null!, _analytics.Object, _writerFactory.Object));
        Assert.Throws<ArgumentNullException>(() => new AnalyticsPipeline(_loader.Object, null!, _writerFactory.Object));
        Assert.Throws<ArgumentNullException>(() => new AnalyticsPipeline(_loader.Object, _analytics.Object, null!));
    }

    private static PipelineRequest Request(bool useAsyncIo) => new()
    {
        InputPath = "in.csv",
        Mode = "unit-test",
        UseAsyncIo = useAsyncIo,
    };

    private static AnalyticsReport CreateReport() =>
        new(
            new ReportMetadata("in.csv", "unit-test", 5, 5, null, null, DateTimeOffset.UnixEpoch),
            [],
            [],
            [],
            [],
            new DeliveryStatistics(0m, 0, 0),
            []);
}
