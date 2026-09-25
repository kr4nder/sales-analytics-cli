using System.Collections.Concurrent;
using SalesAnalytics.Core.Models;

namespace SalesAnalytics.Core.Analytics;

/// <summary>
/// Параллельная реализация аналитики для режимов <c>parallel</c> и <c>full</c>.
/// Суммы по категориям считаются через <see cref="Parallel"/>.ForEach с локальными
/// накопителями потоков, статистика доставки сводится через <see cref="Interlocked"/>,
/// остальные блоки — через PLINQ.
/// </summary>
public sealed class ParallelAnalyticsService : AnalyticsServiceBase
{
    /// <summary>
    /// Ниже этого числа записей параллелизм только вредит: накладные расходы на
    /// разбиение и синхронизацию превышают выигрыш, поэтому считаем в одном потоке.
    /// </summary>
    private const int ParallelThreshold = 512;

    /// <inheritdoc />
    /// <remarks>
    /// Записи разбиваются на диапазоны, каждый поток копит промежуточный итог в обычном
    /// словаре без блокировок, и только готовые частичные суммы сливаются в общий
    /// <see cref="ConcurrentDictionary{TKey, TValue}"/>. Такая схема сводит синхронизацию
    /// к одной операции на диапазон вместо одной на запись.
    /// </remarks>
    protected override IReadOnlyList<CategorySales> CalculateSalesByCategory(IReadOnlyList<Sale> sales)
    {
        var totals = new ConcurrentDictionary<string, CategoryAccumulator>(StringComparer.Ordinal);

        Parallel.ForEach(
            Partitioner.Create(0, sales.Count),
            () => new Dictionary<string, CategoryAccumulator>(StringComparer.Ordinal),
            (range, _, local) =>
            {
                for (var index = range.Item1; index < range.Item2; index++)
                {
                    var sale = sales[index];

                    if (!local.TryGetValue(sale.ProductCategory, out var accumulator))
                    {
                        accumulator = new CategoryAccumulator();
                        local.Add(sale.ProductCategory, accumulator);
                    }

                    accumulator.Add(sale);
                }

                return local;
            },
            local =>
            {
                foreach (var (category, partial) in local)
                {
                    var shared = totals.GetOrAdd(category, _ => new CategoryAccumulator());

                    // ConcurrentDictionary защищает набор ключей, но не содержимое
                    // накопителя, поэтому слияние делается под блокировкой самого элемента.
                    lock (shared)
                    {
                        shared.Merge(partial);
                    }
                }
            });

        return totals
            .Select(pair => new CategorySales(
                Category: pair.Key,
                GrossAmount: RoundMoney(pair.Value.GrossAmount),
                NetRevenue: RoundMoney(pair.Value.NetRevenue),
                OrderCount: pair.Value.OrderCount))
            .OrderByDescending(item => item.GrossAmount)
            .ThenBy(item => item.Category, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    protected override IReadOnlyList<CategoryQuantity> CalculateTopCategoriesByQuantity(IReadOnlyList<Sale> sales, int take) =>
        AsParallelIfLarge(sales)
            .GroupBy(sale => sale.ProductCategory)
            .Select(group => new CategoryQuantity(
                Category: group.Key,
                TotalQuantity: group.Sum(sale => sale.Quantity)))
            // Упорядочивание и отбор топа выполняются уже над несколькими агрегатами,
            // поэтому возвращаемся в последовательный LINQ.
            .AsSequential()
            .OrderByDescending(item => item.TotalQuantity)
            .ThenBy(item => item.Category, StringComparer.Ordinal)
            .Take(take)
            .ToList();

    /// <inheritdoc />
    protected override IReadOnlyList<MonthlyAveragePrice> CalculateAveragePriceByMonth(IReadOnlyList<Sale> sales) =>
        AsParallelIfLarge(sales)
            .GroupBy(sale => sale.MonthKey)
            .Select(group => new MonthlyAveragePrice(
                Month: group.Key,
                AverageUnitPrice: RoundMoney(group.Average(sale => sale.UnitPrice)),
                OrderCount: group.Count()))
            .AsSequential()
            .OrderBy(item => item.Month)
            .ToList();

    /// <inheritdoc />
    protected override IReadOnlyList<CustomerRating> CalculateTopCustomersByRating(IReadOnlyList<Sale> sales, int take) =>
        AsParallelIfLarge(sales)
            .GroupBy(sale => sale.CustomerId)
            .Select(group => new CustomerRating(
                CustomerId: group.Key,
                AverageRating: RoundMoney(group.Average(sale => sale.CustomerRating)),
                OrderCount: group.Count()))
            .AsSequential()
            .OrderByDescending(item => item.AverageRating)
            .ThenByDescending(item => item.OrderCount)
            .ThenBy(item => item.CustomerId)
            .Take(take)
            .ToList();

    /// <inheritdoc />
    /// <remarks>
    /// Сумма, минимум и максимум сводятся атомарно: сумма — через <see cref="Interlocked.Add(ref long, long)"/>,
    /// границы — через цикл сравнения с обменом.
    /// </remarks>
    protected override DeliveryStatistics CalculateDeliveryStatistics(IReadOnlyList<Sale> sales)
    {
        var shared = new DeliveryAccumulator();

        Parallel.ForEach(
            Partitioner.Create(0, sales.Count),
            () => new DeliveryAccumulator(),
            (range, _, local) =>
            {
                for (var index = range.Item1; index < range.Item2; index++)
                {
                    local.Add(sales[index].DeliveryDays);
                }

                return local;
            },
            local =>
            {
                Interlocked.Add(ref shared.TotalDays, local.TotalDays);
                Interlocked.Add(ref shared.Count, local.Count);
                AtomicMin(ref shared.MinDays, local.MinDays);
                AtomicMax(ref shared.MaxDays, local.MaxDays);
            });

        return new DeliveryStatistics(
            AverageDeliveryDays: RoundMoney((decimal)shared.TotalDays / shared.Count),
            MinDeliveryDays: shared.MinDays,
            MaxDeliveryDays: shared.MaxDays);
    }

    /// <inheritdoc />
    protected override IReadOnlyList<CategoryMonthlyDiscount> CalculateAverageDiscountByCategoryAndMonth(IReadOnlyList<Sale> sales) =>
        AsParallelIfLarge(sales)
            .GroupBy(sale => (sale.ProductCategory, sale.MonthKey))
            .Select(group => new CategoryMonthlyDiscount(
                Category: group.Key.ProductCategory,
                Month: group.Key.MonthKey,
                AverageDiscount: RoundDiscount(group.Average(sale => sale.Discount)),
                OrderCount: group.Count()))
            .AsSequential()
            .OrderBy(item => item.Category, StringComparer.Ordinal)
            .ThenBy(item => item.Month)
            .ToList();

    /// <summary>
    /// Включает PLINQ только на выборках, где распараллеливание оправдано.
    /// </summary>
    private static ParallelQuery<Sale> AsParallelIfLarge(IReadOnlyList<Sale> sales) =>
        sales.Count >= ParallelThreshold
            ? sales.AsParallel()
            : sales.AsParallel().WithDegreeOfParallelism(1);

    /// <summary>Атомарно уменьшает <paramref name="target"/> до <paramref name="value"/>, если оно меньше.</summary>
    private static void AtomicMin(ref int target, int value)
    {
        int current;
        while (value < (current = Volatile.Read(ref target)))
        {
            if (Interlocked.CompareExchange(ref target, value, current) == current)
            {
                return;
            }
        }
    }

    /// <summary>Атомарно увеличивает <paramref name="target"/> до <paramref name="value"/>, если оно больше.</summary>
    private static void AtomicMax(ref int target, int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref target)))
        {
            if (Interlocked.CompareExchange(ref target, value, current) == current)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Промежуточный итог по одной категории. Класс, а не структура, чтобы
    /// экземпляр в словаре можно было изменять на месте.
    /// </summary>
    private sealed class CategoryAccumulator
    {
        public decimal GrossAmount { get; private set; }

        public decimal NetRevenue { get; private set; }

        public int OrderCount { get; private set; }

        public void Add(Sale sale)
        {
            GrossAmount += sale.GrossAmount;
            NetRevenue += sale.NetRevenue;
            OrderCount++;
        }

        public void Merge(CategoryAccumulator other)
        {
            GrossAmount += other.GrossAmount;
            NetRevenue += other.NetRevenue;
            OrderCount += other.OrderCount;
        }
    }

    /// <summary>
    /// Промежуточный итог по срокам доставки. Поля открыты, потому что
    /// <see cref="Interlocked"/> работает только со ссылками на поля.
    /// </summary>
    private sealed class DeliveryAccumulator
    {
        public long TotalDays;

        public long Count;

        public int MinDays = int.MaxValue;

        public int MaxDays = int.MinValue;

        public void Add(int deliveryDays)
        {
            TotalDays += deliveryDays;
            Count++;
            MinDays = Math.Min(MinDays, deliveryDays);
            MaxDays = Math.Max(MaxDays, deliveryDays);
        }
    }
}
