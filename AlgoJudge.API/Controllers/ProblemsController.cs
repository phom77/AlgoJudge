using AlgoJudge.Application.DTOs.Problem;
using AlgoJudge.Application.Interfaces;
using Microsoft.AspNetCore.Http;
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

        // POST: api/problems
        [HttpPost]
        public async Task<IActionResult> CreateProblem([FromBody] CreateProblemDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _problemService.CreateProblemAsync(dto);

            return CreatedAtAction(nameof(GetProblems), new { id = result.Id }, result);
        }

        // GET: api/problems
        [HttpGet]
        public async Task<IActionResult> GetProblems([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;
            var result = await _problemService.GetProblemAsync(pageNumber, pageSize);

            return Ok(result);
        }

        // GET: api/problems/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _problemService.GetProblemByIdAsync(id);

            if (result == null) return NotFound(); 

            return Ok(result); 
        }
    }
}
