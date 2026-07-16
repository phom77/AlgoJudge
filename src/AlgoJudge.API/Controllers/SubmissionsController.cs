using AlgoJudge.Application.DTOs.Submission;
using AlgoJudge.Application.DTOs.Common;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Exceptions;
using AlgoJudge.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AlgoJudge.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public class SubmissionsController : ControllerBase
    {
        private readonly ISubmissionService _submissionService;

        public SubmissionsController(ISubmissionService submissionService)
        {
            _submissionService = submissionService;
        }

        [HttpPost]
        [ProducesResponseType(typeof(SubmissionDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SubmissionDto>> SubmitCode(
            [FromBody] CreateSubmissionDto dto)
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                throw new AuthenticationException("Token is invalid.");

            var result = await _submissionService.SubmitCodeAsync(dto, userId.Value);
            return CreatedAtAction(nameof(GetSubmissionById), new { id = result.Id }, result);
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(SubmissionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SubmissionDto>> GetSubmissionById(Guid id)
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                throw new AuthenticationException("Token is invalid.");

            var result = await _submissionService.GetSubmissionByIdAsync(id, userId.Value);
            if (result == null)
                throw new ResourceNotFoundException($"Submission '{id}' was not found.");

            return Ok(result);
        }

        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<SubmissionDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResult<SubmissionDto>>> GetHistory(
            [FromQuery] int? problemId,
            [FromQuery] SubmissionStatus? status,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var userId = GetUserIdFromToken();
            if (userId == null)
                throw new AuthenticationException("Token is invalid.");

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
