using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(User user)
        {
            await _context.Users.AddAsync(user);
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User?> GetByUserNameAsync(string userName)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == userName);
        }
    }
}
