namespace Evermore.Lunara.Core;

public sealed class LunaraEngine
{
    private readonly DeterministicMoonTideProcessor _processor;

    public LunaraEngine(MoonTideContainmentPolicy? containmentPolicy = null)
    {
        _processor = new DeterministicMoonTideProcessor(containmentPolicy);
    }

    public MoonTideSnapshot Process(
        MoonTideInput input,
        CancellationToken cancellationToken = default) =>
        _processor.Process(input, cancellationToken);
}
