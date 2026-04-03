using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.DTOs.Leaderboard
{
    public class LeaderboardEntryDto
    {
        public int Rank { get; set; }
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int TotalScore { get; set; }
        public int SolvedCount { get; set; }
    }
}
