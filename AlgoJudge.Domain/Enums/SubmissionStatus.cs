using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Domain.Enums
{
    public enum SubmissionStatus
    {
        Pending = 1,          
        Compiling = 2,       
        Accepted = 3,        
        WrongAnswer = 4,     
        TimeLimitExceeded = 5,
        MemoryLimitExceeded = 6,
        CompileError = 7,    
        RuntimeError = 8
    }
}
