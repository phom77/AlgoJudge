using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Application.Interfaces
{
    public interface IGraderService
    {
        Task GradeAsync(Guid submissionId, CancellationToken cancellationToken = default);
    }
}
