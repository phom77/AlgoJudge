using AlgoJudge.Application.DTOs.Leaderboard;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.Interfaces
{
    public interface ILeaderboardRepository
    {
        Task<IEnumerable<LeaderboardEntryDto>> GetGlobalLeaderboardAsync(int pageNumber, int pageSize);
        Task<int> GetTotalStudentCountAsync();
    }
}
