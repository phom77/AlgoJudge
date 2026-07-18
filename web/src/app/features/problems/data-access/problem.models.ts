export const PROBLEM_DIFFICULTIES = ['Easy', 'Medium', 'Hard'] as const;

export type ProblemDifficulty = (typeof PROBLEM_DIFFICULTIES)[number];

export interface ProblemTag {
  readonly name: string;
  readonly slug: string;
}

export interface ProblemListItem {
  readonly id: number;
  readonly slug: string;
  readonly title: string;
  readonly difficulty: ProblemDifficulty;
  readonly tags: readonly ProblemTag[];
  readonly isSolved: boolean | null;
}

export interface ProblemPage {
  readonly items: readonly ProblemListItem[];
  readonly pageNumber: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
}

export interface ProblemSample {
  readonly ordinal: number;
  readonly input: string;
  readonly expectedOutput: string;
  readonly explanation: string | null;
}

export type ProblemExecutionMode = 'StdinStdout' | 'Function';
export type FunctionValueType =
  | 'Int32'
  | 'Int64'
  | 'Double'
  | 'Boolean'
  | 'String'
  | 'Int32Array'
  | 'Int64Array'
  | 'DoubleArray'
  | 'BooleanArray'
  | 'StringArray';
export interface FunctionParameter {
  readonly name: string;
  readonly type: FunctionValueType;
}
export interface FunctionSignature {
  readonly className: string;
  readonly methodName: string;
  readonly returnType: FunctionValueType;
  readonly parameters: readonly FunctionParameter[];
}

export interface ProblemDetail extends ProblemListItem {
  readonly statementMarkdown: string;
  readonly constraintsMarkdown: string;
  readonly timeLimitMs: number;
  readonly memoryLimitKb: number;
  readonly judgeVersion: number;
  readonly executionMode: ProblemExecutionMode;
  readonly functionSignature: FunctionSignature | null;
  readonly publishedAt: string | null;
  readonly samples: readonly ProblemSample[];
}

export interface ProblemCatalogueQuery {
  readonly search: string;
  readonly difficulty: ProblemDifficulty | null;
  readonly tags: readonly string[];
  readonly solved: boolean | null;
  readonly pageNumber: number;
  readonly pageSize: number;
}

export const DEFAULT_PROBLEM_QUERY: ProblemCatalogueQuery = {
  search: '',
  difficulty: null,
  tags: [],
  solved: null,
  pageNumber: 1,
  pageSize: 20,
};
