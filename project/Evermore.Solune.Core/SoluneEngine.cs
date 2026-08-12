namespace Evermore.Solune.Core;

public sealed class SoluneEngine
{
    private readonly DeterministicSoluneProcessor _processor;

    public SoluneEngine(DeterministicSoluneProcessor? processor = null)
    {
        _processor = processor ?? new DeterministicSoluneProcessor();
    }

    public SoluneSnapshot Process(
        SoluneInput input,
        CancellationToken cancellationToken = default) =>
        _processor.Process(input, cancellationToken);
}
