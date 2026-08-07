import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AuthService, CurrentUser } from '../../core/auth/auth.service';
import { UsuarioDaLista } from '../../core/auth/users.service';
import { Usuarios } from './usuarios';

describe('Usuarios', () => {
  let httpMock: HttpTestingController;

  const eu: CurrentUser = {
    id: 'id-do-admin',
    email: 'admin@empresa.com',
    role: 'Admin',
  };

  const lista: UsuarioDaLista[] = [
    {
      id: 'id-do-admin',
      email: 'admin@empresa.com',
      role: 'Admin',
      ativo: true,
      criadoEm: '2026-08-01T12:00:00Z',
      desativadoEm: null,
    },
    {
      id: 'id-da-maria',
      email: 'maria@empresa.com',
      role: 'User',
      ativo: true,
      criadoEm: '2026-08-05T12:00:00Z',
      desativadoEm: null,
    },
    {
      id: 'id-do-joao',
      email: 'joao@empresa.com',
      role: 'User',
      ativo: false,
      criadoEm: '2026-08-03T12:00:00Z',
      desativadoEm: '2026-08-06T12:00:00Z',
    },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Usuarios],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        // Stub para controlar quem está logado sem passar pelo /auth/me.
        { provide: AuthService, useValue: { user: signal<CurrentUser | null>(eu) } },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  function montar(usuarios: UsuarioDaLista[] = lista) {
    const fixture = TestBed.createComponent(Usuarios);
    fixture.detectChanges();

    httpMock.expectOne('/api/auth/users').flush(usuarios);
    fixture.detectChanges();

    return fixture;
  }

  function texto(fixture: ComponentFixture<Usuarios>): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function linhas(fixture: ComponentFixture<Usuarios>): HTMLTableRowElement[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr'));
  }

  it('lista as contas com situação e perfil', () => {
    const fixture = montar();

    expect(linhas(fixture)).toHaveLength(3);
    expect(texto(fixture)).toContain('maria@empresa.com');
    expect(texto(fixture)).toContain('Ativa');
    expect(texto(fixture)).toContain('Desativada em');
  });

  it('conta quantas estão ativas', () => {
    const fixture = montar();

    expect(texto(fixture)).toContain('3 contas cadastradas, 2 ativas');
  });

  it('não oferece desativar para a própria conta', () => {
    const fixture = montar();

    const minhaLinha = linhas(fixture)[0];

    // Quem se desativasse ficaria trancado para fora; o servidor recusa, e a
    // tela nem chega a oferecer.
    expect(minhaLinha.textContent).toContain('você');
    expect(minhaLinha.querySelector('button')).toBeNull();
  });

  it('oferece reativar, não desativar, para conta já desativada', () => {
    const fixture = montar();

    expect(linhas(fixture)[2].querySelector('button')?.textContent).toContain('Reativar');
  });

  it('reativa sem pedir confirmação', async () => {
    const fixture = montar();

    // Devolver acesso é reversível com um clique, ao contrário de tirá-lo:
    // não faz sentido exigir confirmação.
    linhas(fixture)[2].querySelector('button')?.click();

    httpMock
      .expectOne({ method: 'POST', url: '/api/auth/users/id-do-joao/reactivate' })
      .flush(null);

    httpMock
      .expectOne('/api/auth/users')
      .flush(
        lista.map((u) => (u.id === 'id-do-joao' ? { ...u, ativo: true, desativadoEm: null } : u)),
      );

    await fixture.whenStable();
    fixture.detectChanges();

    expect(texto(fixture)).toContain('joao@empresa.com voltou a ter acesso.');
  });

  it('pede confirmação antes de desativar', () => {
    const fixture = montar();

    linhas(fixture)[1].querySelector('button')?.click();
    fixture.detectChanges();

    // Nenhuma chamada foi disparada só por clicar em "Desativar".
    httpMock.expectNone('/api/auth/users/id-da-maria');
    expect(linhas(fixture)[1].textContent).toContain('Confirmar');
  });

  it('desativa após confirmar e recarrega a lista', async () => {
    const fixture = montar();

    linhas(fixture)[1].querySelector('button')?.click();
    fixture.detectChanges();

    const botoes = Array.from(linhas(fixture)[1].querySelectorAll('button'));
    botoes.find((botao) => botao.textContent?.includes('Confirmar'))?.click();

    httpMock.expectOne({ method: 'DELETE', url: '/api/auth/users/id-da-maria' }).flush(null);

    const recarga = httpMock.expectOne('/api/auth/users');
    recarga.flush(lista.map((u) => (u.id === 'id-da-maria' ? { ...u, ativo: false } : u)));

    await fixture.whenStable();
    fixture.detectChanges();

    expect(texto(fixture)).toContain('maria@empresa.com não pode mais entrar.');
  });

  it('cancelar não dispara nada', () => {
    const fixture = montar();

    linhas(fixture)[1].querySelector('button')?.click();
    fixture.detectChanges();

    const botoes = Array.from(linhas(fixture)[1].querySelectorAll('button'));
    botoes.find((botao) => botao.textContent?.includes('Cancelar'))?.click();
    fixture.detectChanges();

    httpMock.expectNone({ method: 'DELETE', url: '/api/auth/users/id-da-maria' });
  });

  it('mostra o motivo quando o servidor recusa a desativação', async () => {
    const fixture = montar();

    linhas(fixture)[1].querySelector('button')?.click();
    fixture.detectChanges();

    const botoes = Array.from(linhas(fixture)[1].querySelectorAll('button'));
    botoes.find((botao) => botao.textContent?.includes('Confirmar'))?.click();

    httpMock
      .expectOne({ method: 'DELETE', url: '/api/auth/users/id-da-maria' })
      .flush(
        { detail: 'Não é possível desativar o último administrador ativo.' },
        { status: 409, statusText: 'Conflict' },
      );

    await fixture.whenStable();
    fixture.detectChanges();

    // O motivo vem do servidor: "último administrador" e "própria conta"
    // pedem explicações diferentes para quem está operando.
    expect(texto(fixture)).toContain('último administrador ativo');
  });

  it('avisa quando não consegue carregar a lista', async () => {
    const fixture = TestBed.createComponent(Usuarios);
    fixture.detectChanges();

    httpMock.expectOne('/api/auth/users').flush(null, { status: 500, statusText: 'Server Error' });

    await fixture.whenStable();
    fixture.detectChanges();

    expect(texto(fixture)).toContain('Não foi possível carregar as contas.');
  });
});
