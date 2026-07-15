using AlgoJudge.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

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
        public async Task<IActionResult> GetProblems(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var result = await _problemService.GetProblemAsync(pageNumber, pageSize);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _problemService.GetProblemByIdAsync(id);
            return result == null ? NotFound() : Ok(result);
        }
    }
}
