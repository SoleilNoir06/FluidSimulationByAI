using FluidSimulationByAI.App;

namespace FluidSimulationByAI;

internal static class Program
{
    private static void Main(string[] args)
    {
        using var app = new FluidApp(CaptureOptions.Parse(args));
        app.Run();
    }
}
