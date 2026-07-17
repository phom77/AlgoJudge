import { convertToParamMap } from '@angular/router';

import { readSubmissionQuery, writeSubmissionQuery } from './submission-history-query';
import { DEFAULT_SUBMISSION_QUERY } from './submission.models';

describe('submission history query', () => {
  it('parses problem, verdict and pagination from URL state', () => {
    const query = readSubmissionQuery(
      convertToParamMap({ problemId: '42', status: 'CompileError', page: '3', pageSize: '50' }),
    );

    expect(query).toEqual({ problemId: 42, status: 'CompileError', pageNumber: 3, pageSize: 50 });
  });

  it('normalizes invalid filters without sending invalid backend queries', () => {
    const query = readSubmissionQuery(
      convertToParamMap({ problemId: '0', status: 'Unknown', page: '-1', pageSize: '101' }),
    );

    expect(query).toEqual(DEFAULT_SUBMISSION_QUERY);
  });

  it('omits default pagination when serializing', () => {
    expect(writeSubmissionQuery(DEFAULT_SUBMISSION_QUERY)).toEqual({
      problemId: null,
      status: null,
      page: null,
      pageSize: null,
    });
  });
});
