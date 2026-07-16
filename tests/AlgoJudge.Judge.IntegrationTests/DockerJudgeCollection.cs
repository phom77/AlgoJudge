namespace AlgoJudge.Judge.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DockerJudgeCollection
{
    public const string Name = "Docker judge";
}
