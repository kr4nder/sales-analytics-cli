using System.Text;
using SalesAnalytics.Cli;

// Отчёт содержит кириллицу и символы псевдографики, поэтому вывод переводится в UTF-8.
TryUseUtf8Console();

using var cancellation = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    // Первое нажатие Ctrl+C гасится, чтобы приложение завершилось само
    // и успело сообщить о прерывании.
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

var application = new Application(Console.Out, Console.Error);

return await application.RunAsync(args, cancellation.Token);

static void TryUseUtf8Console()
{
    try
    {
        Console.OutputEncoding = Encoding.UTF8;
    }
    catch (IOException)
    {
        // Вывод перенаправлен в поток, не поддерживающий смену кодировки, — не критично.
    }
}
