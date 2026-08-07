import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';

import { adminGuard, authGuard, guestGuard } from './auth.guard';
import { CurrentUser } from './auth.service';

describe('guards de rota', () => {
  let httpMock: HttpTestingController;

  const admin: CurrentUser = {
    id: '019fdd3f-8c71-7534-a61d-f1e430fe4d80',
    email: 'admin@empresa.com',
    role: 'Admin',
  };

  const comum: CurrentUser = { ...admin, email: 'comum@empresa.com', role: 'User' };

  const rota = {} as ActivatedRouteSnapshot;
  const estado = { url: '/usuarios/novo' } as RouterStateSnapshot;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  /** Executa o guard e resolve a chamada de sessão com o usuário informado. */
  async function executar(
    guard: typeof authGuard,
    sessao: CurrentUser | null,
  ): Promise<boolean | UrlTree> {
    const resultado = TestBed.runInInjectionContext(() => guard(rota, estado));

    const requisicao = httpMock.expectOne('/api/auth/me');

    if (sessao) {
      requisicao.flush(sessao);
    } else {
      requisicao.flush(null, { status: 401, statusText: 'Unauthorized' });
    }

    return (await resultado) as boolean | UrlTree;
  }

  it('authGuard deixa passar quem tem sessão', async () => {
    expect(await executar(authGuard, comum)).toBe(true);
  });

  it('authGuard manda visitante para o login guardando o destino', async () => {
    const resultado = await executar(authGuard, null);

    expect(resultado).toBeInstanceOf(UrlTree);
    // O destino volta na query para devolver a pessoa ao lugar certo depois
    // de entrar, em vez de largá-la numa página inicial genérica.
    expect((resultado as UrlTree).toString()).toContain('destino=%2Fusuarios%2Fnovo');
  });

  it('adminGuard deixa passar administrador', async () => {
    expect(await executar(adminGuard, admin)).toBe(true);
  });

  it('adminGuard barra usuário comum', async () => {
    const resultado = await executar(adminGuard, comum);

    expect(resultado).toBeInstanceOf(UrlTree);
    expect((resultado as UrlTree).toString()).toBe('/');
  });

  it('adminGuard manda visitante para o login', async () => {
    const resultado = await executar(adminGuard, null);

    expect(resultado).toBeInstanceOf(UrlTree);
    expect((resultado as UrlTree).toString()).toContain('/login');
  });

  it('guestGuard tira de tela de login quem já entrou', async () => {
    const resultado = await executar(guestGuard, comum);

    expect(resultado).toBeInstanceOf(UrlTree);
    expect((resultado as UrlTree).toString()).toBe('/');
  });

  it('guestGuard deixa visitante ver o login', async () => {
    expect(await executar(guestGuard, null)).toBe(true);
  });
});
