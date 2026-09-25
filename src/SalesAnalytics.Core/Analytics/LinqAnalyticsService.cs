using SalesAnalytics.Core.Models;

namespace SalesAnalytics.Core.Analytics;

/// <summary>
/// Последовательная реализация аналитики на LINQ-to-Objects.
/// Используется в режимах <c>console</c>, <c>file</c>, <c>async</c> и <c>di</c>.
/// </summary>
public sealed class LinqAnalyticsService : AnalyticsServiceBase
{
    /// <inheritdoc />
    protected override IReadOnlyList<CategorySales> CalculateSalesByCategory(IReadOnlyList<Sale> sales) =>
        sales
            .GroupBy(sale => sale.ProductCategory)
            .Select(group => new CategorySales(
                Category: group.Key,
                GrossAmount: RoundMoney(group.Sum(sale => sale.GrossAmount)),
                NetRevenue: RoundMoney(group.Sum(sale => sale.NetRevenue)),
                OrderCount: group.Count()))
            .OrderByDescending(item => item.GrossAmount)
            .ThenBy(item => item.Category, StringComparer.Ordinal)
            .ToList();

    /// <inheritdoc />
    protected override IReadOnlyList<CategoryQuantity> CalculateTopCategoriesByQuantity(IReadOnlyList<Sale> sales, int take) =>
        sales
            .GroupBy(sale => sale.ProductCategory)
            .Select(group => new CategoryQuantity(
                Category: group.Key,
                TotalQuantity: group.Sum(sale => sale.Quantity)))
            .OrderByDescending(item => item.TotalQuantity)
            .ThenBy(item => item.Category, StringComparer.Ordinal)
            .Take(take)
            .ToList();

    /// <inheritdoc />
    protected override IReadOnlyList<MonthlyAveragePrice> CalculateAveragePriceByMonth(IReadOnlyList<Sale> sales) =>
        sales
            .GroupBy(sale => sale.MonthKey)
            .Select(group => new MonthlyAveragePrice(
                Month: group.Key,
                AverageUnitPrice: RoundMoney(group.Average(sale => sale.UnitPrice)),
                OrderCount: group.Count()))
            .OrderBy(item => item.Month)
            .ToList();

    /// <inheritdoc />
    protected override IReadOnlyList<CustomerRating> CalculateTopCustomersByRating(IReadOnlyList<Sale> sales, int take) =>
        sales
            .GroupBy(sale => sale.CustomerId)
            .Select(group => new CustomerRating(
                CustomerId: group.Key,
                AverageRating: RoundMoney(group.Average(sale => sale.CustomerRating)),
                OrderCount: group.Count()))
            // При равном рейтинге выше тот, кто сделал больше заказов: такая оценка устойчивее.
            .OrderByDescending(item => item.AverageRating)
            .ThenByDescending(item => item.OrderCount)
            .ThenBy(item => item.CustomerId)
            .Take(take)
            .ToList();

    /// <inheritdoc />
    protected override DeliveryStatistics CalculateDeliveryStatistics(IReadOnlyList<Sale> sales) =>
        new(
            AverageDeliveryDays: RoundMoney((decimal)sales.Average(sale => sale.DeliveryDays)),
            MinDeliveryDays: sales.Min(sale => sale.DeliveryDays),
            MaxDeliveryDays: sales.Max(sale => sale.DeliveryDays));

    /// <inheritdoc />
    protected override IReadOnlyList<CategoryMonthlyDiscount> CalculateAverageDiscountByCategoryAndMonth(IReadOnlyList<Sale> sales) =>
        sales
            .GroupBy(sale => (sale.ProductCategory, sale.MonthKey))
            .Select(group => new CategoryMonthlyDiscount(
                Category: group.Key.ProductCategory,
                Month: group.Key.MonthKey,
                AverageDiscount: RoundDiscount(group.Average(sale => sale.Discount)),
                OrderCount: group.Count()))
            .OrderBy(item => item.Category, StringComparer.Ordinal)
            .ThenBy(item => item.Month)
            .ToList();
}
