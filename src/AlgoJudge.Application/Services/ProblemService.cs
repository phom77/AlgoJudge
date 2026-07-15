using AlgoJudge.Application.DTOs.Common;
using AlgoJudge.Application.DTOs.Problem;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Entities;
using AutoMapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.Services
{
    public class ProblemService : IProblemService
    {
        private readonly IProblemRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public ProblemService(IProblemRepository repository, IUnitOfWork unitOfWork, IMapper mapper)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<ProblemDto> CreateProblemAsync(CreateProblemDto dto, Guid createdBy)
        {
            var problem = _mapper.Map<Problem>(dto);
            problem.CreatedBy = createdBy;

            await _repository.AddAsync(problem);

            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<ProblemDto>(problem);
        }

        public async Task<PagedResult<ProblemDto>> GetProblemAsync(int pageNumber, int pageSize)
        {
            var pagedEntities = await _repository.GetPagedAsync(pageNumber, pageSize);

            var dtoItems = _mapper.Map<IEnumerable<ProblemDto>>(pagedEntities.Items).ToList();

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

            return _mapper.Map<ProblemDto>(problem);
        }

        public async Task<ProblemDto?> UpdateProblemAsync(int id, UpdateProblemDto dto, Guid requesterId)
        {
            var problem = await _repository.GetByIdAsync(id);
            if (problem == null)
                return null;

            if(problem.CreatedBy != requesterId)
                throw new UnauthorizedAccessException("You are not authorized to update this problem.");

            problem.Title = dto.Title;
            problem.Description = dto.Description;
            problem.TimeLimit = dto.TimeLimit;
            problem.MemoryLimit = dto.MemoryLimit;
            problem.Difficulty = dto.Difficulty;
            problem.Score = dto.Score;

            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<ProblemDto>(problem);
        }

        public async Task<bool> DeleteProblemAsync(int id, Guid requesterId)
        {
            var problem = await _repository.GetByIdAsync(id);
            if (problem == null) return false;

            if (problem.CreatedBy != requesterId)
                throw new UnauthorizedAccessException("You are not authorized to delete this problem.");

            _repository.Delete(problem);
            await _unitOfWork.SaveChangesAsync();

            return true;

        }
    }
}
