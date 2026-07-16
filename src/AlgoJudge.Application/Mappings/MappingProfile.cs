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
                response => response.ExecutionTimeMs,
                options => options.MapFrom(submission => submission.ExecutionTime))
            .ForMember(
                response => response.MemoryUsedKb,
                options => options.MapFrom(submission => submission.MemoryUsed));
        CreateMap<CreateSubmissionRequest, Submission>();
    }
}
