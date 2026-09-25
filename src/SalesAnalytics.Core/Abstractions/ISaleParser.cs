using SalesAnalytics.Core.Models;

namespace SalesAnalytics.Core.Abstractions;

/// <summary>
/// Преобразует одну строку CSV в объект <see cref="Sale"/>.
/// Выделен в отдельную абстракцию, чтобы разбор строки можно было тестировать
/// и подменять независимо от источника данных.
/// </summary>
public interface ISaleParser
{
    /// <summary>
    /// Разбирает строку CSV.
    /// </summary>
    /// <param name="line">Строка файла без символа перевода строки.</param>
    /// <param name="lineNumber">Номер строки в файле, начиная с 1, — используется в тексте ошибки.</param>
    /// <param name="delimiter">Разделитель колонок.</param>
    /// <returns>Разобранная продажа.</returns>
    /// <exception cref="Exceptions.CsvFormatException">Строка не соответствует ожидаемому формату.</exception>
    Sale Parse(string line, int lineNumber, char delimiter);

    /// <summary>
    /// Пытается разобрать строку CSV, не выбрасывая исключение.
    /// </summary>
    /// <param name="line">Строка файла без символа перевода строки.</param>
    /// <param name="lineNumber">Номер строки в файле, начиная с 1.</param>
    /// <param name="delimiter">Разделитель колонок.</param>
    /// <param name="sale">Разобранная продажа или <see langword="null"/>, если разбор не удался.</param>
    /// <param name="error">Описание причины ошибки или <see langword="null"/> при успехе.</param>
    /// <returns><see langword="true"/>, если строка успешно разобрана.</returns>
    bool TryParse(string line, int lineNumber, char delimiter, out Sale? sale, out string? error);
}
