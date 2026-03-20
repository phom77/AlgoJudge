using AlgoJudge.Application.DTOs.Problem;
using AlgoJudge.Domain.Entities;
using AutoMapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Problem, ProblemDto>();

            CreateMap<CreateProblemDto, Problem>();
        }
    }
}
