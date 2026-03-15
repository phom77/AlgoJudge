using AlgoJudge.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.Interfaces
{
    public interface IProblemRepository
    {
        Task<Problem> CreateAsync(Problem problem);
        Task<Problem?> GetByIdAsync(int id);
        Task<IEnumerable<Problem>> GetAllAsync();
    }
}
