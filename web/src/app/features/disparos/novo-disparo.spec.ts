import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { NovoDisparo } from './novo-disparo';

describe('NovoDisparo', () => {
  let httpMock: HttpTestingController;

  const modelos = [
    {
      id: 'modelo-1',
      nome: 'Boas-vindas',
      categoria: 'Onboarding',
      tags: [],
      totalDeVersoes: 2,
      versaoAtual: 2,
      variaveis: ['nome', 'empresa'],
      arquivado: false,
      atualizadoEm: '2026-08-05T12:00:00Z',
    },
    {
      id: 'modelo-arquivado',
      nome: 'Antigo',
      categoria: '',
      tags: [],
      totalDeVersoes: 1,
      versaoAtual: 1,
      variaveis: [],
      arquivado: true,
      atualizadoEm: '2026-07-01T12:00:00Z',
    },
  ];

  const listas = [
    { id: 'lista-1', nome: 'Clientes', descricao: '', totalDeMembros: 12, totalAtivos: 10 },
    { id: 'lista-2', nome: 'Newsletter', descricao: '', totalDeMembros: 40, totalAtivos: 35 },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NovoDisparo],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  function montar() {
    const fixture = TestBed.createComponent(NovoDisparo);
    fixture.detectChanges();

    httpMock.expectOne((r) => r.url === '/api/templates').flush(modelos);
    httpMock.expectOne('/api/recipient-lists').flush(listas);
    fixture.detectChanges();

    return fixture;
  }

  type Interno = {
    modelos(): unknown[];
    templateId: { set(v: string): void };
    assunto: { set(v: string): void };
    listasEscolhidas: { set(v: string[]): void };
    quando: { set(v: 'agora' | 'agendar'): void };
    confirmando: { set(v: boolean): void };
    podeConfirmar(): boolean;
    totalEstimado(): number;
    confirmar(): void;
  };

  function interno(fixture: ComponentFixture<NovoDisparo>): Interno {
    return fixture.componentInstance as unknown as Interno;
  }

  function texto(fixture: ComponentFixture<NovoDisparo>): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function preencher(fixture: ComponentFixture<NovoDisparo>) {
    interno(fixture).templateId.set('modelo-1');
    interno(fixture).assunto.set('Bem-vindo!');
    interno(fixture).listasEscolhidas.set(['lista-1']);
    fixture.detectChanges();
  }

  it('não oferece modelo arquivado para disparo', () => {
    const fixture = montar();

    // Oferecer para depois o servidor recusar seria fazer a pessoa perder o
    // trabalho de montar o disparo inteiro.
    expect(interno(fixture).modelos()).toHaveLength(1);
    expect(texto(fixture)).not.toContain('Antigo');
  });

  it('não deixa confirmar sem modelo, assunto e lista', () => {
    const fixture = montar();

    expect(interno(fixture).podeConfirmar()).toBe(false);

    preencher(fixture);

    expect(interno(fixture).podeConfirmar()).toBe(true);
  });

  it('soma os destinatários ativos das listas escolhidas', () => {
    const fixture = montar();

    interno(fixture).listasEscolhidas.set(['lista-1', 'lista-2']);
    fixture.detectChanges();

    // Usa os ativos, não o total: quem se descadastrou não recebe.
    expect(interno(fixture).totalEstimado()).toBe(45);
  });

  it('avisa que o envio imediato não pode ser cancelado', () => {
    const fixture = montar();
    preencher(fixture);

    interno(fixture).confirmando.set(true);
    fixture.detectChanges();

    // A pessoa precisa saber disso antes de clicar, não depois.
    expect(texto(fixture)).toContain('não há como cancelar');
  });

  it('avisa que o agendado pode ser cancelado', () => {
    const fixture = montar();
    preencher(fixture);

    interno(fixture).quando.set('agendar');
    interno(fixture).confirmando.set(true);
    fixture.detectChanges();

    expect(texto(fixture)).toContain('poderá cancelar ou remarcar');
  });

  it('cria o disparo e vai para o acompanhamento', async () => {
    const fixture = montar();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    preencher(fixture);
    interno(fixture).confirmar();

    const requisicao = httpMock.expectOne('/api/campaigns');
    expect(requisicao.request.body.templateId).toBe('modelo-1');
    expect(requisicao.request.body.listaIds).toEqual(['lista-1']);
    requisicao.flush({ id: 'disparo-1' });

    await fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith(['/disparos', 'disparo-1']);
  });

  it('mostra o motivo quando o servidor recusa', async () => {
    const fixture = montar();

    preencher(fixture);
    interno(fixture).confirmar();

    httpMock
      .expectOne('/api/campaigns')
      .flush(
        { detail: 'Nenhum destinatário disponível para este disparo.' },
        { status: 400, statusText: 'Bad Request' },
      );

    await fixture.whenStable();
    fixture.detectChanges();

    expect(texto(fixture)).toContain('Nenhum destinatário disponível');
  });

  it('mostra as variáveis que o modelo espera', () => {
    const fixture = montar();

    interno(fixture).templateId.set('modelo-1');
    fixture.detectChanges();

    // Quem dispara precisa saber que os campos vêm do destinatário.
    expect(texto(fixture)).toContain('nome');
    expect(texto(fixture)).toContain('empresa');
  });
});
