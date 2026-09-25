namespace SalesAnalytics.Core.Models;

/// <summary>
/// Период, за который считается статистика.
/// Нижняя граница включается, верхняя — нет (полуинтервал <c>[start; end)</c>),
/// как требует техническое задание для <c>--end_date</c>.
/// </summary>
/// <param name="StartDate">Дата, с которой учитывать записи. <see langword="null"/> — без нижней границы.</param>
/// <param name="EndDate">Дата, до которой учитывать записи. <see langword="null"/> — без верхней границы.</param>
public sealed record AnalyticsPeriod(DateOnly? StartDate, DateOnly? EndDate)
{
    /// <summary>Период без ограничений — учитываются все записи файла.</summary>
    public static AnalyticsPeriod All { get; } = new(null, null);

    /// <summary>Заданы ли какие-либо границы периода.</summary>
    public bool HasBounds => StartDate is not null || EndDate is not null;

    /// <summary>
    /// Проверяет, попадает ли дата в период.
    /// </summary>
    public bool Contains(DateOnly date) =>
        (StartDate is null || date >= StartDate.Value) &&
        (EndDate is null || date < EndDate.Value);
}

/// <summary>
/// Исходные данные для формирования отчёта, не относящиеся к самим продажам.
/// </summary>
/// <param name="SourceFile">Путь к обработанному CSV-файлу — попадает в метаданные отчёта.</param>
/// <param name="Mode">Название режима работы приложения.</param>
/// <param name="Period">Период фильтрации записей.</param>
public sealed record AnalyticsRequest(
    string SourceFile,
    string Mode,
    AnalyticsPeriod Period)
{
    /// <summary>Сколько категорий показывать в блоке «топ категорий по количеству».</summary>
    public int TopCategoriesCount { get; init; } = 4;

    /// <summary>Сколько покупателей показывать в блоке «топ покупателей по рейтингу».</summary>
    public int TopCustomersCount { get; init; } = 5;
}
