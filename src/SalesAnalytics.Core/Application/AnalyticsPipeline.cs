using SalesAnalytics.Core.Abstractions;
using SalesAnalytics.Core.Models;

namespace SalesAnalytics.Core.Application;

/// <summary>
/// Параметры одного запуска обработки.
/// </summary>
public sealed record PipelineRequest
{
    /// <summary>Путь к входному CSV-файлу.</summary>
    public required string InputPath { get; init; }

    /// <summary>Название режима — попадает в метаданные отчёта.</summary>
    public required string Mode { get; init; }

    /// <summary>Путь к JSON-файлу результата или <see langword="null"/> для вывода в консоль.</summary>
    public string? OutputPath { get; init; }

    /// <summary>Период, за который считается статистика.</summary>
    public AnalyticsPeriod Period { get; init; } = AnalyticsPeriod.All;

    /// <summary>
    /// Выполнять ли ввод-вывод асинхронно. Флаг, а не отдельная реализация конвейера,
    /// потому что порядок шагов в обоих случаях одинаков.
    /// </summary>
    public bool UseAsyncIo { get; init; }
}

/// <summary>
/// Связывает три шага обработки: загрузку продаж, расчёт аналитик и запись результата.
/// Не знает ни о командной строке, ни о конкретных реализациях сервисов —
/// поведение целиком определяется тем, что подставлено через зависимости.
/// </summary>
/// <param name="saleLoader">Загрузчик продаж.</param>
/// <param name="analyticsService">Служба расчёта аналитик.</param>
/// <param name="resultWriterFactory">Фабрика писателей результата.</param>
public sealed class AnalyticsPipeline(
    ISaleLoader saleLoader,
    IAnalyticsService analyticsService,
    IResultWriterFactory resultWriterFactory)
{
    private readonly ISaleLoader _saleLoader = saleLoader ?? throw new ArgumentNullException(nameof(saleLoader));
    private readonly IAnalyticsService _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
    private readonly IResultWriterFactory _resultWriterFactory = resultWriterFactory ?? throw new ArgumentNullException(nameof(resultWriterFactory));

    /// <summary>
    /// Выполняет полный цикл: чтение → расчёт → вывод.
    /// </summary>
    /// <param name="request">Параметры запуска.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сформированный отчёт — он же уже выведен в целевой приёмник.</returns>
    public async Task<AnalyticsReport> ExecuteAsync(PipelineRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Каталог результата проверяется до чтения файла и расчёта:
        // бессмысленно тратить время на обработку, если сохранить отчёт всё равно некуда.
        var writer = _resultWriterFactory.Create(request.OutputPath);

        var sales = request.UseAsyncIo
            ? await _saleLoader.LoadAsync(request.InputPath, cancellationToken).ConfigureAwait(false)
            : _saleLoader.Load(request.InputPath);

        var analyticsRequest = new AnalyticsRequest(request.InputPath, request.Mode, request.Period);

        var report = request.UseAsyncIo
            ? await _analyticsService.AnalyzeAsync(sales, analyticsRequest, cancellationToken).ConfigureAwait(false)
            : _analyticsService.Analyze(sales, analyticsRequest);

        if (request.UseAsyncIo)
        {
            await writer.WriteAsync(report, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            writer.Write(report);
        }

        return report;
    }
}
