import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AuthService, CurrentUser } from '../../core/auth/auth.service';
import { Status } from './status';

describe('Status', () => {
  let httpMock: HttpTestingController;

  const painel = {
    modelos: 4,
    destinatarios: 250,
    listas: 3,
    disparosAgendados: 2,
    emailsEnviados: 1280,
    enviadosNosUltimos30Dias: 430,
    recentes: [
      {
        id: 'disparo-1',
        nome: 'Boas-vindas de agosto',
        situacao: 'Completed',
        total: 120,
        enviados: 120,
        agendadoPara: '2026-08-05T12:00:00Z',
      },
    ],
  };

  function configurar(role: CurrentUser['role'] = 'User') {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [Status],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            user: signal<CurrentUser | null>({ id: 'u1', email: 'a@b.com', role }),
            isAdmin: signal(role === 'Admin'),
          },
        },
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
  }

  function montar() {
    const fixture = TestBed.createComponent(Status);
    fixture.detectChanges();

    httpMock.expectOne('/api/dashboard').flush(painel);
    fixture.detectChanges();

    return fixture;
  }

  function texto(fixture: ComponentFixture<Status>): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  afterEach(() => httpMock.verify());

  it('mostra números vindos do sistema', () => {
    configurar();
    const fixture = montar();

    // Números do banco, não escritos no código como no projeto original.
    expect(texto(fixture)).toContain('1280');
    expect(texto(fixture)).toContain('430 nos últimos 30 dias');
    expect(texto(fixture)).toContain('250');
  });

  it('lista os disparos recentes', () => {
    configurar();
    const fixture = montar();

    expect(texto(fixture)).toContain('Boas-vindas de agosto');
    expect(texto(fixture)).toContain('120/120');
    expect(texto(fixture)).toContain('Concluído');
  });

  it('convida a criar o primeiro disparo quando não há recentes', () => {
    configurar();

    const fixture = TestBed.createComponent(Status);
    fixture.detectChanges();

    httpMock.expectOne('/api/dashboard').flush({ ...painel, recentes: [] });
    fixture.detectChanges();

    // Antes a seção sumia calada; agora aponta o caminho.
    expect(texto(fixture)).toContain('Nenhum disparo ainda');
    expect(texto(fixture)).toContain('Criar o primeiro');
  });

  it('esconde o atalho de contas de quem não é administrador', () => {
    configurar('User');
    const fixture = montar();

    expect(texto(fixture)).not.toContain('Cadastrar, desativar e reativar');
  });

  it('mostra o atalho de contas para administrador', () => {
    configurar('Admin');
    const fixture = montar();

    expect(texto(fixture)).toContain('Cadastrar, desativar e reativar');
  });

  it('avisa quando não consegue carregar', async () => {
    configurar();

    const fixture = TestBed.createComponent(Status);
    fixture.detectChanges();

    httpMock.expectOne('/api/dashboard').flush(null, { status: 500, statusText: 'Server Error' });

    await fixture.whenStable();
    fixture.detectChanges();

    expect(texto(fixture)).toContain('Não foi possível carregar o painel.');
  });
});
