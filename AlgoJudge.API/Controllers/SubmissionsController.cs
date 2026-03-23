using AlgoJudge.Application.DTOs.Submission;
using AlgoJudge.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AlgoJudge.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SubmissionsController : ControllerBase
    {
        private readonly ISubmissionService _submissionService;

        public SubmissionsController(ISubmissionService submissionService)
        {
            _submissionService = submissionService;
        }

        [HttpPost]
        public async Task<IActionResult> SubmitCode([FromBody] CreateSubmissionDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var result = await _submissionService.SubmitCodeAsync(dto);

                return CreatedAtAction(nameof(GetSubmissionById), new { id = result.Id }, result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetSubmissionById(Guid id) 
        {
            var result = await _submissionService.GetSubmissionByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }
    }
}
