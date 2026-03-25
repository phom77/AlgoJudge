using AlgoJudge.Application.DTOs.Submission;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Domain.Enums;
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

        // GET /api/submissions?userId=...&problemId=...&status=...&pageNumber=1&pageSize=10
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetHistory(
            [FromQuery] Guid? userId,
            [FromQuery] int? problemId,
            [FromQuery] SubmissionStatus? status,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var callerId = GetUserIdFromToken();
            if (callerId == null)
                return Unauthorized(new { message = "Token không hợp lệ." });

            var isTeacher = User.IsInRole("Teacher");

            if (!isTeacher)
            {
                if (userId.HasValue && userId.Value != callerId.Value)
                    return Forbid();

                userId = callerId.Value; 
            }

            var result = await _submissionService.GetHistoryAsync(
                userId, problemId, status, pageNumber, pageSize);

            return Ok(result);
        }

        private Guid? GetUserIdFromToken()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                     ?? User.FindFirst("sub");

            if (claim == null || !Guid.TryParse(claim.Value, out var id))
                return null;

            return id;
        }
    }
}
