namespace SalesAnalytics.Core.Models;

/// <summary>
/// Служебная информация о выполненном расчёте: попадает в JSON и в шапку консольного отчёта.
/// </summary>
/// <param name="SourceFile">Путь к обработанному CSV-файлу.</param>
/// <param name="Mode">Режим работы приложения, в котором получен отчёт.</param>
/// <param name="TotalRecords">Сколько записей прочитано из файла.</param>
/// <param name="AnalyzedRecords">Сколько записей осталось после фильтрации по датам.</param>
/// <param name="StartDate">Нижняя граница периода включительно, если задана.</param>
/// <param name="EndDate">Верхняя граница периода НЕ включительно, если задана.</param>
/// <param name="GeneratedAtUtc">Момент формирования отчёта в UTC.</param>
public sealed record ReportMetadata(
    string SourceFile,
    string Mode,
    int TotalRecords,
    int AnalyzedRecords,
    DateOnly? StartDate,
    DateOnly? EndDate,
    DateTimeOffset GeneratedAtUtc);

/// <summary>
/// Итоговый отчёт со всеми аналитическими блоками из технического задания.
/// </summary>
/// <param name="Metadata">Служебная информация о расчёте.</param>
/// <param name="SalesByCategory">1. Общая сумма продаж по категориям.</param>
/// <param name="TopCategoriesByQuantity">2. Топ-4 категорий по количеству проданных единиц.</param>
/// <param name="AveragePriceByMonth">3. Средняя цена за каждый месяц.</param>
/// <param name="TopCustomersByRating">4. Топ-5 покупателей по рейтингу.</param>
/// <param name="Delivery">5. Среднее время доставки.</param>
/// <param name="AverageDiscountByCategoryAndMonth">6. Средняя скидка по категории с группировкой по месяцу.</param>
public sealed record AnalyticsReport(
    ReportMetadata Metadata,
    IReadOnlyList<CategorySales> SalesByCategory,
    IReadOnlyList<CategoryQuantity> TopCategoriesByQuantity,
    IReadOnlyList<MonthlyAveragePrice> AveragePriceByMonth,
    IReadOnlyList<CustomerRating> TopCustomersByRating,
    DeliveryStatistics Delivery,
    IReadOnlyList<CategoryMonthlyDiscount> AverageDiscountByCategoryAndMonth);
