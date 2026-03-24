using AlgoJudge.Application.DTOs.TestCase;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Entities;
using AutoMapper;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;

namespace AlgoJudge.Application.Services
{
    public class TestCaseService : ITestCaseService
    {
        private readonly ITestCaseRepository _testCaseRepository;
        private readonly IProblemRepository _problemRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public TestCaseService(
            ITestCaseRepository testCaseRepository,
            IProblemRepository problemRepository,
            IUnitOfWork unitOfWork,
            IMapper mapper)
        {
            _testCaseRepository = testCaseRepository;
            _problemRepository = problemRepository;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<TestCaseDto> CreateAsync(int problemId, CreateTestCaseDto dto)
        {
            var problem = await _problemRepository.GetByIdAsync(problemId);
            if (problem == null)
                throw new ArgumentException($"Problem {problemId} không tồn tại.");

            var testCase = _mapper.Map<TestCase>(dto);
            testCase.ProblemId = problemId;

            await _testCaseRepository.AddAsync(testCase);
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<TestCaseDto>(testCase);
        }

        public async Task<IEnumerable<TestCaseDto>> CreateBulkAsync(int problemId, Stream zipStream)
        {
            var problem = await _problemRepository.GetByIdAsync(problemId);
            if (problem == null)
                throw new ArgumentException($"Problem {problemId} không tồn tại.");

            var testCases = ParseZip(zipStream, problemId);
            if (!testCases.Any())
                throw new ArgumentException("Không tìm thấy cặp input/output hợp lệ trong file .zip.");

            await _testCaseRepository.AddRangeAsync(testCases);
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<IEnumerable<TestCaseDto>>(testCases);
        }

        public async Task DeleteAsync(int problemId, int testCaseId)
        {
            var testCase = await _testCaseRepository.GetByIdAsync(testCaseId);

            if (testCase == null || testCase.ProblemId != problemId)
                throw new ArgumentException($"TestCase {testCaseId} không tồn tại trong problem {problemId}.");

            _testCaseRepository.Delete(testCase);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<IEnumerable<TestCaseDto>> GetByProblemIdAsync(int problemId, bool includeHidden)
        {
            var testCases = await _testCaseRepository.GetByProblemIdAsync(problemId);

            if (!includeHidden)
                testCases = testCases.Where(tc => !tc.IsHidden);

            return _mapper.Map<IEnumerable<TestCaseDto>>(testCases);
        }

        private static List<TestCase> ParseZip(Stream zipStream, int problemId)
        {
            var result = new List<TestCase>();

            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            var inputs = archive.Entries
                .Where(e => e.Name.EndsWith(".in", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(e => Path.GetFileNameWithoutExtension(e.Name), e => e);

            var outputs = archive.Entries
                .Where(e => e.Name.EndsWith(".out", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(e => Path.GetFileNameWithoutExtension(e.Name), e => e);

            foreach (var key in inputs.Keys.Where(outputs.ContainsKey).OrderBy(k => k))
            {
                using var inReader = new StreamReader(inputs[key].Open());
                using var outReader = new StreamReader(outputs[key].Open());

                result.Add(new TestCase
                {
                    ProblemId = problemId,
                    Input = inReader.ReadToEnd(),
                    ExpectedOutput = outReader.ReadToEnd(),
                    IsHidden = true 
                });
            }

            return result;
        }
    }
}
