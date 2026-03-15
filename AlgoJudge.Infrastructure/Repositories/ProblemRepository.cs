using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace AlgoJudge.Infrastructure.Repositories
{
    public class ProblemRepository : IProblemRepository
    {
        private readonly AppDbContext _context;

        public ProblemRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<Problem> CreateAsync(Problem problem)
        {
            await _context.Problems.AddAsync(problem);
            await _context.SaveChangesAsync();
            return problem;
        }

        public async Task<IEnumerable<Problem>> GetAllAsync()
        {
            return await _context.Problems.ToListAsync();
        }

        public async Task<Problem?> GetByIdAsync(int id)
        {
            return await _context.Problems.FindAsync(id);
        }
    }
}
