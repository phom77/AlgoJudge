using AlgoJudge.Application.DTOs.Submission;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using AutoMapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.Services
{
    public class SubmissionService : ISubmissionService
    {
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IProblemRepository _problemRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public SubmissionService(
            ISubmissionRepository submissionRepository,
            IProblemRepository problemRepository,
            IUnitOfWork unitOfWork,
            IMapper mapper)
        {
            _submissionRepository = submissionRepository;
            _problemRepository = problemRepository;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<SubmissionDto?> GetSubmissionByIdAsync(Guid id)
        {
            var submission = await _submissionRepository.GetByIdAsync(id);
            if (submission == null) return null;
            return _mapper.Map<SubmissionDto>(submission);
        }

        public async Task<SubmissionDto> SubmitCodeAsync(CreateSubmissionDto dto, Guid userId)
        {
            var problemExists = await _problemRepository.GetByIdAsync(dto.ProblemId);
            if (problemExists == null)
            {
                throw new ArgumentException($"Problem with ID {dto.ProblemId} does not exist.");
            }
            var submission = _mapper.Map<Submission>(dto);

            submission.UserId = userId;
            submission.Status = SubmissionStatus.Pending;
            submission.CreatedAt = DateTime.UtcNow;
            submission.ExecutionTime = 0;
            submission.MemoryUsed = 0;

            await _submissionRepository.AddAsync(submission);
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<SubmissionDto>(submission);
        }
    }
}
