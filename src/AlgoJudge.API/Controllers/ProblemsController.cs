using AlgoJudge.Application.Contracts.Problems;
using AlgoJudge.Application.Contracts.Common;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AlgoJudge.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public class ProblemsController : ControllerBase
    {
        private readonly IProblemService _problemService;

        public ProblemsController(IProblemService problemService)
        {
            _problemService = problemService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(PagedResponse<ProblemListItemResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PagedResponse<ProblemListItemResponse>>> GetProblems(
            [FromQuery] ProblemListQuery query)
        {
            var userId = GetAuthenticatedUserId();
            var result = await _problemService.GetProblemsAsync(query, userId);
            return Ok(result);
        }

        [HttpGet("{slug}")]
        [ProducesResponseType(typeof(ProblemDetailResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProblemDetailResponse>> GetBySlug(string slug)
        {
            var result = await _problemService.GetProblemBySlugAsync(
                slug,
                GetAuthenticatedUserId());
            if (result == null)
                throw new ResourceNotFoundException($"Problem '{slug}' was not found.");

            return Ok(result);
        }

        private Guid? GetAuthenticatedUserId()
        {
            if (User.Identity?.IsAuthenticated != true)
                return null;

            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                     ?? User.FindFirst("sub");

            return claim != null && Guid.TryParse(claim.Value, out var id)
                ? id
                : null;
        }
    }
}
