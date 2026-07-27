import type { AdminProblemListItemResponse } from '../../../core/api/admin-generated/models/admin-problem-list-item-response';

export type AdminProblemStatus = 'all' | 'draft' | 'published' | 'archived';

export interface AdminProblemQuery {
  readonly search: string;
  readonly status: AdminProblemStatus;
}

export interface AdminProblemPage {
  readonly items: readonly AdminProblemListItemResponse[];
  readonly totalCount: number;
}

export const INITIAL_ADMIN_PROBLEM_QUERY: AdminProblemQuery = {
  search: '',
  status: 'all',
};

export function toProblemStatus(value: AdminProblemStatus): number | undefined {
  return value === 'all' ? undefined : { draft: 1, published: 2, archived: 3 }[value];
}

export function problemStatusLabel(value: unknown): string {
  return normalizeEnumLabel(value, { 1: 'Draft', 2: 'Published', 3: 'Archived' });
}

export function revisionStatusLabel(value: unknown): string {
  return normalizeEnumLabel(value, {
    0: 'Draft',
    1: 'Generating',
    2: 'Ready',
    3: 'Published',
  });
}

export function isProblemStatus(
  value: unknown,
  expected: 'Draft' | 'Published' | 'Archived',
): boolean {
  return problemStatusLabel(value) === expected;
}

function normalizeEnumLabel(value: unknown, labels: Readonly<Record<number, string>>): string {
  if (typeof value === 'string') {
    const normalized = value.trim().toLowerCase();
    const stringMatch = Object.values(labels).find((label) => label.toLowerCase() === normalized);
    if (stringMatch) return stringMatch;
  }

  return labels[Number(value)] ?? 'Unknown';
}
