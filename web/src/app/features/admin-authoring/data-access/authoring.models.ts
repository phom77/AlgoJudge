export type FunctionValueTypeName =
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

export interface AuthoringMetadata {
  readonly slug: string;
  readonly title: string;
  readonly statementMarkdown: string;
  readonly constraintsMarkdown: string;
  readonly difficulty: 'Easy' | 'Medium' | 'Hard';
  readonly timeLimitMs: number;
  readonly memoryLimitKb: number;
  readonly sampleInput: string;
  readonly sampleOutput: string;
}

export interface SignatureInput {
  readonly className: string;
  readonly methodName: string;
  readonly returnType: FunctionValueTypeName;
  readonly parameters: readonly {
    readonly name: string;
    readonly type: FunctionValueTypeName;
  }[];
}

export interface HandwrittenCaseInput {
  readonly name: string;
  readonly arguments: Readonly<Record<string, unknown>>;
}

export interface SourcesInput {
  readonly generator: string;
  readonly validator: string;
  readonly referenceSolution: string;
  readonly wrongSolutionName: string;
  readonly wrongSolution: string;
}

export interface SuiteQualityPolicyInput {
  readonly minimumTestCaseCount: number;
  readonly minimumHandwrittenCases: number;
  readonly minimumEdgeCases: number;
  readonly minimumRandomCases: number;
  readonly minimumAdversarialCases: number;
  readonly minimumStressCases: number;
  readonly requireEachDeclaredWrongSolutionKilled: boolean;
}
