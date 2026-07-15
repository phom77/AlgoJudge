using AlgoJudge.Application.Contracts.Problems;
using AlgoJudge.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AlgoJudge.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProblemsController : ControllerBase
    {
        private readonly IProblemService _problemService;

        public ProblemsController(IProblemService problemService)
        {
            _problemService = problemService;
        }

        [HttpGet]
        public async Task<IActionResult> GetProblems([FromQuery] ProblemListQuery query)
        {
            var userId = GetAuthenticatedUserId();
            if (query.Solved.HasValue && !userId.HasValue)
            {
                return BadRequest(new
                {
                    message = "The solved filter is available only to authenticated users."
                });
            }

            var result = await _problemService.GetProblemsAsync(query, userId);
            return Ok(result);
        }

        [HttpGet("{slug}")]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            var result = await _problemService.GetProblemBySlugAsync(
                slug,
                GetAuthenticatedUserId());
            return result == null ? NotFound() : Ok(result);
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
