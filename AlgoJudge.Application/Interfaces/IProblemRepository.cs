using AlgoJudge.Application.DTOs.Common;
using AlgoJudge.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.Interfaces
{
    public interface IProblemRepository
    {
        Task CreateAsync(Problem problem);
        Task<Problem?> GetByIdAsync(int id);
        Task<PagedResult<Problem>> GetPagedAsync(int pageNumber, int pageSize);
    }
}
