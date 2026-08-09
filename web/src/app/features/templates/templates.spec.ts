import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AuthService, CurrentUser } from '../../core/auth/auth.service';
import { TemplateDaLista } from '../../core/templates/templates.service';
import { Templates } from './templates';

describe('Templates', () => {
  let httpMock: HttpTestingController;

  const lista: TemplateDaLista[] = [
    {
      id: 'id-boas-vindas',
      nome: 'Boas-vindas',
      categoria: 'Onboarding',
      tags: ['novo'],
      totalDeVersoes: 3,
      versaoAtual: 3,
      variaveis: ['nome'],
      arquivado: false,
      atualizadoEm: '2026-08-05T12:00:00Z',
    },
    {
      id: 'id-antigo',
      nome: 'Promoção antiga',
      categoria: 'Marketing',
      tags: [],
      totalDeVersoes: 1,
      versaoAtual: 1,
      variaveis: [],
      arquivado: true,
      atualizadoEm: '2026-07-01T12:00:00Z',
    },
  ];

  function configurar(role: CurrentUser['role']) {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [Templates],
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

  function montar(itens: TemplateDaLista[] = lista) {
    const fixture = TestBed.createComponent(Templates);
    fixture.detectChanges();

    httpMock.expectOne((r) => r.url === '/api/templates').flush(itens);
    httpMock.expectOne('/api/templates/categories').flush(['Onboarding', 'Marketing']);
    httpMock.expectOne('/api/templates/tags').flush(['novo', 'cliente']);
    fixture.detectChanges();

    return fixture;
  }

  function texto(fixture: ComponentFixture<Templates>): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  afterEach(() => httpMock.verify());

  it('lista os modelos com versões e variáveis', () => {
    configurar('User');
    const fixture = montar();

    expect(texto(fixture)).toContain('Boas-vindas');
    expect(texto(fixture)).toContain('3 versões');
    expect(texto(fixture)).toContain('atual: v3');
    expect(texto(fixture)).toContain('nome');
  });

  it('oferece filtrar por tag quando há tags cadastradas', () => {
    configurar('User');
    const fixture = montar();

    const opcoes = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('select[name="tag"] option'),
    ).map((o) => o.textContent?.trim());

    expect(opcoes).toContain('Todas as tags');
    expect(opcoes).toContain('novo');
    expect(opcoes).toContain('cliente');
  });

  it('avisa que o conteúdo não é editável', () => {
    configurar('User');
    const fixture = montar();

    // A regra central da fase precisa estar visível para quem usa, não só
    // imposta pelo servidor.
    expect(texto(fixture)).toContain('nunca é alterado');
  });

  it('não oferece arquivar para usuário comum', () => {
    configurar('User');
    const fixture = montar();

    const botoes = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.cartao-acao button'),
    );

    expect(botoes).toHaveLength(0);
  });

  it('oferece arquivar e reativar para administrador', () => {
    configurar('Admin');
    const fixture = montar();

    const botoes = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.cartao-acao button'),
    ).map((botao) => botao.textContent?.trim());

    expect(botoes).toContain('Arquivar');
    expect(botoes).toContain('Reativar');
  });

  it('arquiva e recarrega a lista', async () => {
    configurar('Admin');
    const fixture = montar();

    const arquivar = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.cartao-acao button'),
    ).find((botao) => botao.textContent?.includes('Arquivar')) as HTMLButtonElement;

    arquivar.click();

    httpMock.expectOne({ method: 'DELETE', url: '/api/templates/id-boas-vindas' }).flush(null);

    httpMock.expectOne((r) => r.url === '/api/templates').flush([lista[1]]);

    await fixture.whenStable();
    fixture.detectChanges();

    // A mensagem precisa dizer o que de fato aconteceu: arquivar não apaga.
    expect(texto(fixture)).toContain('As versões continuam guardadas');
  });

  it('envia busca e categoria como filtro', () => {
    configurar('User');
    const fixture = montar();

    fixture.componentInstance['busca'].set('boas');
    fixture.componentInstance['categoria'].set('Onboarding');
    fixture.componentInstance['carregar']();

    const requisicao = httpMock.expectOne(
      (r) => r.url === '/api/templates' && r.params.get('busca') === 'boas',
    );

    expect(requisicao.request.params.get('categoria')).toBe('Onboarding');
    requisicao.flush([]);
  });

  it('avisa quando não consegue carregar', async () => {
    configurar('User');

    const fixture = TestBed.createComponent(Templates);
    fixture.detectChanges();

    httpMock
      .expectOne((r) => r.url === '/api/templates')
      .flush(null, { status: 500, statusText: 'Server Error' });
    httpMock.expectOne('/api/templates/categories').flush([]);
    httpMock.expectOne('/api/templates/tags').flush([]);

    await fixture.whenStable();
    fixture.detectChanges();

    expect(texto(fixture)).toContain('Não foi possível carregar os modelos.');
  });
});
