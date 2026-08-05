using AlgoJudge.Application.Contracts.Submissions;
using AlgoJudge.Domain.Entities;
using AutoMapper;

namespace AlgoJudge.Application.Mappings;

public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Submission, SubmissionResponse>()
            .ForMember(
                response => response.ProblemTitle,
                options => options.MapFrom(submission => submission.Problem.Title))
            .ForMember(
                response => response.ProblemSlug,
                options => options.MapFrom(submission => submission.Problem.Slug))
            .ForMember(
                response => response.ExecutionTimeMs,
                options => options.MapFrom(submission => submission.ExecutionTime))
            .ForMember(
                response => response.MemoryUsedKb,
                options => options.MapFrom(submission => submission.MemoryUsed));
        CreateMap<Submission, SubmissionContentResponse>();
        CreateMap<CreateSubmissionRequest, Submission>();
    }
}
