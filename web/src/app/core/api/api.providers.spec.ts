import { TestBed } from '@angular/core/testing';

import { provideAlgoJudgeApi } from './api.providers';
import { AlgoJudgeApiConfiguration } from './generated/algo-judge-api-configuration';

describe('provideAlgoJudgeApi', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('uses the current origin by default', () => {
    TestBed.configureTestingModule({ providers: [provideAlgoJudgeApi()] });

    expect(TestBed.inject(AlgoJudgeApiConfiguration).rootUrl).toBe('');
  });

  it('normalizes an application-relative API root', () => {
    TestBed.configureTestingModule({ providers: [provideAlgoJudgeApi('/judge/')] });

    expect(TestBed.inject(AlgoJudgeApiConfiguration).rootUrl).toBe('/judge');
  });

  it('rejects a cross-origin API root', () => {
    expect(() => provideAlgoJudgeApi('https://api.example.com')).toThrowError(
      'AlgoJudge API root URL must be a same-origin path.',
    );
  });
});
