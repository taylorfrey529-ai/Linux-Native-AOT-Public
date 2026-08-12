namespace Evermore.Cli;

internal static class Program
{
    private static Task<int> Main(string[] args) =>
        EvermoreCliApp.InvokeAsync(args, Console.Out, Console.Error);
}
