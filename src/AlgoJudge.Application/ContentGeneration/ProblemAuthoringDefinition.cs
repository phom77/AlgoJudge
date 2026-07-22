using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.Application.ContentGeneration;

public sealed class ProblemAuthoringDefinition
{
    public int SchemaVersion { get; init; }
    public ProblemExecutionMode ExecutionMode { get; init; }
    public FunctionSignature FunctionSignature { get; init; } = new();
    public IReadOnlyList<HandwrittenCaseDefinition> HandwrittenCases { get; init; } = [];
    public GeneratorSourceDefinition Generator { get; init; } = new();
    public GeneratorSourceDefinition InputValidator { get; init; } = new();
    public FunctionSourceDefinition ReferenceSolution { get; init; } = new();
    public IReadOnlyList<WrongSolutionDefinition> WrongSolutions { get; init; } = [];
}
