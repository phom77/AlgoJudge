using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Infrastructure.Repositories
{
    public class SubmissionRepository : ISubmissionRepository
    {
        private readonly AppDbContext _context;
        public SubmissionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Submission submission)
        {
            await _context.Submissions.AddAsync(submission);
        }

        public async Task<Submission?> GetByIdAsync(Guid id)
        {
            return await _context.Submissions.FindAsync(id);
        }
    }
}
