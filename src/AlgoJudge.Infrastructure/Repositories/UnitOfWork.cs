using AlgoJudge.Application.Interfaces;
using AlgoJudge.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        public UnitOfWork(AppDbContext context)
        {
            _context = context;
        }
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
