import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, finalize, map, of, switchMap, takeWhile, tap, timer } from 'rxjs';
import type { Observable } from 'rxjs';

import type { ContentGenerationStatusResponse } from '../../../core/api/admin-generated/models/content-generation-status-response';
import type { GeneratedSuiteReviewResponse } from '../../../core/api/admin-generated/models/generated-suite-review-response';
import type { ProblemDraftResponse } from '../../../core/api/admin-generated/models/problem-draft-response';
import type { ApiProblem } from '../../../core/error/api-problem';
import { createUnknownApiProblem, isApiProblem } from '../../../core/error/api-problem';
import { AuthoringGateway } from './authoring.gateway';
import type {
  AuthoringMetadata,
  HandwrittenCaseInput,
  SignatureInput,
  SourcesInput,
  SuiteQualityPolicyInput,
} from './authoring.models';

interface AuthoringState {
  readonly draft: ProblemDraftResponse | null;
  readonly generation: ContentGenerationStatusResponse | null;
  readonly review: GeneratedSuiteReviewResponse | null;
  readonly busy: boolean;
  readonly problem: ApiProblem | null;
  readonly published: boolean;
}

@Injectable()
export class AuthoringStore {
  private readonly gateway = inject(AuthoringGateway);
  private readonly state = signal<AuthoringState>({
    draft: null,
    generation: null,
    review: null,
    busy: false,
    problem: null,
    published: false,
  });

  readonly draft = computed(() => this.state().draft);
  readonly generation = computed(() => this.state().generation);
  readonly review = computed(() => this.state().review);
  readonly busy = computed(() => this.state().busy);
  readonly problem = computed(() => this.state().problem);
  readonly published = computed(() => this.state().published);

  load(revisionId: string): Observable<ProblemDraftResponse | null> {
    return this.request(this.gateway.get(revisionId), (draft) => ({
      draft,
      published: isPublished(draft.status),
    })).pipe(
      switchMap((draft) =>
        draft !== null && isReady(draft.status)
          ? this.gateway.review(revisionId).pipe(
              tap((review) => this.state.update((state) => ({ ...state, review }))),
              map(() => draft),
              catchError(() => of(draft)),
            )
          : of(draft),
      ),
    );
  }

  create(metadata: AuthoringMetadata): Observable<ProblemDraftResponse | null> {
    return this.request(this.gateway.create(metadata), (draft) => ({
      draft,
      published: false,
    }));
  }

  saveMetadata(
    revisionId: string,
    metadata: AuthoringMetadata,
  ): Observable<ProblemDraftResponse | null> {
    return this.request(this.gateway.updateMetadata(revisionId, metadata), (draft) => ({
      draft,
      review: null,
      published: false,
    }));
  }

  saveSignature(
    revisionId: string,
    value: SignatureInput,
  ): Observable<ProblemDraftResponse | null> {
    return this.request(this.gateway.updateSignature(revisionId, value), (draft) => ({
      draft,
      review: null,
      published: false,
    }));
  }

  saveCases(
    revisionId: string,
    value: readonly HandwrittenCaseInput[],
  ): Observable<ProblemDraftResponse | null> {
    return this.request(this.gateway.updateCases(revisionId, value), (draft) => ({
      draft,
      review: null,
      published: false,
    }));
  }

  saveSources(revisionId: string, value: SourcesInput): Observable<ProblemDraftResponse | null> {
    return this.request(this.gateway.updateSources(revisionId, value), (draft) => ({
      draft,
      review: null,
      published: false,
    }));
  }

  saveQualityPolicy(
    revisionId: string,
    value: SuiteQualityPolicyInput,
  ): Observable<ProblemDraftResponse | null> {
    return this.request(this.gateway.updateQualityPolicy(revisionId, value), (draft) => ({
      draft,
      review: null,
      published: false,
    }));
  }

  generateAndReview(revisionId: string): Observable<GeneratedSuiteReviewResponse | null> {
    return this.monitorGeneration(revisionId, this.gateway.generate(revisionId));
  }

  resumeGenerationAndReview(revisionId: string): Observable<GeneratedSuiteReviewResponse | null> {
    return this.monitorGeneration(revisionId);
  }

  private monitorGeneration(
    revisionId: string,
    start?: Observable<ContentGenerationStatusResponse>,
  ): Observable<GeneratedSuiteReviewResponse | null> {
    this.state.update((state) => ({ ...state, busy: true, problem: null, review: null }));
    const poll = timer(0, 1_200).pipe(
      switchMap(() => this.gateway.generationStatus(revisionId)),
      tap((generation) => this.setGeneration(generation)),
      takeWhile((generation) => !isGenerationFinal(generation.jobStatus), true),
    );
    const generation = start
      ? start.pipe(
          tap((status) => this.setGeneration(status)),
          switchMap(() => poll),
        )
      : poll;
    return generation.pipe(
      switchMap((status) =>
        isGenerationSucceeded(status.jobStatus) ? this.gateway.review(revisionId) : of(null),
      ),
      tap((review) => this.state.update((state) => ({ ...state, review }))),
      catchError((error: unknown) => {
        this.setProblem(error);
        return of(null);
      }),
      finalize(() => this.state.update((state) => ({ ...state, busy: false }))),
    );
  }

  publish(revisionId: string): Observable<boolean> {
    this.state.update((state) => ({ ...state, busy: true, problem: null }));
    return this.gateway.publish(revisionId).pipe(
      map(() => true),
      tap(() => this.state.update((state) => ({ ...state, published: true }))),
      catchError((error: unknown) => {
        this.setProblem(error);
        return of(false);
      }),
      finalize(() => this.state.update((state) => ({ ...state, busy: false }))),
    );
  }

  private request<T>(
    source: Observable<T>,
    update: (value: T) => Partial<AuthoringState>,
  ): Observable<T | null> {
    this.state.update((state) => ({ ...state, busy: true, problem: null }));
    return source.pipe(
      tap((value) => this.state.update((state) => ({ ...state, ...update(value) }))),
      catchError((error: unknown) => {
        this.setProblem(error);
        return of(null);
      }),
      finalize(() => this.state.update((state) => ({ ...state, busy: false }))),
    );
  }

  private setGeneration(generation: ContentGenerationStatusResponse): void {
    this.state.update((state) => ({ ...state, generation }));
  }

  private setProblem(error: unknown): void {
    const problem = isApiProblem(error) ? error : createUnknownApiProblem();
    this.state.update((state) => ({ ...state, problem }));
  }
}

function isReady(status: unknown): boolean {
  return status === 'Ready' || status === 'Published' || status === 2 || status === 3;
}

function isPublished(status: unknown): boolean {
  return status === 'Published' || status === 3;
}

function isGenerationFinal(status: unknown): boolean {
  return status === 'Succeeded' || status === 'Failed' || status === 2 || status === 3;
}

function isGenerationSucceeded(status: unknown): boolean {
  return status === 'Succeeded' || status === 2;
}
