using SalesAnalytics.Core.Abstractions;
using SalesAnalytics.Core.Exceptions;
using SalesAnalytics.Core.Models;

namespace SalesAnalytics.Core.Analytics;

/// <summary>
/// Общий каркас расчёта отчёта: фильтрация по периоду, сборка метаданных
/// и порядок вызова аналитических блоков. Наследники переопределяют только то,
/// как считается конкретный блок, — за счёт этого последовательная и параллельная
/// реализации гарантированно дают отчёт одинаковой структуры.
/// </summary>
public abstract class AnalyticsServiceBase : IAnalyticsService
{
    /// <summary>Знаков после запятой для денежных сумм и средних значений.</summary>
    protected const int MoneyPrecision = 2;

    /// <summary>Знаков после запятой для скидок: 0.2833 информативнее, чем 0.28.</summary>
    protected const int DiscountPrecision = 4;

    /// <inheritdoc />
    public AnalyticsReport Analyze(IReadOnlyList<Sale> sales, AnalyticsRequest request)
    {
        ArgumentNullException.ThrowIfNull(sales);
        ArgumentNullException.ThrowIfNull(request);

        var analyzed = ApplyPeriodFilter(sales, request.Period);

        if (analyzed.Count == 0)
        {
            throw new EmptyInputException(
                $"За период {DescribePeriod(request.Period)} не найдено ни одной записи. " +
                "Проверьте значения --start_date и --end_date.");
        }

        var metadata = new ReportMetadata(
            SourceFile: request.SourceFile,
            Mode: request.Mode,
            TotalRecords: sales.Count,
            AnalyzedRecords: analyzed.Count,
            StartDate: request.Period.StartDate,
            EndDate: request.Period.EndDate,
            GeneratedAtUtc: DateTimeOffset.UtcNow);

        return new AnalyticsReport(
            metadata,
            CalculateSalesByCategory(analyzed),
            CalculateTopCategoriesByQuantity(analyzed, request.TopCategoriesCount),
            CalculateAveragePriceByMonth(analyzed),
            CalculateTopCustomersByRating(analyzed, request.TopCustomersCount),
            CalculateDeliveryStatistics(analyzed),
            CalculateAverageDiscountByCategoryAndMonth(analyzed));
    }

    /// <inheritdoc />
    public Task<AnalyticsReport> AnalyzeAsync(
        IReadOnlyList<Sale> sales,
        AnalyticsRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Расчёт — работа процессорная, а не ввод-вывод, поэтому единственный честный
        // способ сделать метод асинхронным — вынести его в пул потоков.
        return Task.Run(() => Analyze(sales, request), cancellationToken);
    }

    /// <summary>1. Общая сумма продаж по категориям.</summary>
    protected abstract IReadOnlyList<CategorySales> CalculateSalesByCategory(IReadOnlyList<Sale> sales);

    /// <summary>2. Топ категорий по количеству проданных единиц.</summary>
    protected abstract IReadOnlyList<CategoryQuantity> CalculateTopCategoriesByQuantity(IReadOnlyList<Sale> sales, int take);

    /// <summary>3. Средняя цена за единицу по месяцам.</summary>
    protected abstract IReadOnlyList<MonthlyAveragePrice> CalculateAveragePriceByMonth(IReadOnlyList<Sale> sales);

    /// <summary>4. Топ покупателей по средней оценке.</summary>
    protected abstract IReadOnlyList<CustomerRating> CalculateTopCustomersByRating(IReadOnlyList<Sale> sales, int take);

    /// <summary>5. Статистика по срокам доставки.</summary>
    protected abstract DeliveryStatistics CalculateDeliveryStatistics(IReadOnlyList<Sale> sales);

    /// <summary>6. Средняя скидка по категории с группировкой по месяцам.</summary>
    protected abstract IReadOnlyList<CategoryMonthlyDiscount> CalculateAverageDiscountByCategoryAndMonth(IReadOnlyList<Sale> sales);

    /// <summary>
    /// Оставляет только записи, попадающие в период <c>[start; end)</c>.
    /// Если границы не заданы, исходная коллекция возвращается без копирования.
    /// </summary>
    protected static IReadOnlyList<Sale> ApplyPeriodFilter(IReadOnlyList<Sale> sales, AnalyticsPeriod period) =>
        period.HasBounds
            ? sales.Where(sale => period.Contains(sale.OrderDate)).ToList()
            : sales;

    /// <summary>Округляет денежные величины до <see cref="MoneyPrecision"/> знаков.</summary>
    protected static decimal RoundMoney(decimal value) =>
        Math.Round(value, MoneyPrecision, MidpointRounding.AwayFromZero);

    /// <summary>Округляет скидку до <see cref="DiscountPrecision"/> знаков.</summary>
    protected static decimal RoundDiscount(decimal value) =>
        Math.Round(value, DiscountPrecision, MidpointRounding.AwayFromZero);

    private static string DescribePeriod(AnalyticsPeriod period) =>
        (period.StartDate, period.EndDate) switch
        {
            (null, null) => "«весь файл»",
            ({ } start, null) => $"с {start:yyyy-MM-dd}",
            (null, { } end) => $"до {end:yyyy-MM-dd} (не включая)",
            ({ } start, { } end) => $"с {start:yyyy-MM-dd} по {end:yyyy-MM-dd} (не включая)",
        };
}
