namespace SalesAnalytics.Cli.Configuration;

/// <summary>
/// Текст справки, выводимой по <c>--help</c> и при запуске без аргументов.
/// </summary>
public static class HelpText
{
    /// <summary>Полный текст справки.</summary>
    public const string Value = """
        Аналитика продаж из CSV-файла.

        ИСПОЛЬЗОВАНИЕ
          SalesAnalytics.Cli --mode=<режим> --input=<файл.csv> [--output=<файл.json>]
                             [--start_date=<дата>] [--end_date=<дата>] [--max-rows=<N>]

        РЕЖИМЫ (--mode, -m)
          console    Синхронное чтение CSV, расчёт и вывод в консоль. Значение по умолчанию.
          file       То же самое, но результат сохраняется в JSON-файл (--output обязателен).
          async      Асинхронное чтение файла и асинхронная запись результата.
          parallel   Параллельный расчёт аналитик: Parallel.ForEach и PLINQ.
          di         Все сервисы разрешаются из DI-контейнера (Microsoft.Extensions.Hosting).
          full       Асинхронный ввод-вывод + параллельный расчёт + DI.

        АРГУМЕНТЫ
          --input, -i     Путь к входному CSV-файлу. Обязательный.
          --output, -o    Путь к JSON-файлу результата. Без него отчёт печатается в консоль.
          --start_date    Дата, с которой учитывать записи (включительно).
          --end_date      Дата, до которой учитывать записи (НЕ включительно).
          --max-rows      Максимум строк в блоке консольного отчёта; 0 — без ограничения.
                          По умолчанию 24: блоки с разбивкой по месяцам иначе занимают сотни строк.
          --help, -h      Показать эту справку.

        ФОРМАТЫ ДАТ
          yyyy-MM-dd, dd.MM.yyyy, M/d/yyyy — например, 2022-03-01, 01.03.2022, 3/1/2022.

        ФОРМАТ ВХОДНОГО ФАЙЛА
          Заголовок со столбцами order_id, order_date, customer_id, product_category, region,
          quantity, unit_price, discount, payment_method, delivery_days, customer_rating, revenue.
          Разделитель определяется автоматически: запятая, точка с запятой или табуляция.
          Дробная часть чисел — точка (для файлов с «;» допускается и запятая).

        ПРИМЕРЫ
          SalesAnalytics.Cli --mode=console --input=sales_5000.csv
          SalesAnalytics.Cli --mode=file --input=sales_5000.csv --output=result.json
          SalesAnalytics.Cli --mode=async --input=sales_5000.csv
          SalesAnalytics.Cli --mode=full --input=sales_5000.csv --output=result.json
          SalesAnalytics.Cli -m parallel -i sales_5000.csv --start_date=2022-01-01 --end_date=2023-01-01

        КОДЫ ВОЗВРАТА
          0  Отчёт успешно сформирован.
          1  Ошибка: неверные аргументы, недоступный файл или некорректные данные.
        """;
}
