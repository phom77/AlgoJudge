using AlgoJudge.Application.DTOs.Common;
using AlgoJudge.Application.DTOs.Problem;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.Interfaces
{
    public interface IProblemService
    {
        Task<ProblemDto> CreateProblemAsync(CreateProblemDto dto);
        Task<ProblemDto?> GetProblemByIdAsync(int id);
        Task<PagedResult<ProblemDto>> GetProblemAsync(int pageNumber, int pageSize);
    }
}
