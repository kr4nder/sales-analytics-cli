namespace SalesAnalytics.Core.Models;

/// <summary>
/// Общая сумма продаж по одной категории товара.
/// </summary>
/// <param name="Category">Категория товара.</param>
/// <param name="GrossAmount">Сумма без учёта скидки (<c>quantity * unit_price</c>).</param>
/// <param name="NetRevenue">Фактическая выручка с учётом скидки.</param>
/// <param name="OrderCount">Количество заказов в категории.</param>
public sealed record CategorySales(
    string Category,
    decimal GrossAmount,
    decimal NetRevenue,
    int OrderCount);

/// <summary>
/// Количество проданных единиц товара в категории.
/// </summary>
/// <param name="Category">Категория товара.</param>
/// <param name="TotalQuantity">Суммарное количество проданных единиц.</param>
public sealed record CategoryQuantity(
    string Category,
    int TotalQuantity);

/// <summary>
/// Средняя цена за единицу товара в конкретном месяце.
/// </summary>
/// <param name="Month">Первое число месяца.</param>
/// <param name="AverageUnitPrice">Среднее арифметическое <c>unit_price</c> по заказам месяца.</param>
/// <param name="OrderCount">Количество заказов в месяце.</param>
public sealed record MonthlyAveragePrice(
    DateOnly Month,
    decimal AverageUnitPrice,
    int OrderCount);

/// <summary>
/// Покупатель и его средняя оценка.
/// </summary>
/// <param name="CustomerId">Идентификатор покупателя.</param>
/// <param name="AverageRating">Средняя оценка по всем заказам покупателя.</param>
/// <param name="OrderCount">Количество заказов покупателя.</param>
public sealed record CustomerRating(
    int CustomerId,
    decimal AverageRating,
    int OrderCount);

/// <summary>
/// Статистика по срокам доставки.
/// </summary>
/// <param name="AverageDeliveryDays">Среднее время доставки в днях.</param>
/// <param name="MinDeliveryDays">Минимальный срок доставки.</param>
/// <param name="MaxDeliveryDays">Максимальный срок доставки.</param>
public sealed record DeliveryStatistics(
    decimal AverageDeliveryDays,
    int MinDeliveryDays,
    int MaxDeliveryDays);

/// <summary>
/// Средняя скидка по категории за конкретный месяц.
/// </summary>
/// <param name="Category">Категория товара.</param>
/// <param name="Month">Первое число месяца.</param>
/// <param name="AverageDiscount">Средняя скидка долей единицы (0.25 — это 25 %).</param>
/// <param name="OrderCount">Количество заказов в группе.</param>
public sealed record CategoryMonthlyDiscount(
    string Category,
    DateOnly Month,
    decimal AverageDiscount,
    int OrderCount);
