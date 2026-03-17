using AlgoJudge.Application.DTOs.Common;
using AlgoJudge.Application.DTOs.Problem;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.Services
{
    public class ProblemService : IProblemService
    {
        private readonly IProblemRepository _repository;
        private readonly IUnitOfWork _unitOfWork;

        public ProblemService(IProblemRepository repository, IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
        }

        public async Task<ProblemDto> CreateProblemAsync(CreateProblemDto dto)
        {
            var problem = new Problem
            {
                Title = dto.Title,
                Description = dto.Description,
                TimeLimit = dto.TimeLimit,
                MemoryLimit = dto.MemoryLimit,
                Difficulty = dto.Difficulty,
                CreatedBy = dto.CreatedBy
            };

            await _repository.CreateAsync(problem);

            await _unitOfWork.SaveChangesAsync();

            return new ProblemDto
            {
                Id = problem.Id,
                Title = problem.Title,
                Description = problem.Description,
                TimeLimit = problem.TimeLimit,
                MemoryLimit = problem.MemoryLimit,
                Difficulty = problem.Difficulty,
                CreatedAt = problem.CreatedAt
            };
        }

        public async Task<PagedResult<ProblemDto>> GetProblemAsync(int pageNumber, int pageSize)
        {
            var pagedEntities = await _repository.GetPagedAsync(pageNumber, pageSize);

            var dtoItems = pagedEntities.Items.Select(p => new ProblemDto
            {
                Id = p.Id,
                Title = p.Title,
                Description = p.Description,
                TimeLimit = p.TimeLimit,
                MemoryLimit = p.MemoryLimit,
                Difficulty = p.Difficulty,
                CreatedAt = p.CreatedAt
            }).ToList();

            return new PagedResult<ProblemDto>
            {
                Items = dtoItems,
                TotalCount = pagedEntities.TotalCount,
                PageNumber = pagedEntities.PageNumber,
                PageSize = pagedEntities.PageSize
            };
        }

        public async Task<ProblemDto?> GetProblemByIdAsync(int id)
        {
            var problem = await _repository.GetByIdAsync(id);

            if (problem == null) return null;

            return new ProblemDto
            {
                Id = problem.Id,
                Title = problem.Title,
                Description = problem.Description,
                TimeLimit = problem.TimeLimit,
                MemoryLimit = problem.MemoryLimit,
                Difficulty = problem.Difficulty,
                CreatedAt = problem.CreatedAt
            };
        }
    }
}
