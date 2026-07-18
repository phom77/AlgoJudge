import type { PagedResponseOfProblemListItemResponse } from '../../../core/api/generated/models/paged-response-of-problem-list-item-response';
import type { ProblemDetailResponse } from '../../../core/api/generated/models/problem-detail-response';
import type { ProblemListItemResponse } from '../../../core/api/generated/models/problem-list-item-response';
import type { ProblemSampleResponse } from '../../../core/api/generated/models/problem-sample-response';
import type { TagResponse } from '../../../core/api/generated/models/tag-response';
import type {
  ProblemDetail,
  FunctionSignature,
  FunctionValueType,
  ProblemDifficulty,
  ProblemListItem,
  ProblemPage,
  ProblemSample,
  ProblemTag,
} from './problem.models';

export function mapProblemPage(response: PagedResponseOfProblemListItemResponse): ProblemPage {
  return {
    items: (response.items ?? []).map(mapProblemListItem),
    pageNumber: readNonNegativeInteger(response.pageNumber, 1),
    pageSize: readNonNegativeInteger(response.pageSize, 20),
    totalCount: readNonNegativeInteger(response.totalCount, 0),
    totalPages: readNonNegativeInteger(response.totalPages, 0),
  };
}

export function mapProblemDetail(response: ProblemDetailResponse): ProblemDetail {
  return {
    ...mapProblemListItem(response),
    statementMarkdown: readText(response.statementMarkdown),
    constraintsMarkdown: readText(response.constraintsMarkdown),
    timeLimitMs: readNonNegativeInteger(response.timeLimitMs, 0),
    memoryLimitKb: readNonNegativeInteger(response.memoryLimitKb, 0),
    judgeVersion: readNonNegativeInteger(response.judgeVersion, 0),
    executionMode: Number(response.executionMode) === 1 ? 'Function' : 'StdinStdout',
    functionSignature: mapFunctionSignature(response.functionSignature),
    publishedAt: readTimestamp(response.publishedAt),
    samples: (response.samples ?? [])
      .map(mapSample)
      .sort((left, right) => left.ordinal - right.ordinal),
  };
}

const FUNCTION_TYPES: readonly FunctionValueType[] = [
  'Int32',
  'Int64',
  'Double',
  'Boolean',
  'String',
  'Int32Array',
  'Int64Array',
  'DoubleArray',
  'BooleanArray',
  'StringArray',
];

function mapFunctionSignature(
  value: ProblemDetailResponse['functionSignature'],
): FunctionSignature | null {
  if (!value) return null;
  const readType = (type: unknown): FunctionValueType => {
    if (typeof type === 'string' && FUNCTION_TYPES.includes(type as FunctionValueType))
      return type as FunctionValueType;
    const mapped = FUNCTION_TYPES[Number(type)];
    if (!mapped) throw new Error('The function signature contains an invalid value type.');
    return mapped;
  };
  return {
    className: readRequiredText(value.className, 'function className'),
    methodName: readRequiredText(value.methodName, 'function methodName'),
    returnType: readType(value.returnType),
    parameters: (value.parameters ?? []).map((parameter) => ({
      name: readRequiredText(parameter.name, 'function parameter name'),
      type: readType(parameter.type),
    })),
  };
}

function mapProblemListItem(response: ProblemListItemResponse): ProblemListItem {
  return {
    id: readNonNegativeInteger(response.id, 0),
    slug: readRequiredText(response.slug, 'slug'),
    title: readRequiredText(response.title, 'title'),
    difficulty: readDifficulty(response.difficulty),
    tags: (response.tags ?? []).map(mapTag).filter((tag): tag is ProblemTag => tag !== null),
    isSolved: typeof response.isSolved === 'boolean' ? response.isSolved : null,
  };
}

function mapTag(response: TagResponse): ProblemTag | null {
  const slug = readText(response.slug).toLowerCase();
  if (slug.length === 0) return null;
  return { slug, name: readText(response.name) || slug };
}

function mapSample(response: ProblemSampleResponse): ProblemSample {
  return {
    ordinal: readNonNegativeInteger(response.ordinal, 0),
    input: response.input ?? '',
    expectedOutput: response.expectedOutput ?? '',
    explanation: readNullableText(response.explanation),
  };
}

function readDifficulty(value: unknown): ProblemDifficulty {
  if (value === 'Easy' || value === 1) return 'Easy';
  if (value === 'Medium' || value === 2) return 'Medium';
  if (value === 'Hard' || value === 3) return 'Hard';
  throw new Error('The problem response contains an invalid difficulty.');
}

function readRequiredText(value: unknown, field: string): string {
  const text = readText(value);
  if (text.length === 0) throw new Error(`The problem response is missing ${field}.`);
  return text;
}

function readText(value: unknown): string {
  return typeof value === 'string' ? value.trim() : '';
}

function readNullableText(value: unknown): string | null {
  const text = readText(value);
  return text.length > 0 ? text : null;
}

function readTimestamp(value: unknown): string | null {
  return typeof value === 'string' && !Number.isNaN(Date.parse(value)) ? value : null;
}

function readNonNegativeInteger(value: unknown, fallback: number): number {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= 0 ? parsed : fallback;
}
