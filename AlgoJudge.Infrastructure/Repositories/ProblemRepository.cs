using AlgoJudge.Application.DTOs.Common;
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
        public async Task AddAsync(Problem problem)
        {
            await _context.Problems.AddAsync(problem);
        }
        public async Task<Problem?> GetByIdAsync(int id)
        {
            return await _context.Problems.FindAsync(id);
        }

        public async Task<PagedResult<Problem>> GetPagedAsync(int pageNumber, int pageSize)
        {
            var totalCount = await _context.Problems.CountAsync();

            var items = await _context.Problems
                .OrderByDescending(p => p.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<Problem>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }
    }
}
