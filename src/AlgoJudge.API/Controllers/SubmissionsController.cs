using AlgoJudge.Application.DTOs.Submission;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AlgoJudge.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
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
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { message = "Token is invalid." });

            try
            {
                var result = await _submissionService.SubmitCodeAsync(dto, userId.Value);
                return CreatedAtAction(nameof(GetSubmissionById), new { id = result.Id }, result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetSubmissionById(Guid id)
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { message = "Token is invalid." });

            try
            {
                var result = await _submissionService.GetSubmissionByIdAsync(id, userId.Value);
                return result == null ? NotFound() : Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetHistory(
            [FromQuery] int? problemId,
            [FromQuery] SubmissionStatus? status,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { message = "Token is invalid." });

            var result = await _submissionService.GetHistoryAsync(
                userId.Value, problemId, status, pageNumber, pageSize);

            return Ok(result);
        }

        private Guid? GetUserIdFromToken()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                     ?? User.FindFirst("sub");

            return claim != null && Guid.TryParse(claim.Value, out var id)
                ? id
                : null;
        }
    }
}
