using AlgoJudge.Application.ContentGeneration;

namespace AlgoJudge.Application.Contracts.Admin;

public sealed class UpdateAuthoringSourcesRequest
{
    public GeneratorSourceDefinition Generator { get; init; } = new();
    public GeneratorSourceDefinition InputValidator { get; init; } = new();
    public FunctionSourceDefinition ReferenceSolution { get; init; } = new();
    public IReadOnlyList<WrongSolutionDefinition> WrongSolutions { get; init; } = [];
}
