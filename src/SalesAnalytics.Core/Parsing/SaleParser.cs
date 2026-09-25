using System.Globalization;
using SalesAnalytics.Core.Abstractions;
using SalesAnalytics.Core.Exceptions;
using SalesAnalytics.Core.Models;

namespace SalesAnalytics.Core.Parsing;

/// <summary>
/// Разбор строк CSV без сторонних библиотек: строка делится по разделителю,
/// числа читаются в инвариантной культуре, дата — по набору известных форматов.
/// </summary>
public sealed class SaleParser : ISaleParser
{
    /// <inheritdoc />
    public Sale Parse(string line, int lineNumber, char delimiter)
    {
        if (!TryParse(line, lineNumber, delimiter, out var sale, out var error))
        {
            throw new CsvFormatException(lineNumber, error!);
        }

        return sale!;
    }

    /// <inheritdoc />
    public bool TryParse(string line, int lineNumber, char delimiter, out Sale? sale, out string? error)
    {
        sale = null;
        error = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            error = "строка пустая.";
            return false;
        }

        var fields = line.Split(delimiter);
        if (fields.Length != CsvFormat.ColumnCount)
        {
            error = $"ожидалось {CsvFormat.ColumnCount} колонок, получено {fields.Length}.";
            return false;
        }

        // Пробелы вокруг значений допускаются: в примере из ТЗ колонки выровнены отступами.
        for (var i = 0; i < fields.Length; i++)
        {
            fields[i] = fields[i].Trim();
        }

        if (!TryReadInt(fields, CsvFormat.Columns.OrderId, "order_id", out var orderId, ref error) ||
            !TryReadDate(fields, CsvFormat.Columns.OrderDate, "order_date", out var orderDate, ref error) ||
            !TryReadInt(fields, CsvFormat.Columns.CustomerId, "customer_id", out var customerId, ref error) ||
            !TryReadText(fields, CsvFormat.Columns.ProductCategory, "product_category", out var category, ref error) ||
            !TryReadText(fields, CsvFormat.Columns.Region, "region", out var region, ref error) ||
            !TryReadInt(fields, CsvFormat.Columns.Quantity, "quantity", out var quantity, ref error) ||
            !TryReadDecimal(fields, CsvFormat.Columns.UnitPrice, "unit_price", out var unitPrice, ref error) ||
            !TryReadDecimal(fields, CsvFormat.Columns.Discount, "discount", out var discount, ref error) ||
            !TryReadText(fields, CsvFormat.Columns.PaymentMethod, "payment_method", out var payment, ref error) ||
            !TryReadInt(fields, CsvFormat.Columns.DeliveryDays, "delivery_days", out var deliveryDays, ref error) ||
            !TryReadDecimal(fields, CsvFormat.Columns.CustomerRating, "customer_rating", out var rating, ref error) ||
            !TryReadDecimal(fields, CsvFormat.Columns.Revenue, "revenue", out var revenue, ref error))
        {
            return false;
        }

        if (!TryValidate(quantity, discount, deliveryDays, rating, ref error))
        {
            return false;
        }

        sale = new Sale(
            orderId,
            orderDate,
            customerId,
            category,
            region,
            quantity,
            unitPrice,
            discount,
            payment,
            deliveryDays,
            rating,
            revenue);

        return true;
    }

    /// <summary>
    /// Проверяет значения на смысловую корректность — формально разобранная строка
    /// всё ещё может содержать бессмысленные данные.
    /// </summary>
    private static bool TryValidate(
        int quantity,
        decimal discount,
        int deliveryDays,
        decimal rating,
        ref string? error)
    {
        if (quantity < 0)
        {
            error = $"количество не может быть отрицательным (quantity = {quantity}).";
            return false;
        }

        if (discount is < 0m or > 1m)
        {
            error = $"скидка должна быть в диапазоне от 0 до 1 (discount = {Format(discount)}).";
            return false;
        }

        if (deliveryDays < 0)
        {
            error = $"срок доставки не может быть отрицательным (delivery_days = {deliveryDays}).";
            return false;
        }

        if (rating is < 0m or > 5m)
        {
            error = $"оценка должна быть в диапазоне от 0 до 5 (customer_rating = {Format(rating)}).";
            return false;
        }

        return true;
    }

    private static bool TryReadText(string[] fields, int index, string column, out string value, ref string? error)
    {
        value = fields[index];
        if (value.Length != 0)
        {
            return true;
        }

        error = $"колонка «{column}» не заполнена.";
        return false;
    }

    private static bool TryReadInt(string[] fields, int index, string column, out int value, ref string? error)
    {
        var raw = fields[index];
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        error = $"колонка «{column}» содержит «{raw}», ожидалось целое число.";
        return false;
    }

    private static bool TryReadDecimal(string[] fields, int index, string column, out decimal value, ref string? error)
    {
        var raw = fields[index];

        // Файлы с разделителем «;» обычно приходят из локалей, где дробная часть
        // отделяется запятой, поэтому запятую приводим к точке и читаем инвариантно.
        var normalized = raw.Replace(',', '.');

        if (decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        error = $"колонка «{column}» содержит «{raw}», ожидалось число.";
        return false;
    }

    private static bool TryReadDate(string[] fields, int index, string column, out DateOnly value, ref string? error)
    {
        var raw = fields[index];
        if (DateOnly.TryParseExact(raw, CsvFormat.DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
        {
            return true;
        }

        error = $"колонка «{column}» содержит «{raw}», ожидалась дата в формате {string.Join(" / ", CsvFormat.DateFormats)}.";
        return false;
    }

    private static string Format(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}
