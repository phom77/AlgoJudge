using AlgoJudge.Application.DTOs.Submission;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.Interfaces
{
    public interface ISubmissionService
    {
        Task<SubmissionDto> SubmitCodeAsync(CreateSubmissionDto dto);
        Task<SubmissionDto?> GetSubmissionByIdAsync(Guid id);
    }
}
