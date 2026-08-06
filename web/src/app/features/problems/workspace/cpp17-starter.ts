import type {
  FunctionSignature,
  FunctionValueType,
  ProblemDetail,
} from '../data-access/problem.models';

const STDIN_STARTER = `#include <bits/stdc++.h>
using namespace std;

int main() {
  ios::sync_with_stdio(false);
  cin.tie(nullptr);

  return 0;
}
`;

export function createCpp17Starter(problem: ProblemDetail): string {
  if (problem.executionMode !== 'Function' || problem.functionSignature === null) {
    return STDIN_STARTER;
  }

  return createFunctionStarter(problem.functionSignature);
}

function createFunctionStarter(signature: FunctionSignature): string {
  const parameters = signature.parameters
    .map((parameter) => `${cpp17Type(parameter.type)} ${parameter.name}`)
    .join(', ');

  return `#include <bits/stdc++.h>
using namespace std;

class ${signature.className} {
public:
  ${cpp17Type(signature.returnType)} ${signature.methodName}(${parameters}) {
    // Write your solution here.
    return ${defaultValue(signature.returnType)};
  }
};
`;
}

export function cpp17Type(type: FunctionValueType): string {
  return {
    Int32: 'int',
    Int64: 'long long',
    Double: 'double',
    Boolean: 'bool',
    String: 'string',
    Int32Array: 'vector<int>',
    Int64Array: 'vector<long long>',
    DoubleArray: 'vector<double>',
    BooleanArray: 'vector<bool>',
    StringArray: 'vector<string>',
  }[type];
}

function defaultValue(type: FunctionValueType): string {
  return {
    Int32: '0',
    Int64: '0',
    Double: '0.0',
    Boolean: 'false',
    String: '{}',
    Int32Array: '{}',
    Int64Array: '{}',
    DoubleArray: '{}',
    BooleanArray: '{}',
    StringArray: '{}',
  }[type];
}
