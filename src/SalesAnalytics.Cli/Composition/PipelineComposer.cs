using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SalesAnalytics.Cli.Configuration;
using SalesAnalytics.Core.Analytics;
using SalesAnalytics.Core.Application;
using SalesAnalytics.Core.DependencyInjection;
using SalesAnalytics.Core.Output;
using SalesAnalytics.Core.Parsing;

namespace SalesAnalytics.Cli.Composition;

/// <summary>
/// Готовый к запуску конвейер вместе с ресурсами, которые нужно освободить после работы
/// (для DI-режимов это построенный <see cref="IHost"/>).
/// </summary>
/// <param name="Pipeline">Конвейер обработки.</param>
/// <param name="Scope">Владелец зависимостей или <see langword="null"/>, если контейнер не использовался.</param>
public sealed record ComposedPipeline(AnalyticsPipeline Pipeline, IDisposable? Scope) : IDisposable
{
    /// <inheritdoc />
    public void Dispose() => Scope?.Dispose();
}

/// <summary>
/// Собирает конвейер под выбранный режим работы.
/// Режимы <c>di</c> и <c>full</c> строятся на <see cref="IHost"/>, остальные — прямым
/// созданием объектов: так видно, что доменный код одинаково работает и с контейнером, и без него.
/// </summary>
public static class PipelineComposer
{
    /// <summary>
    /// Создаёт конвейер по параметрам запуска.
    /// </summary>
    /// <param name="options">Разобранные аргументы командной строки.</param>
    /// <param name="consoleOutput">Приёмник консольного вывода.</param>
    public static ComposedPipeline Create(CommandLineOptions options, TextWriter consoleOutput)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(consoleOutput);

        var serviceOptions = new SalesAnalyticsOptions
        {
            UseParallelAnalytics = options.Mode.UsesParallelAnalytics(),
            ConsoleOutput = consoleOutput,
            ConsoleReport = new ConsoleReportOptions { MaxRowsPerBlock = options.MaxRowsPerBlock },
        };

        return options.Mode.UsesDependencyInjection()
            ? CreateWithHost(serviceOptions)
            : CreateManually(serviceOptions);
    }

    /// <summary>
    /// Режимы <c>di</c> и <c>full</c>: сервисы регистрируются в контейнере и разрешаются из него.
    /// </summary>
    private static ComposedPipeline CreateWithHost(SalesAnalyticsOptions serviceOptions)
    {
        var builder = Host.CreateApplicationBuilder();

        // Отчёт печатается в тот же поток, что и логи хоста, поэтому
        // информационные сообщения инфраструктуры приглушаются.
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddSalesAnalytics(serviceOptions);

        var host = builder.Build();

        return new ComposedPipeline(host.Services.GetRequiredService<AnalyticsPipeline>(), host);
    }

    /// <summary>
    /// Режимы <c>console</c>, <c>file</c>, <c>async</c> и <c>parallel</c>: зависимости
    /// собираются вручную, без контейнера.
    /// </summary>
    private static ComposedPipeline CreateManually(SalesAnalyticsOptions serviceOptions)
    {
        var csvReader = new CsvReader(new SaleParser(), serviceOptions.CsvReader);
        var saleLoader = new FileSaleLoader(csvReader);

        AnalyticsServiceBase analyticsService = serviceOptions.UseParallelAnalytics
            ? new ParallelAnalyticsService()
            : new LinqAnalyticsService();

        var writerFactory = new ResultWriterFactory(serviceOptions.ConsoleOutput, serviceOptions.ConsoleReport);

        return new ComposedPipeline(new AnalyticsPipeline(saleLoader, analyticsService, writerFactory), Scope: null);
    }
}
