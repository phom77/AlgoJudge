using AlgoJudge.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.Interfaces
{
    public interface ISubmissionRepository
    {
        Task AddAsync(Submission submission);
        Task<Submission?> GetByIdAsync(Guid id);
        Task<IEnumerable<Submission>> GetPendingAsync();
    }
}
