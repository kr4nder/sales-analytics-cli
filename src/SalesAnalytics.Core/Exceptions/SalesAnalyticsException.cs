namespace SalesAnalytics.Core.Exceptions;

/// <summary>
/// Базовый класс для всех ошибок, о которых нужно сообщить пользователю
/// понятным текстом и завершить приложение с кодом возврата 1.
/// Исключения, не унаследованные от этого класса, считаются непредвиденными.
/// </summary>
public abstract class SalesAnalyticsException : Exception
{
    /// <inheritdoc cref="SalesAnalyticsException"/>
    protected SalesAnalyticsException(string message)
        : base(message)
    {
    }

    /// <inheritdoc cref="SalesAnalyticsException"/>
    protected SalesAnalyticsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Входной CSV-файл не найден или недоступен для чтения.
/// </summary>
public sealed class InputFileException : SalesAnalyticsException
{
    /// <inheritdoc cref="InputFileException"/>
    public InputFileException(string message)
        : base(message)
    {
    }

    /// <inheritdoc cref="InputFileException"/>
    public InputFileException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Создаёт ошибку об отсутствующем файле.</summary>
    public static InputFileException NotFound(string path) =>
        new($"Входной файл не найден: «{path}». Проверьте значение аргумента --input.");
}

/// <summary>
/// Файл прочитан, но не содержит ни одной записи о продаже.
/// </summary>
public sealed class EmptyInputException : SalesAnalyticsException
{
    /// <inheritdoc cref="EmptyInputException"/>
    public EmptyInputException(string message)
        : base(message)
    {
    }

    /// <summary>Создаёт ошибку о пустом файле.</summary>
    public static EmptyInputException ForFile(string path) =>
        new($"Файл «{path}» не содержит данных о продажах: нет ни одной строки после заголовка.");
}

/// <summary>
/// Строка CSV не соответствует ожидаемому формату.
/// </summary>
public sealed class CsvFormatException : SalesAnalyticsException
{
    /// <inheritdoc cref="CsvFormatException"/>
    public CsvFormatException(int lineNumber, string reason)
        : base($"Ошибка разбора CSV в строке {lineNumber}: {reason}")
    {
        LineNumber = lineNumber;
        Reason = reason;
    }

    /// <summary>Номер проблемной строки в файле, начиная с 1.</summary>
    public int LineNumber { get; }

    /// <summary>Причина ошибки без указания номера строки.</summary>
    public string Reason { get; }
}

/// <summary>
/// Каталог для сохранения результата отсутствует или недоступен для записи.
/// </summary>
public sealed class OutputPathException : SalesAnalyticsException
{
    /// <inheritdoc cref="OutputPathException"/>
    public OutputPathException(string message)
        : base(message)
    {
    }

    /// <inheritdoc cref="OutputPathException"/>
    public OutputPathException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
