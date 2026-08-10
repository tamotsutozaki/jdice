import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, firstValueFrom, of, tap } from 'rxjs';

export type UserRole = 'Admin' | 'User';

export interface CurrentUser {
  id: string;
  email: string;
  role: UserRole;
}

/**
 * O token vive num cookie httpOnly, então o JavaScript não consegue lê-lo nem
 * saber quem está logado a partir dele. Quem responde essa pergunta é a API,
 * via /auth/me — e o resultado fica em cache aqui para não repetir a chamada a
 * cada navegação.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly currentUser = signal<CurrentUser | null>(null);
  private sessionResolved = false;

  readonly user = this.currentUser.asReadonly();
  readonly isLoggedIn = computed(() => this.currentUser() !== null);
  readonly isAdmin = computed(() => this.currentUser()?.role === 'Admin');

  login(email: string, senha: string): Observable<void> {
    return this.http.post<void>('/api/auth/login', { email, senha }).pipe(
      tap(() => {
        // Força a próxima verificação a consultar a API: o usuário mudou.
        this.sessionResolved = false;
      }),
    );
  }

  /** Troca a própria senha. Exige a atual — o backend recusa se não conferir. */
  trocarSenha(senhaAtual: string, novaSenha: string): Observable<void> {
    return this.http.post<void>('/api/auth/me/password', { senhaAtual, novaSenha });
  }

  logout(): Observable<void> {
    return this.http.post<void>('/api/auth/logout', {}).pipe(
      tap(() => {
        this.currentUser.set(null);
        this.sessionResolved = true;
      }),
    );
  }

  /** Resolve a sessão uma única vez; chamadas seguintes usam o valor em cache. */
  async ensureSessionLoaded(): Promise<CurrentUser | null> {
    if (this.sessionResolved) {
      return this.currentUser();
    }

    const user = await firstValueFrom(
      this.http.get<CurrentUser>('/api/auth/me').pipe(catchError(() => of(null))),
    );

    this.currentUser.set(user);
    this.sessionResolved = true;

    return user;
  }

  /** Chamado pelo interceptor quando a API responde 401: a sessão acabou. */
  clearSession(): void {
    this.currentUser.set(null);
    this.sessionResolved = true;
  }
}
