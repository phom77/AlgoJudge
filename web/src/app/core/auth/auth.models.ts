export interface AuthUser {
  readonly userName: string;
  readonly email: string;
  readonly expiresAt: string;
}

export interface LoginCredentials {
  readonly userName: string;
  readonly password: string;
}

export interface RegistrationDetails {
  readonly userName: string;
  readonly email: string;
  readonly password: string;
  readonly fullName: string;
}

export type AuthPhase = 'checking' | 'anonymous' | 'authenticated';
