using System.Globalization;
using System.Text;
using SalesAnalytics.Core.Abstractions;
using SalesAnalytics.Core.Models;

namespace SalesAnalytics.Core.Output;

/// <summary>
/// Настройки читаемого вывода в консоль.
/// </summary>
public sealed record ConsoleReportOptions
{
    /// <summary>
    /// Максимум строк в одном блоке отчёта. Блоки с разбивкой по месяцам
    /// на реальных данных дают сотни строк, которые вытесняют остальной вывод
    /// из окна терминала, поэтому по умолчанию список усекается.
    /// Значение <c>0</c> отключает ограничение.
    /// </summary>
    public int MaxRowsPerBlock { get; init; } = 24;
}

/// <summary>
/// Выводит отчёт в консоль (или любой другой <see cref="TextWriter"/>)
/// в виде выровненных текстовых таблиц.
/// </summary>
/// <param name="output">Приёмник текста. В тестах — <see cref="StringWriter"/>.</param>
/// <param name="options">Настройки вывода.</param>
public sealed class ConsoleResultWriter(TextWriter output, ConsoleReportOptions? options = null) : IResultWriter
{
    /// <summary>Ширина разделительной линии между блоками.</summary>
    private const int SeparatorWidth = 78;

    /// <summary>Отчёт для человека форматируется в русской культуре: «1 234,56».</summary>
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("ru-RU");

    private readonly TextWriter _output = output ?? throw new ArgumentNullException(nameof(output));
    private readonly ConsoleReportOptions _options = options ?? new ConsoleReportOptions();

    /// <inheritdoc />
    public void Write(AnalyticsReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        _output.Write(Render(report));
        _output.Flush();
    }

    /// <inheritdoc />
    public async Task WriteAsync(AnalyticsReport report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();

        await _output.WriteAsync(Render(report)).ConfigureAwait(false);
        await _output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Собирает текст отчёта целиком. Отдельный метод упрощает тестирование:
    /// результат можно сравнить со строкой, не подменяя консоль.
    /// </summary>
    private string Render(AnalyticsReport report)
    {
        var builder = new StringBuilder();

        WriteHeader(builder, report.Metadata);
        WriteSalesByCategory(builder, report.SalesByCategory);
        WriteTopCategories(builder, report.TopCategoriesByQuantity);
        WriteAveragePriceByMonth(builder, report.AveragePriceByMonth);
        WriteTopCustomers(builder, report.TopCustomersByRating);
        WriteDelivery(builder, report.Delivery);
        WriteDiscounts(builder, report.AverageDiscountByCategoryAndMonth);

        return builder.ToString();
    }

    private static void WriteHeader(StringBuilder builder, ReportMetadata metadata)
    {
        builder.AppendLine(new string('=', SeparatorWidth));
        builder.AppendLine("  АНАЛИТИКА ПРОДАЖ");
        builder.AppendLine(new string('=', SeparatorWidth));
        builder.AppendLine($"Файл:               {metadata.SourceFile}");
        builder.AppendLine($"Режим:              {metadata.Mode}");
        builder.AppendLine($"Записей прочитано:  {Number(metadata.TotalRecords)}");
        builder.AppendLine($"Записей в расчёте:  {Number(metadata.AnalyzedRecords)}");
        builder.AppendLine($"Период:             {DescribePeriod(metadata)}");
        builder.AppendLine($"Сформирован:        {metadata.GeneratedAtUtc.UtcDateTime:yyyy-MM-dd HH:mm:ss} UTC");
    }

    private void WriteSalesByCategory(StringBuilder builder, IReadOnlyList<CategorySales> items)
    {
        WriteBlockTitle(builder, "1. Общая сумма продаж по категориям");

        var table = new TextTable(
            ("Категория", 16, false),
            ("Сумма без скидки", 18, true),
            ("Выручка со скидкой", 20, true),
            ("Заказов", 9, true));

        foreach (var item in Limit(items))
        {
            table.AddRow(item.Category, Money(item.GrossAmount), Money(item.NetRevenue), Number(item.OrderCount));
        }

        table.AddSeparator();
        table.AddRow(
            "ИТОГО",
            Money(items.Sum(item => item.GrossAmount)),
            Money(items.Sum(item => item.NetRevenue)),
            Number(items.Sum(item => item.OrderCount)));

        table.Render(builder);
        WriteTruncationNote(builder, items.Count);
    }

    private void WriteTopCategories(StringBuilder builder, IReadOnlyList<CategoryQuantity> items)
    {
        WriteBlockTitle(builder, $"2. Топ-{items.Count} категорий по количеству проданных единиц");

        var table = new TextTable(
            ("Место", 7, false),
            ("Категория", 16, false),
            ("Единиц продано", 16, true));

        var position = 1;
        foreach (var item in items)
        {
            table.AddRow(position++.ToString(DisplayCulture), item.Category, Number(item.TotalQuantity));
        }

        table.Render(builder);
    }

    private void WriteAveragePriceByMonth(StringBuilder builder, IReadOnlyList<MonthlyAveragePrice> items)
    {
        WriteBlockTitle(builder, "3. Средняя цена за единицу товара по месяцам");

        var table = new TextTable(
            ("Месяц", 18, false),
            ("Средняя цена", 14, true),
            ("Заказов", 9, true));

        foreach (var item in Limit(items))
        {
            table.AddRow(MonthName(item.Month), Money(item.AverageUnitPrice), Number(item.OrderCount));
        }

        table.Render(builder);
        WriteTruncationNote(builder, items.Count);
    }

    private void WriteTopCustomers(StringBuilder builder, IReadOnlyList<CustomerRating> items)
    {
        WriteBlockTitle(builder, $"4. Топ-{items.Count} покупателей по рейтингу");

        var table = new TextTable(
            ("Место", 7, false),
            ("Покупатель", 14, false),
            ("Средний рейтинг", 17, true),
            ("Заказов", 9, true));

        var position = 1;
        foreach (var item in items)
        {
            table.AddRow(
                position++.ToString(DisplayCulture),
                item.CustomerId.ToString(DisplayCulture),
                Money(item.AverageRating),
                Number(item.OrderCount));
        }

        table.Render(builder);
    }

    private void WriteDelivery(StringBuilder builder, DeliveryStatistics delivery)
    {
        WriteBlockTitle(builder, "5. Время доставки");

        builder.AppendLine($"  Среднее:  {Money(delivery.AverageDeliveryDays)} дн.");
        builder.AppendLine($"  Минимум:  {Number(delivery.MinDeliveryDays)} дн.");
        builder.AppendLine($"  Максимум: {Number(delivery.MaxDeliveryDays)} дн.");
    }

    private void WriteDiscounts(StringBuilder builder, IReadOnlyList<CategoryMonthlyDiscount> items)
    {
        WriteBlockTitle(builder, "6. Средняя скидка по категориям с разбивкой по месяцам");

        var table = new TextTable(
            ("Категория", 16, false),
            ("Месяц", 18, false),
            ("Средняя скидка", 16, true),
            ("Заказов", 9, true));

        foreach (var item in Limit(items))
        {
            table.AddRow(item.Category, MonthName(item.Month), Percent(item.AverageDiscount), Number(item.OrderCount));
        }

        table.Render(builder);
        WriteTruncationNote(builder, items.Count);
    }

    private static void WriteBlockTitle(StringBuilder builder, string title)
    {
        builder.AppendLine();
        builder.AppendLine($"=== {title} ===");
    }

    /// <summary>
    /// Обрезает список до <see cref="ConsoleReportOptions.MaxRowsPerBlock"/> строк.
    /// </summary>
    private IEnumerable<T> Limit<T>(IReadOnlyList<T> items) =>
        _options.MaxRowsPerBlock > 0 ? items.Take(_options.MaxRowsPerBlock) : items;

    /// <summary>
    /// Предупреждает, что часть строк скрыта, чтобы усечение не выглядело потерей данных.
    /// </summary>
    private void WriteTruncationNote(StringBuilder builder, int totalRows)
    {
        var limit = _options.MaxRowsPerBlock;
        if (limit <= 0 || totalRows <= limit)
        {
            return;
        }

        builder.AppendLine(
            $"  … показаны первые {limit} из {Number(totalRows)} строк. " +
            "Полный список доступен в JSON или с параметром --max-rows=0.");
    }

    private static string DescribePeriod(ReportMetadata metadata) =>
        (metadata.StartDate, metadata.EndDate) switch
        {
            (null, null) => "весь файл",
            ({ } start, null) => $"с {start:yyyy-MM-dd}",
            (null, { } end) => $"до {end:yyyy-MM-dd} (не включая)",
            ({ } start, { } end) => $"с {start:yyyy-MM-dd} по {end:yyyy-MM-dd} (не включая)",
        };

    private static string MonthName(DateOnly month)
    {
        var text = month.ToString("MMMM yyyy", DisplayCulture);
        return char.ToUpper(text[0], DisplayCulture) + text[1..];
    }

    private static string Money(decimal value) => value.ToString("N2", DisplayCulture);

    private static string Number(int value) => value.ToString("N0", DisplayCulture);

    private static string Percent(decimal value) => (value * 100m).ToString("N2", DisplayCulture) + " %";

    /// <summary>
    /// Минимальная таблица с выравниванием колонок по фиксированной ширине.
    /// </summary>
    private sealed class TextTable
    {
        private const string ColumnGap = "  ";

        private readonly (string Title, int Width, bool RightAligned)[] _columns;
        private readonly List<string[]?> _rows = [];

        public TextTable(params (string Title, int Width, bool RightAligned)[] columns) => _columns = columns;

        public void AddRow(params string[] cells) => _rows.Add(cells);

        /// <summary>Добавляет горизонтальную линию — используется перед итоговой строкой.</summary>
        public void AddSeparator() => _rows.Add(null);

        public void Render(StringBuilder builder)
        {
            var headerCells = _columns.Select(column => Align(column.Title, column.Width, column.RightAligned));
            builder.AppendLine(string.Join(ColumnGap, headerCells));
            builder.AppendLine(RenderSeparator());

            foreach (var row in _rows)
            {
                if (row is null)
                {
                    builder.AppendLine(RenderSeparator());
                    continue;
                }

                var cells = row.Select((cell, index) => Align(cell, _columns[index].Width, _columns[index].RightAligned));
                builder.AppendLine(string.Join(ColumnGap, cells));
            }
        }

        private string RenderSeparator() =>
            string.Join(ColumnGap, _columns.Select(column => new string('-', column.Width)));

        /// <summary>
        /// Выравнивает ячейку по ширине колонки; слишком длинное значение усекается
        /// с многоточием, чтобы таблица не разъезжалась.
        /// </summary>
        private static string Align(string value, int width, bool rightAligned)
        {
            if (value.Length > width)
            {
                value = value[..(width - 1)] + "…";
            }

            return rightAligned ? value.PadLeft(width) : value.PadRight(width);
        }
    }
}
