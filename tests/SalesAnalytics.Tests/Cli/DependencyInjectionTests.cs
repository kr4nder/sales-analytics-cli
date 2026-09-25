using Microsoft.Extensions.DependencyInjection;
using SalesAnalytics.Cli.Composition;
using SalesAnalytics.Cli.Configuration;
using SalesAnalytics.Core.Abstractions;
using SalesAnalytics.Core.Analytics;
using SalesAnalytics.Core.Application;
using SalesAnalytics.Core.DependencyInjection;
using SalesAnalytics.Core.Output;
using SalesAnalytics.Core.Parsing;

namespace SalesAnalytics.Tests.Cli;

/// <summary>
/// Тесты регистрации и разрешения зависимостей в DI-контейнере.
/// </summary>
public sealed class DependencyInjectionTests
{
    [Theory]
    [InlineData(typeof(ISaleParser), typeof(SaleParser))]
    [InlineData(typeof(ICsvReader), typeof(CsvReader))]
    [InlineData(typeof(ISaleLoader), typeof(FileSaleLoader))]
    [InlineData(typeof(IResultWriterFactory), typeof(ResultWriterFactory))]
    public void AddSalesAnalytics_ResolvesEveryAbstractionToItsImplementation(Type service, Type expected)
    {
        using var provider = BuildProvider();

        Assert.IsType(expected, provider.GetRequiredService(service));
    }

    [Fact]
    public void AddSalesAnalytics_ResolvesPipelineWithAllDependencies()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<AnalyticsPipeline>());
    }

    [Fact]
    public void AddSalesAnalytics_ByDefault_RegistersSequentialAnalytics()
    {
        using var provider = BuildProvider();

        Assert.IsType<LinqAnalyticsService>(provider.GetRequiredService<IAnalyticsService>());
    }

    [Fact]
    public void AddSalesAnalytics_WithParallelFlag_RegistersParallelAnalytics()
    {
        using var provider = BuildProvider(new SalesAnalyticsOptions { UseParallelAnalytics = true });

        Assert.IsType<ParallelAnalyticsService>(provider.GetRequiredService<IAnalyticsService>());
    }

    [Theory]
    [InlineData(typeof(ISaleParser))]
    [InlineData(typeof(ICsvReader))]
    [InlineData(typeof(ISaleLoader))]
    [InlineData(typeof(IAnalyticsService))]
    [InlineData(typeof(IResultWriterFactory))]
    public void StatelessServices_AreRegisteredAsSingletons(Type service)
    {
        using var provider = BuildProvider();

        Assert.Same(provider.GetRequiredService(service), provider.GetRequiredService(service));
    }

    [Fact]
    public void Pipeline_IsRegisteredAsTransient()
    {
        using var provider = BuildProvider();

        Assert.NotSame(provider.GetRequiredService<AnalyticsPipeline>(), provider.GetRequiredService<AnalyticsPipeline>());
    }

    [Fact]
    public void AddSalesAnalytics_DeclaresExpectedLifetimes()
    {
        var services = new ServiceCollection().AddSalesAnalytics(Options());

        Assert.Equal(ServiceLifetime.Singleton, LifetimeOf(services, typeof(ISaleParser)));
        Assert.Equal(ServiceLifetime.Singleton, LifetimeOf(services, typeof(IAnalyticsService)));
        Assert.Equal(ServiceLifetime.Transient, LifetimeOf(services, typeof(AnalyticsPipeline)));
    }

    [Fact]
    public void AddSalesAnalytics_NullServices_Throws() =>
        Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.AddSalesAnalytics(null!));

    [Theory]
    [InlineData(ExecutionMode.Di)]
    [InlineData(ExecutionMode.Full)]
    public void PipelineComposer_DiModes_BuildThroughAContainerThatOwnsDisposal(ExecutionMode mode)
    {
        using var output = new StringWriter();

        using var composed = PipelineComposer.Create(CliOptions(mode), output);

        Assert.NotNull(composed.Pipeline);
        Assert.NotNull(composed.Scope);
    }

    [Theory]
    [InlineData(ExecutionMode.Console)]
    [InlineData(ExecutionMode.File)]
    [InlineData(ExecutionMode.Async)]
    [InlineData(ExecutionMode.Parallel)]
    public void PipelineComposer_NonDiModes_BuildWithoutAContainer(ExecutionMode mode)
    {
        using var output = new StringWriter();

        using var composed = PipelineComposer.Create(CliOptions(mode), output);

        Assert.NotNull(composed.Pipeline);
        Assert.Null(composed.Scope);
    }

    [Theory]
    [InlineData(ExecutionMode.Console, false, false, false)]
    [InlineData(ExecutionMode.File, false, false, false)]
    [InlineData(ExecutionMode.Async, true, false, false)]
    [InlineData(ExecutionMode.Parallel, false, true, false)]
    [InlineData(ExecutionMode.Di, false, false, true)]
    [InlineData(ExecutionMode.Full, true, true, true)]
    public void ExecutionMode_CapabilitiesMatchTheSpecification(
        ExecutionMode mode,
        bool asyncIo,
        bool parallel,
        bool dependencyInjection)
    {
        Assert.Equal(asyncIo, mode.UsesAsyncIo());
        Assert.Equal(parallel, mode.UsesParallelAnalytics());
        Assert.Equal(dependencyInjection, mode.UsesDependencyInjection());
    }

    private static ServiceProvider BuildProvider(SalesAnalyticsOptions? options = null) =>
        new ServiceCollection()
            .AddSalesAnalytics(options ?? Options())
            .BuildServiceProvider(validateScopes: true);

    private static SalesAnalyticsOptions Options() => new() { ConsoleOutput = TextWriter.Null };

    private static ServiceLifetime LifetimeOf(IServiceCollection services, Type serviceType) =>
        services.Last(descriptor => descriptor.ServiceType == serviceType).Lifetime;

    private static CommandLineOptions CliOptions(ExecutionMode mode) => new()
    {
        Mode = mode,
        InputPath = "sales.csv",
        OutputPath = mode == ExecutionMode.File ? "result.json" : null,
    };
}
