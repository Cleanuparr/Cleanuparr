import { Injectable, signal } from '@angular/core';
import { Observable, of } from 'rxjs';

export interface User {
  id: string;
  username: string;
}

export interface LoginCredentials {
  username: string;
  password: string;
}

export interface AuthResult {
  success: boolean;
  error?: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly _isAuthenticated = signal(true);
  private readonly _user = signal<User | null>(null);

  readonly isAuthenticated = this._isAuthenticated.asReadonly();
  readonly user = this._user.asReadonly();

  login(_credentials: LoginCredentials): Observable<AuthResult> {
    // Placeholder: always succeeds. Implement real auth later.
    return of({ success: true });
  }

  logout(): void {
    // Placeholder: no-op. Implement real logout later.
  }
}
