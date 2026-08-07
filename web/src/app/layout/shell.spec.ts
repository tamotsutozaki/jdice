import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { AuthService, CurrentUser } from '../core/auth/auth.service';
import { MARCA } from './marca';
import { Shell } from './shell';

describe('Shell', () => {
  let httpMock: HttpTestingController;
  let logout: ReturnType<typeof vi.fn>;

  function configurar(role: CurrentUser['role']) {
    TestBed.resetTestingModule();

    logout = vi.fn().mockReturnValue(of(undefined));

    TestBed.configureTestingModule({
      imports: [Shell],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            user: signal<CurrentUser | null>({
              id: 'u1',
              email: 'pedro.tozaki@empresa.com',
              role,
            }),
            isAdmin: signal(role === 'Admin'),
            logout,
          },
        },
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);

    const fixture = TestBed.createComponent(Shell);
    fixture.detectChanges();

    return fixture;
  }

  function texto(fixture: ComponentFixture<Shell>): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function rotulosDeNavegacao(fixture: ComponentFixture<Shell>): string[] {
    return Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.topbar-nav .nav-link'),
    ).map((link) => link.textContent?.trim() ?? '');
  }

  afterEach(() => httpMock.verify());

  it('mostra a marca no topo', () => {
    const fixture = configurar('User');

    expect(texto(fixture)).toContain(MARCA.nome);
    expect(texto(fixture)).toContain(MARCA.subtitulo);
  });

  it('esconde a navegação de contas de quem não é administrador', () => {
    const fixture = configurar('User');

    // O adminGuard barraria de qualquer forma; oferecer o link seria um beco
    // sem saída.
    expect(rotulosDeNavegacao(fixture)).toEqual(['Dashboard', 'Modelos', 'Destinatários']);
  });

  it('mostra a navegação de contas para administrador', () => {
    const fixture = configurar('Admin');

    expect(rotulosDeNavegacao(fixture)).toEqual([
      'Dashboard',
      'Modelos',
      'Destinatários',
      'Contas',
    ]);
  });

  it('deriva as iniciais do e-mail de quem está logado', () => {
    const fixture = configurar('User');

    const avatar = (fixture.nativeElement as HTMLElement).querySelector('.user-avatar');

    expect(avatar?.textContent?.trim()).toBe('PT');
  });

  it('encerra a sessão e volta para o login', async () => {
    const fixture = configurar('User');
    const navigateByUrl = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.logout-btn')?.click();

    await fixture.whenStable();

    expect(logout).toHaveBeenCalled();
    expect(navigateByUrl).toHaveBeenCalledWith('/login');
  });
});
