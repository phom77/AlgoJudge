import type { ParamMap, Params } from '@angular/router';

import {
  DEFAULT_PROBLEM_QUERY,
  PROBLEM_DIFFICULTIES,
  type ProblemCatalogueQuery,
  type ProblemDifficulty,
} from './problem.models';

export function readProblemQuery(
  params: ParamMap,
  isAuthenticated: boolean,
): ProblemCatalogueQuery {
  return {
    search: readSearch(params.get('search')),
    difficulty: readDifficulty(params.get('difficulty')),
    tags: readTags(params.getAll('tags')),
    solved: isAuthenticated ? readSolved(params.get('solved')) : null,
    pageNumber: readInteger(params.get('page'), DEFAULT_PROBLEM_QUERY.pageNumber, 1, 2_147_483_647),
    pageSize: readInteger(params.get('pageSize'), DEFAULT_PROBLEM_QUERY.pageSize, 1, 100),
  };
}

export function writeProblemQuery(query: ProblemCatalogueQuery): Params {
  return {
    search: query.search || null,
    difficulty: query.difficulty,
    tags: query.tags.length > 0 ? query.tags : null,
    solved: query.solved,
    page: query.pageNumber === DEFAULT_PROBLEM_QUERY.pageNumber ? null : query.pageNumber,
    pageSize: query.pageSize === DEFAULT_PROBLEM_QUERY.pageSize ? null : query.pageSize,
  };
}

export function problemQueryKey(query: ProblemCatalogueQuery): string {
  return [
    query.search,
    query.difficulty ?? '',
    query.tags.join(','),
    query.solved === null ? '' : String(query.solved),
    query.pageNumber,
    query.pageSize,
  ].join('|');
}

function readSearch(value: string | null): string {
  return (value ?? '').trim().slice(0, 100);
}

function readDifficulty(value: string | null): ProblemDifficulty | null {
  return PROBLEM_DIFFICULTIES.find((difficulty) => difficulty === value) ?? null;
}

function readTags(values: readonly string[]): readonly string[] {
  const tags = values
    .flatMap((value) => value.split(','))
    .map((value) => value.trim().toLowerCase())
    .filter((value) => value.length > 0 && value.length <= 50);
  return [...new Set(tags)].slice(0, 10);
}

function readSolved(value: string | null): boolean | null {
  if (value === 'true') return true;
  if (value === 'false') return false;
  return null;
}

function readInteger(
  value: string | null,
  fallback: number,
  minimum: number,
  maximum: number,
): number {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= minimum && parsed <= maximum ? parsed : fallback;
}
