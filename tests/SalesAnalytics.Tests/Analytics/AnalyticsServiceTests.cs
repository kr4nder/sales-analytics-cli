using SalesAnalytics.Core.Abstractions;
using SalesAnalytics.Core.Analytics;
using SalesAnalytics.Core.Exceptions;
using SalesAnalytics.Core.Models;

namespace SalesAnalytics.Tests.Analytics;

/// <summary>
/// Тесты расчёта аналитик. Каждый тест выполняется для обеих реализаций:
/// последовательной и параллельной — они обязаны давать одинаковый результат.
/// Ожидаемые значения совпадают с расчётом из примера в техническом задании.
/// </summary>
public sealed class AnalyticsServiceTests
{
    /// <summary>Обе реализации <see cref="IAnalyticsService"/> для параметризованных тестов.</summary>
    public static TheoryData<IAnalyticsService> Implementations =>
    [
        new LinqAnalyticsService(),
        new ParallelAnalyticsService(),
    ];

    [Theory]
    [MemberData(nameof(Implementations))]
    public void SalesByCategory_MatchesAssignmentExample(IAnalyticsService service)
    {
        var report = service.Analyze(TestData.SampleSales, TestData.Request());

        var byCategory = report.SalesByCategory.ToDictionary(item => item.Category);

        // Значения из раздела «Ожидаемый результат» задания:
        // Beauty 373.65*7 + 311.28*3, Clothing 47.74*7 + 139.87*5, Electronics 524.47*5.
        Assert.Equal(3549.39m, byCategory["Beauty"].GrossAmount);
        Assert.Equal(1033.53m, byCategory["Clothing"].GrossAmount);
        Assert.Equal(2622.35m, byCategory["Electronics"].GrossAmount);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void SalesByCategory_IsSortedByGrossAmountDescending(IAnalyticsService service)
    {
        var report = service.Analyze(TestData.SampleSales, TestData.Request());

        Assert.Equal(["Beauty", "Electronics", "Clothing"], report.SalesByCategory.Select(item => item.Category));
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void SalesByCategory_SumsNetRevenueAndOrderCount(IAnalyticsService service)
    {
        var report = service.Analyze(TestData.SampleSales, TestData.Request());

        var beauty = report.SalesByCategory.Single(item => item.Category == "Beauty");

        Assert.Equal(1883.2m + 644.35m, beauty.NetRevenue);
        Assert.Equal(2, beauty.OrderCount);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void TopCategoriesByQuantity_RanksByTotalUnits(IAnalyticsService service)
    {
        var report = service.Analyze(TestData.SampleSales, TestData.Request());

        // Clothing 7+5 = 12, Beauty 7+3 = 10, Electronics 5.
        Assert.Equal(
            [("Clothing", 12), ("Beauty", 10), ("Electronics", 5)],
            report.TopCategoriesByQuantity.Select(item => (item.Category, item.TotalQuantity)));
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void TopCategoriesByQuantity_RespectsRequestedLimit(IAnalyticsService service)
    {
        var request = TestData.Request() with { TopCategoriesCount = 2 };

        var report = service.Analyze(TestData.SampleSales, request);

        Assert.Equal(2, report.TopCategoriesByQuantity.Count);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void AveragePriceByMonth_MatchesAssignmentExample(IAnalyticsService service)
    {
        var report = service.Analyze(TestData.SampleSales, TestData.Request());

        var january = Assert.Single(report.AveragePriceByMonth);

        // (373.65 + 47.74 + 311.28 + 524.47 + 139.87) / 5 = 279.40 — значение из задания.
        Assert.Equal(new DateOnly(2022, 1, 1), january.Month);
        Assert.Equal(279.40m, january.AverageUnitPrice);
        Assert.Equal(5, january.OrderCount);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void AveragePriceByMonth_GroupsByCalendarMonthAndSortsChronologically(IAnalyticsService service)
    {
        IReadOnlyList<Sale> sales =
        [
            TestData.Sale(orderDate: "2022-03-05", unitPrice: 100m),
            TestData.Sale(orderDate: "2022-03-28", unitPrice: 300m),
            TestData.Sale(orderDate: "2022-01-15", unitPrice: 50m),
        ];

        var report = service.Analyze(sales, TestData.Request());

        Assert.Equal(
            [(new DateOnly(2022, 1, 1), 50m), (new DateOnly(2022, 3, 1), 200m)],
            report.AveragePriceByMonth.Select(item => (item.Month, item.AverageUnitPrice)));
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void TopCustomersByRating_RanksCustomersByAverageRating(IAnalyticsService service)
    {
        var report = service.Analyze(TestData.SampleSales, TestData.Request());

        Assert.Equal(
            [1106, 1102, 1435, 1860, 1270],
            report.TopCustomersByRating.Select(item => item.CustomerId));
        Assert.Equal(4.9m, report.TopCustomersByRating[0].AverageRating);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void TopCustomersByRating_AveragesRepeatedOrdersOfSameCustomer(IAnalyticsService service)
    {
        IReadOnlyList<Sale> sales =
        [
            TestData.Sale(customerId: 1, rating: 5m),
            TestData.Sale(customerId: 1, rating: 3m),
            TestData.Sale(customerId: 2, rating: 4.5m),
        ];

        var report = service.Analyze(sales, TestData.Request());

        var first = report.TopCustomersByRating[0];
        Assert.Equal(2, first.CustomerId);
        Assert.Equal(4.5m, first.AverageRating);

        var second = report.TopCustomersByRating[1];
        Assert.Equal(1, second.CustomerId);
        Assert.Equal(4m, second.AverageRating);
        Assert.Equal(2, second.OrderCount);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void TopCustomersByRating_RespectsRequestedLimit(IAnalyticsService service)
    {
        var request = TestData.Request() with { TopCustomersCount = 3 };

        var report = service.Analyze(TestData.SampleSales, request);

        Assert.Equal(3, report.TopCustomersByRating.Count);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void Delivery_ComputesAverageMinAndMax(IAnalyticsService service)
    {
        var report = service.Analyze(TestData.SampleSales, TestData.Request());

        // (10 + 6 + 6 + 6 + 4) / 5 = 6.4
        Assert.Equal(6.4m, report.Delivery.AverageDeliveryDays);
        Assert.Equal(4, report.Delivery.MinDeliveryDays);
        Assert.Equal(10, report.Delivery.MaxDeliveryDays);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void AverageDiscountByCategoryAndMonth_GroupsByBothKeys(IAnalyticsService service)
    {
        var report = service.Analyze(TestData.SampleSales, TestData.Request());

        var discounts = report.AverageDiscountByCategoryAndMonth
            .ToDictionary(item => (item.Category, item.Month), item => item.AverageDiscount);

        var january = new DateOnly(2022, 1, 1);
        Assert.Equal(0.295m, discounts[("Beauty", january)]);
        Assert.Equal(0.21m, discounts[("Clothing", january)]);
        Assert.Equal(0.02m, discounts[("Electronics", january)]);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void AverageDiscountByCategoryAndMonth_SeparatesMonthsWithinCategory(IAnalyticsService service)
    {
        IReadOnlyList<Sale> sales =
        [
            TestData.Sale(category: "Home", orderDate: "2022-01-10", discount: 0.2m),
            TestData.Sale(category: "Home", orderDate: "2022-02-10", discount: 0.4m),
        ];

        var report = service.Analyze(sales, TestData.Request());

        Assert.Equal(2, report.AverageDiscountByCategoryAndMonth.Count);
        Assert.Equal(0.2m, report.AverageDiscountByCategoryAndMonth[0].AverageDiscount);
        Assert.Equal(0.4m, report.AverageDiscountByCategoryAndMonth[1].AverageDiscount);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void Analyze_PeriodFilter_ExcludesEndDate(IAnalyticsService service)
    {
        // Записи датированы 1–5 января; период [2022-01-02; 2022-01-04) оставляет 2-е и 3-е.
        var period = new AnalyticsPeriod(new DateOnly(2022, 1, 2), new DateOnly(2022, 1, 4));

        var report = service.Analyze(TestData.SampleSales, TestData.Request(period));

        Assert.Equal(5, report.Metadata.TotalRecords);
        Assert.Equal(2, report.Metadata.AnalyzedRecords);

        // Остались заказы 10002 (Clothing) и 10003 (Beauty), по одному в каждой категории.
        Assert.Equal(
            [("Beauty", 1), ("Clothing", 1)],
            report.SalesByCategory
                .OrderBy(item => item.Category, StringComparer.Ordinal)
                .Select(item => (item.Category, item.OrderCount)));
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void Analyze_PeriodWithoutMatchingRecords_Throws(IAnalyticsService service)
    {
        var period = new AnalyticsPeriod(new DateOnly(2030, 1, 1), new DateOnly(2030, 2, 1));

        var exception = Assert.Throws<EmptyInputException>(
            () => service.Analyze(TestData.SampleSales, TestData.Request(period)));

        Assert.Contains("2030-01-01", exception.Message);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void Analyze_FillsMetadata(IAnalyticsService service)
    {
        var report = service.Analyze(TestData.SampleSales, TestData.Request());

        Assert.Equal("sample.csv", report.Metadata.SourceFile);
        Assert.Equal("test", report.Metadata.Mode);
        Assert.Equal(5, report.Metadata.TotalRecords);
        Assert.Equal(5, report.Metadata.AnalyzedRecords);
        Assert.Null(report.Metadata.StartDate);
        Assert.Null(report.Metadata.EndDate);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public async Task AnalyzeAsync_ProducesSameResultAsAnalyze(IAnalyticsService service)
    {
        var expected = service.Analyze(TestData.SampleSales, TestData.Request());

        var actual = await service.AnalyzeAsync(TestData.SampleSales, TestData.Request());

        Assert.Equal(expected.SalesByCategory, actual.SalesByCategory);
        Assert.Equal(expected.TopCategoriesByQuantity, actual.TopCategoriesByQuantity);
        Assert.Equal(expected.AveragePriceByMonth, actual.AveragePriceByMonth);
        Assert.Equal(expected.TopCustomersByRating, actual.TopCustomersByRating);
        Assert.Equal(expected.Delivery, actual.Delivery);
        Assert.Equal(expected.AverageDiscountByCategoryAndMonth, actual.AverageDiscountByCategoryAndMonth);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public async Task AnalyzeAsync_CancelledToken_Throws(IAnalyticsService service)
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.AnalyzeAsync(TestData.SampleSales, TestData.Request(), cancellation.Token));
    }

    /// <summary>
    /// Главная проверка потокобезопасности: на объёме, превышающем порог включения
    /// параллелизма, параллельная реализация обязана совпасть с последовательной.
    /// </summary>
    [Fact]
    public void ParallelService_OnLargeDataset_MatchesSequentialService()
    {
        var sales = TestData.GenerateSales(5_000);

        var sequential = new LinqAnalyticsService().Analyze(sales, TestData.Request());
        var parallel = new ParallelAnalyticsService().Analyze(sales, TestData.Request());

        Assert.Equal(sequential.SalesByCategory, parallel.SalesByCategory);
        Assert.Equal(sequential.TopCategoriesByQuantity, parallel.TopCategoriesByQuantity);
        Assert.Equal(sequential.AveragePriceByMonth, parallel.AveragePriceByMonth);
        Assert.Equal(sequential.TopCustomersByRating, parallel.TopCustomersByRating);
        Assert.Equal(sequential.Delivery, parallel.Delivery);
        Assert.Equal(sequential.AverageDiscountByCategoryAndMonth, parallel.AverageDiscountByCategoryAndMonth);
    }

    [Theory]
    [MemberData(nameof(Implementations))]
    public void Analyze_NullArguments_Throw(IAnalyticsService service)
    {
        Assert.Throws<ArgumentNullException>(() => service.Analyze(null!, TestData.Request()));
        Assert.Throws<ArgumentNullException>(() => service.Analyze(TestData.SampleSales, null!));
    }
}
