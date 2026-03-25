using AlgoJudge.Application.DTOs.Submission;
using AlgoJudge.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
        [Authorize]
        public async Task<IActionResult> SubmitCode([FromBody] CreateSubmissionDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
                           ?? User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized(new { message = "Token không hợp lệ." });
            try
            {
                var result = await _submissionService.SubmitCodeAsync(dto, userId);

                return CreatedAtAction(nameof(GetSubmissionById), new { id = result.Id }, result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetSubmissionById(Guid id) 
        {
            var result = await _submissionService.GetSubmissionByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }
    }
}
