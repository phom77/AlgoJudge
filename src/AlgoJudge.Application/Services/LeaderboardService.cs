using AlgoJudge.Application.DTOs.Common;
using AlgoJudge.Application.DTOs.Leaderboard;
using AlgoJudge.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.Services
{
    public class LeaderboardService : ILeaderboardService
    {
        private readonly ILeaderboardRepository _leaderboardRepository;

        public LeaderboardService(ILeaderboardRepository leaderboardRepository)
        {
            _leaderboardRepository = leaderboardRepository;
        }

        public async Task<PagedResult<LeaderboardEntryDto>> GetGlobalLeaderboardAsync(
            int pageNumber, int pageSize)
        {
            var entries = (await _leaderboardRepository
                .GetGlobalLeaderboardAsync(pageNumber, pageSize))
                .ToList();

            var startRank = (pageNumber - 1) * pageSize + 1;
            for (var i = 0; i < entries.Count; i++)
                entries[i].Rank = startRank + i;

            var totalCount = await _leaderboardRepository.GetTotalStudentCountAsync();

            return new PagedResult<LeaderboardEntryDto>
            {
                Items = entries,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }
    }
}
