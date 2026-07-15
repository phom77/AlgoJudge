using AlgoJudge.Application.DTOs.Common;
using AlgoJudge.Application.DTOs.Leaderboard;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.Interfaces
{
    public interface ILeaderboardService
    {
        Task<PagedResult<LeaderboardEntryDto>> GetGlobalLeaderboardAsync(int pageNumber, int pageSize);
    }
}
