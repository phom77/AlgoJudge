using AlgoJudge.Application.DTOs.Submission;
using AlgoJudge.Domain.Entities;
using AutoMapper;

namespace AlgoJudge.Application.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Submission, SubmissionDto>();
            CreateMap<CreateSubmissionDto, Submission>();
        }
    }
}
