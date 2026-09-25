namespace SalesAnalytics.Core.Models;

/// <summary>
/// Одна строка заказа из CSV-файла продаж.
/// </summary>
/// <param name="OrderId">Идентификатор заказа (<c>order_id</c>).</param>
/// <param name="OrderDate">Дата заказа (<c>order_date</c>).</param>
/// <param name="CustomerId">Идентификатор покупателя (<c>customer_id</c>).</param>
/// <param name="ProductCategory">Категория товара (<c>product_category</c>).</param>
/// <param name="Region">Регион продажи (<c>region</c>).</param>
/// <param name="Quantity">Количество проданных единиц (<c>quantity</c>).</param>
/// <param name="UnitPrice">Цена за единицу до скидки (<c>unit_price</c>).</param>
/// <param name="Discount">Скидка долей единицы, например 0.28 — это 28 % (<c>discount</c>).</param>
/// <param name="PaymentMethod">Способ оплаты (<c>payment_method</c>).</param>
/// <param name="DeliveryDays">Срок доставки в днях (<c>delivery_days</c>).</param>
/// <param name="CustomerRating">Оценка покупателя от 1 до 5 (<c>customer_rating</c>).</param>
/// <param name="Revenue">Выручка по заказу с учётом скидки (<c>revenue</c>).</param>
public sealed record Sale(
    int OrderId,
    DateOnly OrderDate,
    int CustomerId,
    string ProductCategory,
    string Region,
    int Quantity,
    decimal UnitPrice,
    decimal Discount,
    string PaymentMethod,
    int DeliveryDays,
    decimal CustomerRating,
    decimal Revenue)
{
    /// <summary>
    /// Сумма заказа без учёта скидки: <c>quantity * unit_price</c>.
    /// Именно эта величина используется в аналитике «Общая сумма продаж по категориям»,
    /// как показано в примере ожидаемого вывода в техническом задании.
    /// </summary>
    public decimal GrossAmount => Quantity * UnitPrice;

    /// <summary>
    /// Фактическая выручка с учётом скидки — значение колонки <c>revenue</c> из исходного файла.
    /// </summary>
    public decimal NetRevenue => Revenue;

    /// <summary>
    /// Первое число месяца, к которому относится заказ. Используется как ключ группировки по месяцам.
    /// </summary>
    public DateOnly MonthKey => new(OrderDate.Year, OrderDate.Month, 1);
}
