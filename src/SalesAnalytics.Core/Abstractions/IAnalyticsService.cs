using SalesAnalytics.Core.Models;

namespace SalesAnalytics.Core.Abstractions;

/// <summary>
/// Считает аналитические показатели по набору продаж.
/// Реализации отличаются только способом вычисления (последовательно или параллельно),
/// поэтому режим работы приложения меняет лишь зарегистрированную реализацию.
/// </summary>
public interface IAnalyticsService
{
    /// <summary>
    /// Считает отчёт синхронно.
    /// </summary>
    /// <param name="sales">Исходные продажи.</param>
    /// <param name="request">Параметры расчёта: период, размеры топов, метаданные.</param>
    /// <exception cref="Exceptions.EmptyInputException">После фильтрации по периоду не осталось записей.</exception>
    AnalyticsReport Analyze(IReadOnlyList<Sale> sales, AnalyticsRequest request);

    /// <summary>
    /// Считает отчёт асинхронно: вычисления выносятся в пул потоков,
    /// чтобы вызывающий код мог не блокироваться на счёте по большому файлу.
    /// </summary>
    /// <param name="sales">Исходные продажи.</param>
    /// <param name="request">Параметры расчёта.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <exception cref="Exceptions.EmptyInputException">После фильтрации по периоду не осталось записей.</exception>
    Task<AnalyticsReport> AnalyzeAsync(
        IReadOnlyList<Sale> sales,
        AnalyticsRequest request,
        CancellationToken cancellationToken = default);
}
