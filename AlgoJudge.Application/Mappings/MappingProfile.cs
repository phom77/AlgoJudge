using AlgoJudge.Application.DTOs.Problem;
using AlgoJudge.Application.DTOs.Submission;
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
            //Problem Mappings
            CreateMap<Problem, ProblemDto>();
            CreateMap<CreateProblemDto, Problem>();

            //Submission Mappings
            CreateMap<Submission, SubmissionDto>();
            CreateMap<CreateSubmissionDto, Submission>();
        }
    }
}
