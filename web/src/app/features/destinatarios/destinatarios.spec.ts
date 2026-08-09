import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AuthService, CurrentUser } from '../../core/auth/auth.service';
import { Destinatario } from '../../core/recipients/recipients.service';
import { Destinatarios } from './destinatarios';

describe('Destinatarios', () => {
  let httpMock: HttpTestingController;

  const pessoas: Destinatario[] = [
    {
      id: 'id-ana',
      email: 'ana@empresa.com',
      nome: 'Ana Souza',
      campos: { empresa: 'Acme', plano: 'Premium' },
      inscrito: true,
      descadastradoEm: null,
      criadoEm: '2026-08-01T12:00:00Z',
    },
    {
      id: 'id-bruno',
      email: 'bruno@empresa.com',
      nome: '',
      campos: {},
      inscrito: false,
      descadastradoEm: '2026-08-05T12:00:00Z',
      criadoEm: '2026-08-02T12:00:00Z',
    },
  ];

  function configurar(role: CurrentUser['role'] = 'User') {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [Destinatarios],
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

  const listasFake = [
    {
      id: 'lista-1',
      nome: 'Clientes',
      descricao: '',
      totalDeMembros: 3,
      totalAtivos: 3,
      criadoEm: '2026-08-01T12:00:00Z',
      atualizadoEm: '2026-08-01T12:00:00Z',
    },
  ];

  function montar(itens: Destinatario[] = pessoas, total = itens.length, listas: unknown[] = []) {
    const fixture = TestBed.createComponent(Destinatarios);
    fixture.detectChanges();

    httpMock
      .expectOne((r) => r.url === '/api/recipients')
      .flush({
        itens,
        total,
        pagina: 1,
        tamanhoDaPagina: 25,
        totalDePaginas: Math.ceil(total / 25),
      });
    httpMock.expectOne('/api/recipient-lists').flush(listas);
    fixture.detectChanges();

    return fixture;
  }

  function texto(fixture: ComponentFixture<Destinatarios>): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  afterEach(() => httpMock.verify());

  it('lista destinatários com campos e situação', () => {
    configurar();
    const fixture = montar();

    expect(texto(fixture)).toContain('ana@empresa.com');
    expect(texto(fixture)).toContain('empresa');
    expect(texto(fixture)).toContain('Acme');
    expect(texto(fixture)).toContain('Recebe');
    expect(texto(fixture)).toContain('Descadastrado em');
  });

  it('descadastra e diz que vale para todos os envios', async () => {
    configurar();
    const fixture = montar();

    const botao = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('button'),
    ).find((b) => b.textContent?.includes('Descadastrar')) as HTMLButtonElement;

    botao.click();

    httpMock.expectOne({ method: 'POST', url: '/api/recipients/id-ana/unsubscribe' }).flush(null);

    httpMock
      .expectOne((r) => r.url === '/api/recipients')
      .flush({
        itens: [],
        total: 0,
        pagina: 1,
        tamanhoDaPagina: 25,
        totalDePaginas: 0,
      });

    await fixture.whenStable();
    fixture.detectChanges();

    // A mensagem tem que dizer o alcance real: não é só sair de uma lista.
    expect(texto(fixture)).toContain('não receberá mais nenhum envio');
  });

  it('não oferece remover para usuário comum', () => {
    configurar('User');
    const fixture = montar();

    const rotulos = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('button'),
    ).map((b) => b.textContent?.trim());

    expect(rotulos).not.toContain('Remover');
  });

  it('oferece remover para administrador', () => {
    configurar('Admin');
    const fixture = montar();

    const rotulos = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('button'),
    ).map((b) => b.textContent?.trim());

    expect(rotulos).toContain('Remover');
  });

  it('explica a diferença entre descadastrar e sair da lista', () => {
    configurar();
    const fixture = montar();

    expect(texto(fixture)).toContain('Descadastrar vale para todas as listas');
  });

  it('mostra a paginação quando há mais de uma página', () => {
    configurar();
    const fixture = montar(pessoas, 60);

    expect(texto(fixture)).toContain('Página 1 de 3');
  });

  it('oferece adicionar à lista quando há listas e um botão de editar', () => {
    configurar();
    const fixture = montar(pessoas, pessoas.length, listasFake);

    const rotulos = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('button, a'),
    ).map((el) => el.textContent?.trim());

    expect(rotulos).toContain('Adicionar à lista');
    expect(rotulos).toContain('Editar');
  });

  it('adiciona um destinatário existente a uma lista escolhida', async () => {
    configurar();
    const fixture = montar(pessoas, pessoas.length, listasFake);

    const abrir = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('button'),
    ).find((b) => b.textContent?.includes('Adicionar à lista')) as HTMLButtonElement;
    abrir.click();
    fixture.detectChanges();

    const select = (fixture.nativeElement as HTMLElement).querySelector(
      'select[name="listaEscolhida"]',
    ) as HTMLSelectElement;
    select.value = select.options[1].value;
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    const confirmar = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.add-lista button'),
    ).find((b) => b.textContent?.includes('Adicionar')) as HTMLButtonElement;
    confirmar.click();

    httpMock
      .expectOne({ method: 'POST', url: '/api/recipient-lists/lista-1/members/id-ana' })
      .flush(null);

    httpMock
      .expectOne((r) => r.url === '/api/recipients')
      .flush({ itens: pessoas, total: 2, pagina: 1, tamanhoDaPagina: 25, totalDePaginas: 1 });

    await fixture.whenStable();
    fixture.detectChanges();

    expect(texto(fixture)).toContain('foi adicionado a Clientes');
  });

  it('avisa quando não consegue carregar', async () => {
    configurar();

    const fixture = TestBed.createComponent(Destinatarios);
    fixture.detectChanges();

    httpMock
      .expectOne((r) => r.url === '/api/recipients')
      .flush(null, { status: 500, statusText: 'Server Error' });
    httpMock.expectOne('/api/recipient-lists').flush([]);

    await fixture.whenStable();
    fixture.detectChanges();

    expect(texto(fixture)).toContain('Não foi possível carregar os destinatários.');
  });
});
