using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Domain.Enums
{
    public enum SubmissionStatus
    {
        Pending = 0,
        Judging = 1,
        Accepted = 2,
        WrongAnser = 3,
        TimeLimitExceeded = 4,
        MemoryLimitExceeded = 5,
        ComplieError = 6,
        RunTimeError = 7
    }
}
