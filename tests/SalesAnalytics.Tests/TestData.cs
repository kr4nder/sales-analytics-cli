using SalesAnalytics.Core.Models;

namespace SalesAnalytics.Tests;

/// <summary>
/// Общие данные для тестов.
/// Взяты из примера в техническом задании, поэтому ожидаемые значения
/// в тестах аналитики совпадают с приведённым там расчётом.
/// </summary>
internal static class TestData
{
    /// <summary>Строка заголовка CSV.</summary>
    public const string Header =
        "order_id,order_date,customer_id,product_category,region,quantity,unit_price,discount," +
        "payment_method,delivery_days,customer_rating,revenue";

    /// <summary>Пять строк данных из примера в задании.</summary>
    public static readonly string[] SampleRows =
    [
        "10001,1/1/2022,1102,Beauty,South,7,373.65,0.28,Wallet,10,4.7,1883.2",
        "10002,1/2/2022,1435,Clothing,South,7,47.74,0.09,Card,6,3.9,304.1",
        "10003,1/3/2022,1860,Beauty,East,3,311.28,0.31,COD,6,2.5,644.35",
        "10004,1/4/2022,1270,Electronics,West,5,524.47,0.02,Wallet,6,1.6,2569.9",
        "10005,1/5/2022,1106,Clothing,West,5,139.87,0.33,Wallet,4,4.9,468.56",
    ];

    /// <summary>Полный CSV-файл из примера в виде строки.</summary>
    public static string SampleCsv => string.Join(Environment.NewLine, [Header, .. SampleRows]);

    /// <summary>Те же пять записей уже в виде объектов.</summary>
    public static IReadOnlyList<Sale> SampleSales =>
    [
        Sale(10001, "2022-01-01", 1102, "Beauty", 7, 373.65m, 0.28m, 10, 4.7m, 1883.2m),
        Sale(10002, "2022-01-02", 1435, "Clothing", 7, 47.74m, 0.09m, 6, 3.9m, 304.1m),
        Sale(10003, "2022-01-03", 1860, "Beauty", 3, 311.28m, 0.31m, 6, 2.5m, 644.35m),
        Sale(10004, "2022-01-04", 1270, "Electronics", 5, 524.47m, 0.02m, 6, 1.6m, 2569.9m),
        Sale(10005, "2022-01-05", 1106, "Clothing", 5, 139.87m, 0.33m, 4, 4.9m, 468.56m),
    ];

    /// <summary>Параметры расчёта по умолчанию.</summary>
    public static AnalyticsRequest Request(AnalyticsPeriod? period = null) =>
        new("sample.csv", "test", period ?? AnalyticsPeriod.All);

    /// <summary>
    /// Создаёт продажу с осмысленными значениями по умолчанию,
    /// чтобы в тестах задавать только значимые для проверки поля.
    /// </summary>
    public static Sale Sale(
        int orderId = 1,
        string orderDate = "2022-01-01",
        int customerId = 100,
        string category = "Beauty",
        int quantity = 1,
        decimal unitPrice = 100m,
        decimal discount = 0.1m,
        int deliveryDays = 5,
        decimal rating = 4m,
        decimal? revenue = null,
        string region = "South",
        string paymentMethod = "Card")
    {
        var date = DateOnly.Parse(orderDate, System.Globalization.CultureInfo.InvariantCulture);

        return new Sale(
            orderId,
            date,
            customerId,
            category,
            region,
            quantity,
            unitPrice,
            discount,
            paymentMethod,
            deliveryDays,
            rating,
            revenue ?? quantity * unitPrice * (1 - discount));
    }

    /// <summary>
    /// Генерирует набор продаж заданного размера — нужен для проверки того,
    /// что параллельная реализация даёт тот же результат на объёме,
    /// превышающем порог включения параллелизма.
    /// </summary>
    public static IReadOnlyList<Sale> GenerateSales(int count, int seed = 42)
    {
        var random = new Random(seed);
        string[] categories = ["Beauty", "Clothing", "Electronics", "Home"];
        var start = new DateOnly(2022, 1, 1);

        return Enumerable.Range(0, count)
            .Select(index => Sale(
                orderId: 10_000 + index,
                orderDate: start.AddDays(index % 900).ToString("yyyy-MM-dd"),
                customerId: 1_000 + random.Next(0, 50),
                category: categories[random.Next(categories.Length)],
                quantity: random.Next(1, 10),
                unitPrice: Math.Round((decimal)(random.NextDouble() * 500 + 10), 2),
                discount: Math.Round((decimal)random.NextDouble() * 0.4m, 2),
                deliveryDays: random.Next(1, 12),
                rating: Math.Round((decimal)(random.NextDouble() * 4 + 1), 1)))
            .ToList();
    }
}
