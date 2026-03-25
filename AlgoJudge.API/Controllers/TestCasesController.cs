using AlgoJudge.Application.DTOs.TestCase;
using AlgoJudge.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AlgoJudge.API.Controllers
{
    [Route("api/problems/{problemId}/testcases")]
    [ApiController]
    public class TestCasesController : ControllerBase
    {
        private readonly ITestCaseService _testCaseService;

        public TestCasesController(ITestCaseService testCaseService)
        {
            _testCaseService = testCaseService;
        }

        [HttpPost]
        [Authorize(Roles = "Teacher")]
        public async Task<IActionResult> Create(int problemId, [FromBody] CreateTestCaseDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var result = await _testCaseService.CreateAsync(problemId, dto);
                return CreatedAtAction(nameof(GetAll), new { problemId }, result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // POST /api/problems/{problemId}/testcases/bulk
        // Body: multipart/form-data, field "file" = file .zip
        [HttpPost("bulk")]
        [Authorize(Roles = "Teacher")]
        public async Task<IActionResult> CreateBulk(int problemId, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Vui lòng upload file .zip." });

            if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Chỉ chấp nhận file .zip." });

            const long maxSizeBytes = 20 * 1024 * 1024; 
            if (file.Length > maxSizeBytes)
                return BadRequest(new { message = "File quá lớn. Tối đa 20MB." });

            try
            {
                using var stream = file.OpenReadStream();
                var result = await _testCaseService.CreateBulkAsync(problemId, stream);
                return Ok(new { message = $"Đã thêm {result.Count()} test case.", items = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(int problemId, [FromQuery] bool includeHidden = false)
        {
            var result = await _testCaseService.GetByProblemIdAsync(problemId, includeHidden);
            return Ok(result);
        }

        // DELETE /api/problems/{problemId}/testcases/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Teacher")]
        public async Task<IActionResult> Delete(int problemId, int id)
        {
            try
            {
                await _testCaseService.DeleteAsync(problemId, id);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
    }
}
