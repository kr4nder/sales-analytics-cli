using Microsoft.Extensions.DependencyInjection;
using SalesAnalytics.Core.Abstractions;
using SalesAnalytics.Core.Analytics;
using SalesAnalytics.Core.Application;
using SalesAnalytics.Core.Output;
using SalesAnalytics.Core.Parsing;

namespace SalesAnalytics.Core.DependencyInjection;

/// <summary>
/// Настройки, от которых зависит состав регистрируемых сервисов.
/// </summary>
public sealed record SalesAnalyticsOptions
{
    /// <summary>Использовать параллельную реализацию расчёта вместо последовательной.</summary>
    public bool UseParallelAnalytics { get; init; }

    /// <summary>Приёмник консольного вывода. По умолчанию — стандартный вывод процесса.</summary>
    public TextWriter ConsoleOutput { get; init; } = Console.Out;

    /// <summary>Настройки консольного отчёта.</summary>
    public ConsoleReportOptions ConsoleReport { get; init; } = new();

    /// <summary>Настройки чтения CSV.</summary>
    public CsvReaderOptions CsvReader { get; init; } = new();
}

/// <summary>
/// Регистрация сервисов приложения в контейнере.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует все сервисы, необходимые для работы <see cref="AnalyticsPipeline"/>.
    /// </summary>
    /// <remarks>
    /// Выбор времени жизни:
    /// <list type="bullet">
    /// <item><description>
    /// <see cref="ISaleParser"/>, <see cref="ICsvReader"/>, <see cref="ISaleLoader"/> и
    /// <see cref="IAnalyticsService"/> — <c>Singleton</c>: они не хранят состояние между вызовами
    /// (состояние разбора живёт внутри одного вызова метода), поэтому потокобезопасны
    /// и не требуют пересоздания.
    /// </description></item>
    /// <item><description>
    /// <see cref="IResultWriterFactory"/> — <c>Singleton</c>: хранит только ссылку на
    /// <see cref="TextWriter"/> и настройки вывода, сами писатели создаются на каждый запуск.
    /// </description></item>
    /// <item><description>
    /// <see cref="AnalyticsPipeline"/> — <c>Transient</c>: это объект-сценарий одного запуска.
    /// Он дешёвый, и отдельный экземпляр на запуск избавляет от риска разделяемого состояния,
    /// если сценарий когда-нибудь начнут выполнять параллельно.
    /// </description></item>
    /// </list>
    /// </remarks>
    /// <param name="services">Коллекция сервисов.</param>
    /// <param name="options">Настройки состава сервисов.</param>
    public static IServiceCollection AddSalesAnalytics(
        this IServiceCollection services,
        SalesAnalyticsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var effectiveOptions = options ?? new SalesAnalyticsOptions();

        services.AddSingleton(effectiveOptions);
        services.AddSingleton(effectiveOptions.CsvReader);
        services.AddSingleton(effectiveOptions.ConsoleReport);

        services.AddSingleton<ISaleParser, SaleParser>();
        services.AddSingleton<ICsvReader>(provider =>
            new CsvReader(provider.GetRequiredService<ISaleParser>(), effectiveOptions.CsvReader));
        services.AddSingleton<ISaleLoader, FileSaleLoader>();

        // Единственное различие между режимами console/di и parallel/full
        // на уровне контейнера — какая реализация расчёта зарегистрирована.
        if (effectiveOptions.UseParallelAnalytics)
        {
            services.AddSingleton<IAnalyticsService, ParallelAnalyticsService>();
        }
        else
        {
            services.AddSingleton<IAnalyticsService, LinqAnalyticsService>();
        }

        services.AddSingleton<IResultWriterFactory>(_ =>
            new ResultWriterFactory(effectiveOptions.ConsoleOutput, effectiveOptions.ConsoleReport));

        services.AddTransient<AnalyticsPipeline>();

        return services;
    }
}
