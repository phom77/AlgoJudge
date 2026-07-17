import { convertToParamMap } from '@angular/router';

import { problemQueryKey, readProblemQuery, writeProblemQuery } from './problem-query';
import { DEFAULT_PROBLEM_QUERY } from './problem.models';

describe('problem query', () => {
  it('parses validated filters and repeated tags from URL parameters', () => {
    const query = readProblemQuery(
      convertToParamMap({
        search: ' graph ',
        difficulty: 'Hard',
        tags: ['Graph', 'dynamic-programming'],
        solved: 'false',
        page: '3',
        pageSize: '50',
      }),
      true,
    );

    expect(query).toEqual({
      search: 'graph',
      difficulty: 'Hard',
      tags: ['graph', 'dynamic-programming'],
      solved: false,
      pageNumber: 3,
      pageSize: 50,
    });
  });

  it('drops solved state for anonymous users and normalizes invalid pagination', () => {
    const query = readProblemQuery(
      convertToParamMap({ solved: 'true', page: '0', pageSize: '500' }),
      false,
    );

    expect(query.solved).toBeNull();
    expect(query.pageNumber).toBe(DEFAULT_PROBLEM_QUERY.pageNumber);
    expect(query.pageSize).toBe(DEFAULT_PROBLEM_QUERY.pageSize);
  });

  it('omits defaults when serializing and creates stable comparison keys', () => {
    const params = writeProblemQuery(DEFAULT_PROBLEM_QUERY);

    expect(params).toEqual({
      search: null,
      difficulty: null,
      tags: null,
      solved: null,
      page: null,
      pageSize: null,
    });
    expect(problemQueryKey(DEFAULT_PROBLEM_QUERY)).toBe(
      problemQueryKey({ ...DEFAULT_PROBLEM_QUERY }),
    );
  });
});
