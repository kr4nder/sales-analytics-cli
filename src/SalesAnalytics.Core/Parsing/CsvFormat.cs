using System.Globalization;

namespace SalesAnalytics.Core.Parsing;

/// <summary>
/// Сведения о формате входного CSV-файла: набор колонок, разделители и форматы дат.
/// </summary>
public static class CsvFormat
{
    /// <summary>Разделитель по умолчанию, если определить его по заголовку не удалось.</summary>
    public const char DefaultDelimiter = ',';

    /// <summary>Количество колонок в строке файла продаж.</summary>
    public const int ColumnCount = 12;

    /// <summary>Разделители, которые приложение умеет распознавать автоматически.</summary>
    public static readonly char[] SupportedDelimiters = [',', ';', '\t'];

    /// <summary>
    /// Форматы дат, которые принимаются в колонке <c>order_date</c>.
    /// Основной формат исходного файла — <c>M/d/yyyy</c> (например, <c>1/1/2022</c>).
    /// </summary>
    public static readonly string[] DateFormats =
    [
        "M/d/yyyy",
        "MM/dd/yyyy",
        "yyyy-MM-dd",
        "d.M.yyyy",
        "dd.MM.yyyy",
    ];

    /// <summary>Порядковые номера колонок в строке CSV.</summary>
    public static class Columns
    {
        /// <summary>Идентификатор заказа.</summary>
        public const int OrderId = 0;

        /// <summary>Дата заказа.</summary>
        public const int OrderDate = 1;

        /// <summary>Идентификатор покупателя.</summary>
        public const int CustomerId = 2;

        /// <summary>Категория товара.</summary>
        public const int ProductCategory = 3;

        /// <summary>Регион продажи.</summary>
        public const int Region = 4;

        /// <summary>Количество единиц.</summary>
        public const int Quantity = 5;

        /// <summary>Цена за единицу.</summary>
        public const int UnitPrice = 6;

        /// <summary>Скидка.</summary>
        public const int Discount = 7;

        /// <summary>Способ оплаты.</summary>
        public const int PaymentMethod = 8;

        /// <summary>Срок доставки в днях.</summary>
        public const int DeliveryDays = 9;

        /// <summary>Оценка покупателя.</summary>
        public const int CustomerRating = 10;

        /// <summary>Выручка с учётом скидки.</summary>
        public const int Revenue = 11;
    }

    /// <summary>
    /// Определяет разделитель по строке заголовка: побеждает тот символ,
    /// который делит строку ровно на <see cref="ColumnCount"/> частей.
    /// Если однозначно определить не удалось, выбирается самый частый символ-кандидат.
    /// </summary>
    public static char DetectDelimiter(string? headerLine)
    {
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return DefaultDelimiter;
        }

        var best = DefaultDelimiter;
        var bestCount = 0;

        foreach (var candidate in SupportedDelimiters)
        {
            var count = headerLine.Count(c => c == candidate);
            if (count == 0)
            {
                continue;
            }

            // Точное совпадение с ожидаемым числом колонок — лучший возможный признак.
            if (count == ColumnCount - 1)
            {
                return candidate;
            }

            if (count > bestCount)
            {
                best = candidate;
                bestCount = count;
            }
        }

        return best;
    }

    /// <summary>
    /// Определяет, является ли строка заголовком таблицы.
    /// Заголовок распознаётся по имени первой колонки или по тому,
    /// что в первой колонке лежит не число.
    /// </summary>
    public static bool IsHeaderLine(string line, char delimiter)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var firstField = line.Split(delimiter, 2)[0].Trim();

        return firstField.Equals("order_id", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(firstField, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }
}
