import { inject, Injectable } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import type { Observable } from 'rxjs';

import { AntiforgeryService } from '../../../core/api/antiforgery.service';
import { AlgoJudgeAdminApi } from '../../../core/api/admin-generated/algo-judge-admin-api';
import { apiInternalAdminProblemDraftsPost$Json } from '../../../core/api/admin-generated/fn/problem-authoring/api-internal-admin-problem-drafts-post-json';
import { apiInternalAdminProblemDraftsRevisionIdGenerationGet$Json } from '../../../core/api/admin-generated/fn/problem-authoring/api-internal-admin-problem-drafts-revision-id-generation-get-json';
import { apiInternalAdminProblemDraftsRevisionIdGenerationPost$Json } from '../../../core/api/admin-generated/fn/problem-authoring/api-internal-admin-problem-drafts-revision-id-generation-post-json';
import { apiInternalAdminProblemDraftsRevisionIdGet$Json } from '../../../core/api/admin-generated/fn/problem-authoring/api-internal-admin-problem-drafts-revision-id-get-json';
import { apiInternalAdminProblemDraftsRevisionIdHandwrittenCasesPut$Json } from '../../../core/api/admin-generated/fn/problem-authoring/api-internal-admin-problem-drafts-revision-id-handwritten-cases-put-json';
import { apiInternalAdminProblemDraftsRevisionIdMetadataPut$Json } from '../../../core/api/admin-generated/fn/problem-authoring/api-internal-admin-problem-drafts-revision-id-metadata-put-json';
import { apiInternalAdminProblemDraftsRevisionIdPublishPost } from '../../../core/api/admin-generated/fn/problem-authoring/api-internal-admin-problem-drafts-revision-id-publish-post';
import { apiInternalAdminProblemDraftsRevisionIdQualityPolicyPut$Json } from '../../../core/api/admin-generated/fn/problem-authoring/api-internal-admin-problem-drafts-revision-id-quality-policy-put-json';
import { apiInternalAdminProblemDraftsRevisionIdSignaturePut$Json } from '../../../core/api/admin-generated/fn/problem-authoring/api-internal-admin-problem-drafts-revision-id-signature-put-json';
import { apiInternalAdminProblemDraftsRevisionIdSourcesPut$Json } from '../../../core/api/admin-generated/fn/problem-authoring/api-internal-admin-problem-drafts-revision-id-sources-put-json';
import { apiInternalAdminProblemDraftsRevisionIdSuiteReviewGet$Json } from '../../../core/api/admin-generated/fn/problem-authoring/api-internal-admin-problem-drafts-revision-id-suite-review-get-json';
import type { ContentGenerationStatusResponse } from '../../../core/api/admin-generated/models/content-generation-status-response';
import type { GeneratedSuiteReviewResponse } from '../../../core/api/admin-generated/models/generated-suite-review-response';
import type { ProblemDraftResponse } from '../../../core/api/admin-generated/models/problem-draft-response';
import { mapProblemDetails } from '../../../core/api/problem-details.mapper';
import type {
  AuthoringMetadata,
  HandwrittenCaseInput,
  SignatureInput,
  SourcesInput,
  SuiteQualityPolicyInput,
} from './authoring.models';

@Injectable({ providedIn: 'root' })
export class AuthoringGateway {
  private readonly api = inject(AlgoJudgeAdminApi);
  private readonly antiforgery = inject(AntiforgeryService);

  create(metadata: AuthoringMetadata): Observable<ProblemDraftResponse> {
    return this.runUnsafe(
      this.api.invoke(apiInternalAdminProblemDraftsPost$Json, {
        body: metadataBody(metadata),
      }),
    );
  }

  get(revisionId: string): Observable<ProblemDraftResponse> {
    return this.wrap(
      this.api.invoke(apiInternalAdminProblemDraftsRevisionIdGet$Json, { revisionId }),
    );
  }

  updateMetadata(
    revisionId: string,
    metadata: AuthoringMetadata,
  ): Observable<ProblemDraftResponse> {
    return this.runUnsafe(
      this.api.invoke(apiInternalAdminProblemDraftsRevisionIdMetadataPut$Json, {
        revisionId,
        body: metadataBody(metadata),
      }),
    );
  }

  updateSignature(revisionId: string, signature: SignatureInput): Observable<ProblemDraftResponse> {
    return this.runUnsafe(
      this.api.invoke(apiInternalAdminProblemDraftsRevisionIdSignaturePut$Json, {
        revisionId,
        body: {
          signature: {
            className: signature.className,
            methodName: signature.methodName,
            returnType: signature.returnType as unknown as number,
            parameters: signature.parameters.map((parameter) => ({
              name: parameter.name,
              type: parameter.type as unknown as number,
            })),
          },
        },
      }),
    );
  }

  updateCases(
    revisionId: string,
    cases: readonly HandwrittenCaseInput[],
  ): Observable<ProblemDraftResponse> {
    return this.runUnsafe(
      this.api.invoke(apiInternalAdminProblemDraftsRevisionIdHandwrittenCasesPut$Json, {
        revisionId,
        body: {
          cases: cases.map((item) => ({
            name: item.name,
            group: 'handwritten',
            arguments: item.arguments,
          })),
        },
      }),
    );
  }

  updateSources(revisionId: string, sources: SourcesInput): Observable<ProblemDraftResponse> {
    const wrongSolutions = sources.wrongSolution.trim()
      ? [{ name: sources.wrongSolutionName, language: 'cpp17', source: sources.wrongSolution }]
      : [];
    return this.runUnsafe(
      this.api.invoke(apiInternalAdminProblemDraftsRevisionIdSourcesPut$Json, {
        revisionId,
        body: {
          generator: { language: 'csharp', sdkVersion: 1, source: sources.generator },
          inputValidator: { language: 'csharp', sdkVersion: 1, source: sources.validator },
          referenceSolution: { language: 'cpp17', source: sources.referenceSolution },
          wrongSolutions,
        },
      }),
    );
  }

  updateQualityPolicy(
    revisionId: string,
    qualityPolicy: SuiteQualityPolicyInput,
  ): Observable<ProblemDraftResponse> {
    const groupMinimums = [
      ['handwritten', qualityPolicy.minimumHandwrittenCases],
      ['edge', qualityPolicy.minimumEdgeCases],
      ['random', qualityPolicy.minimumRandomCases],
      ['adversarial', qualityPolicy.minimumAdversarialCases],
      ['stress', qualityPolicy.minimumStressCases],
    ] as const;
    return this.runUnsafe(
      this.api.invoke(apiInternalAdminProblemDraftsRevisionIdQualityPolicyPut$Json, {
        revisionId,
        body: {
          qualityPolicy: {
            minimumTestCaseCount: qualityPolicy.minimumTestCaseCount,
            minimumCasesByGroup: groupMinimums
              .filter(([, minimumCaseCount]) => minimumCaseCount > 0)
              .map(([group, minimumCaseCount]) => ({ group, minimumCaseCount })),
            requireEachDeclaredWrongSolutionKilled:
              qualityPolicy.requireEachDeclaredWrongSolutionKilled,
          },
        },
      }),
    );
  }

  generate(revisionId: string): Observable<ContentGenerationStatusResponse> {
    return this.runUnsafe(
      this.api.invoke(apiInternalAdminProblemDraftsRevisionIdGenerationPost$Json, { revisionId }),
    );
  }

  generationStatus(revisionId: string): Observable<ContentGenerationStatusResponse> {
    return this.wrap(
      this.api.invoke(apiInternalAdminProblemDraftsRevisionIdGenerationGet$Json, { revisionId }),
    );
  }

  review(revisionId: string): Observable<GeneratedSuiteReviewResponse> {
    return this.wrap(
      this.api.invoke(apiInternalAdminProblemDraftsRevisionIdSuiteReviewGet$Json, { revisionId }),
    );
  }

  publish(revisionId: string): Observable<void> {
    return this.runUnsafe(
      this.api.invoke(apiInternalAdminProblemDraftsRevisionIdPublishPost, { revisionId }),
    );
  }

  private runUnsafe<T>(source: Observable<T>): Observable<T> {
    return this.antiforgery.ensureToken().pipe(
      switchMap(() => source),
      catchError((error: unknown) => this.rethrowProblem(error, true)),
    );
  }

  private wrap<T>(source: Observable<T>): Observable<T> {
    return source.pipe(catchError((error: unknown) => this.rethrowProblem(error)));
  }

  private rethrowProblem(error: unknown, unsafe = false): Observable<never> {
    const problem = mapProblemDetails(error);
    if (unsafe && problem.code === 'csrf') this.antiforgery.invalidate();
    return throwError(() => problem);
  }
}

function metadataBody(metadata: AuthoringMetadata) {
  return {
    slug: metadata.slug,
    title: metadata.title,
    statementMarkdown: metadata.statementMarkdown,
    constraintsMarkdown: metadata.constraintsMarkdown,
    difficulty: metadata.difficulty as unknown as number,
    timeLimitMs: metadata.timeLimitMs,
    memoryLimitKb: metadata.memoryLimitKb,
    samples: [{ input: metadata.sampleInput, expectedOutput: metadata.sampleOutput }],
  };
}
