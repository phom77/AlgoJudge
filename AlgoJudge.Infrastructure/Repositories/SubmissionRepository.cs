using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
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

        public async Task<IEnumerable<Submission>> GetPendingAsync()
        {
            return await _context.Submissions
                .Where(s => s.Status == SubmissionStatus.Pending)
                .OrderBy(s => s.CreatedAt) 
                .Take(10)                 
                .ToListAsync();
        }
    }
}
