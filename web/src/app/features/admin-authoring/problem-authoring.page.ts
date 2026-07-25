import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EMPTY, switchMap } from 'rxjs';

import type { ProblemDraftResponse } from '../../core/api/admin-generated/models/problem-draft-response';
import { SourceEditorComponent } from '../../shared/ui/code-editor/source-editor.component';
import {
  asDifficulty,
  asValueType,
  firstWrongSolution,
  inferAuthoringStep,
  isGeneratingStatus,
  sourceOrDefault,
} from './authoring-draft.mapper';
import type { AuthoringStep } from './authoring-draft.mapper';
import {
  createCaseControl,
  createCasesForm,
  createMetadataForm,
  createParameterControl,
  createQualityPolicyForm,
  createSignatureForm,
  createSourcesForm,
} from './authoring.forms';
import {
  GENERATOR_PRESET,
  GENERATOR_PRESET_CASE_COUNT,
  REFERENCE_PRESET,
  VALIDATOR_PRESET,
  WRONG_SOLUTION_PRESET,
} from './authoring-presets';
import { AuthoringStore } from './data-access/authoring.store';
import type { FunctionValueTypeName, HandwrittenCaseInput } from './data-access/authoring.models';

@Component({
  selector: 'aj-problem-authoring-page',
  imports: [ReactiveFormsModule, RouterLink, SourceEditorComponent],
  providers: [AuthoringStore],
  templateUrl: './problem-authoring.page.html',
  styleUrl: './problem-authoring.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProblemAuthoringPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly store = inject(AuthoringStore);
  protected readonly step = signal<AuthoringStep>('metadata');
  protected readonly localError = signal<string | null>(null);
  protected readonly metadataForm = createMetadataForm();
  protected readonly signatureForm = createSignatureForm();
  protected readonly casesForm = createCasesForm();
  protected readonly qualityPolicyForm = createQualityPolicyForm();
  protected readonly sourcesForm = createSourcesForm(
    GENERATOR_PRESET,
    VALIDATOR_PRESET,
    REFERENCE_PRESET,
    WRONG_SOLUTION_PRESET,
  );
  protected readonly valueTypes: readonly FunctionValueTypeName[] = [
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
  protected readonly generatorPresetCaseCount = GENERATOR_PRESET_CASE_COUNT;

  constructor() {
    const revisionId = this.route.snapshot.paramMap.get('revisionId');
    if (revisionId !== null) {
      this.store
        .load(revisionId)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe((draft) => {
          if (draft === null) return;
          this.hydrate(draft);
          if (isGeneratingStatus(draft.status))
            this.store
              .resumeGenerationAndReview(draft.revisionId ?? revisionId)
              .pipe(takeUntilDestroyed(this.destroyRef))
              .subscribe();
        });
    }
  }

  protected selectStep(step: AuthoringStep): void {
    if (this.store.draft() !== null || step === 'metadata') this.step.set(step);
  }

  protected saveMetadata(): void {
    if (this.metadataForm.invalid) return this.metadataForm.markAllAsTouched();
    const revisionId = this.revisionId();
    const request =
      revisionId === null
        ? this.store.create(this.metadataForm.getRawValue())
        : this.store.saveMetadata(revisionId, this.metadataForm.getRawValue());
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((draft) => {
      if (draft?.revisionId) {
        if (revisionId === null)
          void this.router.navigate(['/admin/problems', draft.revisionId, 'author']);
        this.step.set('signature');
      }
    });
  }

  protected saveSignature(): void {
    const revisionId = this.revisionId();
    if (revisionId === null || this.signatureForm.invalid)
      return this.signatureForm.markAllAsTouched();
    this.store
      .saveSignature(revisionId, this.signatureForm.getRawValue())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((draft) => {
        if (draft !== null) this.step.set('cases');
      });
  }

  protected saveCases(): void {
    const revisionId = this.revisionId();
    if (revisionId === null || this.casesForm.invalid) return this.casesForm.markAllAsTouched();
    let cases: HandwrittenCaseInput[];
    try {
      cases = this.casesForm.controls.cases.getRawValue().map((item) => ({
        name: item.name,
        arguments: JSON.parse(item.argumentsJson) as Readonly<Record<string, unknown>>,
      }));
      this.localError.set(null);
    } catch {
      this.localError.set('Every handwritten testcase must contain a valid JSON object.');
      return;
    }
    this.store
      .saveCases(revisionId, cases)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((draft) => {
        if (draft !== null) this.step.set('sources');
      });
  }

  protected saveSources(): void {
    const revisionId = this.revisionId();
    if (revisionId === null || this.sourcesForm.invalid) return this.sourcesForm.markAllAsTouched();
    this.store
      .saveSources(revisionId, this.sourcesForm.getRawValue())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((draft) => {
        if (draft !== null) this.step.set('review');
      });
  }

  protected generate(): void {
    const revisionId = this.revisionId();
    if (revisionId === null || this.qualityPolicyForm.invalid)
      return this.qualityPolicyForm.markAllAsTouched();
    this.store
      .saveQualityPolicy(revisionId, this.qualityPolicyForm.getRawValue())
      .pipe(
        switchMap((draft) => (draft === null ? EMPTY : this.store.generateAndReview(revisionId))),
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe();
  }

  protected saveQualityPolicy(): void {
    const revisionId = this.revisionId();
    if (revisionId === null || this.qualityPolicyForm.invalid)
      return this.qualityPolicyForm.markAllAsTouched();
    this.store
      .saveQualityPolicy(revisionId, this.qualityPolicyForm.getRawValue())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe();
  }

  protected publish(): void {
    const revisionId = this.revisionId();
    if (revisionId !== null)
      this.store.publish(revisionId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
  }

  protected addParameter(): void {
    this.signatureForm.controls.parameters.push(createParameterControl());
  }
  protected removeParameter(index: number): void {
    this.signatureForm.controls.parameters.removeAt(index);
  }
  protected addCase(): void {
    this.casesForm.controls.cases.push(
      createCaseControl(`case-${this.casesForm.controls.cases.length + 1}`, '{}'),
    );
  }
  protected removeCase(index: number): void {
    this.casesForm.controls.cases.removeAt(index);
  }
  protected applyGeneratorPreset(): void {
    this.sourcesForm.controls.generator.setValue(GENERATOR_PRESET);
  }

  protected generationDuration(): string {
    const generation = this.store.generation();
    if (!generation?.startedAt) return '—';
    const end = generation.finishedAt ? Date.parse(generation.finishedAt) : Date.now();
    return `${Math.max(0, end - Date.parse(generation.startedAt))} ms`;
  }

  protected entries(
    value: Record<string, number | string> | undefined,
  ): readonly [string, number | string][] {
    return Object.entries(value ?? {});
  }

  private revisionId(): string | null {
    return this.store.draft()?.revisionId ?? null;
  }

  private hydrate(draft: ProblemDraftResponse): void {
    this.hydrateMetadata(draft);
    this.hydrateSignature(draft);
    this.hydrateCases(draft);
    this.hydrateSources(draft);
    this.hydrateQualityPolicy(draft);
    this.step.set(inferAuthoringStep(draft));
  }

  private hydrateMetadata(draft: ProblemDraftResponse): void {
    const sample = draft.samples?.[0];
    this.metadataForm.patchValue({
      slug: draft.slug ?? '',
      title: draft.title ?? '',
      statementMarkdown: draft.statementMarkdown ?? '',
      constraintsMarkdown: draft.constraintsMarkdown ?? '',
      difficulty: asDifficulty(draft.difficulty),
      timeLimitMs: Number(draft.timeLimitMs ?? 1000),
      memoryLimitKb: Number(draft.memoryLimitKb ?? 262144),
      sampleInput: sample?.input ?? '',
      sampleOutput: sample?.expectedOutput ?? '',
    });
  }

  private hydrateSignature(draft: ProblemDraftResponse): void {
    const signature = draft.definition?.functionSignature;
    if (!signature?.className) return;
    this.signatureForm.controls.parameters.clear();
    for (const parameter of signature.parameters ?? [])
      this.signatureForm.controls.parameters.push(
        createParameterControl(parameter.name ?? '', asValueType(parameter.type)),
      );
    this.signatureForm.patchValue({
      className: signature.className,
      methodName: signature.methodName ?? '',
      returnType: asValueType(signature.returnType),
    });
  }

  private hydrateCases(draft: ProblemDraftResponse): void {
    const cases = draft.definition?.handwrittenCases ?? [];
    if (cases.length > 0) {
      this.casesForm.controls.cases.clear();
      for (const item of cases)
        this.casesForm.controls.cases.push(
          createCaseControl(item.name ?? '', JSON.stringify(item.arguments ?? {}, null, 2)),
        );
    }
  }

  private hydrateSources(draft: ProblemDraftResponse): void {
    const definition = draft.definition;
    if (!definition) return;
    const wrongSolution = firstWrongSolution(definition.wrongSolutions, WRONG_SOLUTION_PRESET);
    this.sourcesForm.patchValue({
      generator: sourceOrDefault(definition.generator, GENERATOR_PRESET),
      validator: sourceOrDefault(definition.inputValidator, VALIDATOR_PRESET),
      referenceSolution: sourceOrDefault(definition.referenceSolution, REFERENCE_PRESET),
      wrongSolutionName: wrongSolution.name,
      wrongSolution: wrongSolution.source,
    });
  }

  private hydrateQualityPolicy(draft: ProblemDraftResponse): void {
    const policy = draft.definition?.qualityPolicy;
    const groupMinimum = (group: string) =>
      Number(
        policy?.minimumCasesByGroup?.find((item) => item.group === group)?.minimumCaseCount ??
          (group === 'handwritten' ? 1 : 0),
      );
    this.qualityPolicyForm.patchValue({
      minimumTestCaseCount: Number(policy?.minimumTestCaseCount ?? 1),
      minimumHandwrittenCases: groupMinimum('handwritten'),
      minimumEdgeCases: groupMinimum('edge'),
      minimumRandomCases: groupMinimum('random'),
      minimumAdversarialCases: groupMinimum('adversarial'),
      minimumStressCases: groupMinimum('stress'),
      requireEachDeclaredWrongSolutionKilled:
        policy?.requireEachDeclaredWrongSolutionKilled !== false,
    });
  }
}
