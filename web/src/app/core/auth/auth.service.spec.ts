import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';

import { AuthService, CurrentUser } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  const admin: CurrentUser = {
    id: '019fdd3f-8c71-7534-a61d-f1e430fe4d80',
    email: 'admin@empresa.com',
    role: 'Admin',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('começa sem sessão', () => {
    expect(service.user()).toBeNull();
    expect(service.isLoggedIn()).toBe(false);
  });

  it('carrega o usuário a partir de /auth/me', async () => {
    const promessa = service.ensureSessionLoaded();

    httpMock.expectOne('/api/auth/me').flush(admin);
    await promessa;

    expect(service.user()).toEqual(admin);
    expect(service.isLoggedIn()).toBe(true);
    expect(service.isAdmin()).toBe(true);
  });

  it('trata 401 do /auth/me como ausência de sessão, sem estourar erro', async () => {
    const promessa = service.ensureSessionLoaded();

    httpMock.expectOne('/api/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(await promessa).toBeNull();
    expect(service.isLoggedIn()).toBe(false);
  });

  it('consulta /auth/me uma única vez em chamadas seguidas', async () => {
    const primeira = service.ensureSessionLoaded();
    httpMock.expectOne('/api/auth/me').flush(admin);
    await primeira;

    await service.ensureSessionLoaded();

    // Sem nova requisição pendente: o valor veio do cache.
    httpMock.expectNone('/api/auth/me');
  });

  it('login invalida o cache para que a sessão seja relida', async () => {
    const primeira = service.ensureSessionLoaded();
    httpMock.expectOne('/api/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    await primeira;

    service.login('admin@empresa.com', 'senha-bem-comprida-123').subscribe();
    httpMock.expectOne('/api/auth/login').flush(null);

    const segunda = service.ensureSessionLoaded();
    httpMock.expectOne('/api/auth/me').flush(admin);

    expect(await segunda).toEqual(admin);
  });

  it('logout limpa a sessão em memória', async () => {
    const carga = service.ensureSessionLoaded();
    httpMock.expectOne('/api/auth/me').flush(admin);
    await carga;

    service.logout().subscribe();
    httpMock.expectOne('/api/auth/logout').flush(null);

    expect(service.user()).toBeNull();
    expect(service.isLoggedIn()).toBe(false);
  });
});
