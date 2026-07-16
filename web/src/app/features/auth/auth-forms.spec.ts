import { FormBuilder } from '@angular/forms';

import { createLoginForm } from './login/login.form';
import { createRegisterForm } from './register/register.form';

describe('auth forms', () => {
  const builder = new FormBuilder().nonNullable;

  it('matches backend username and password limits for login', () => {
    const form = createLoginForm(builder);

    form.setValue({ userName: 'ab', password: '12345' });
    expect(form.invalid).toBe(true);
    form.setValue({ userName: 'ada', password: '123456' });
    expect(form.valid).toBe(true);
  });

  it('matches registration username, email and full-name validation', () => {
    const form = createRegisterForm(builder);

    form.setValue({
      userName: 'invalid name',
      email: 'invalid',
      fullName: 'A',
      password: '123456',
    });
    expect(form.invalid).toBe(true);
    form.setValue({
      userName: 'ada_1',
      email: 'ada@example.com',
      fullName: 'Ada Lovelace',
      password: '123456',
    });
    expect(form.valid).toBe(true);
  });
});
