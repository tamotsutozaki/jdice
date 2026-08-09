import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { TemplateDetalhe } from '../../core/templates/templates.service';
import { TemplateDetalhePagina } from './template-detalhe';

describe('TemplateDetalhePagina', () => {
  let httpMock: HttpTestingController;

  const detalhe: TemplateDetalhe = {
    id: 'id-modelo',
    nome: 'Boas-vindas',
    categoria: 'Onboarding',
    tags: ['novo'],
    arquivado: false,
    criadoPor: 'u1',
    criadoEm: '2026-08-01T12:00:00Z',
    atualizadoEm: '2026-08-05T12:00:00Z',
    versoes: [
      {
        id: 'v2',
        numero: 2,
        html: '<p>Olá {{ nome }}, versão nova.</p>',
        variaveis: ['nome'],
        criadoPor: 'u1',
        criadoEm: '2026-08-05T12:00:00Z',
      },
      {
        id: 'v1',
        numero: 1,
        html: '<p>Olá {{ nome }}.</p>',
        variaveis: ['nome'],
        criadoPor: 'u1',
        criadoEm: '2026-08-01T12:00:00Z',
      },
    ],
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TemplateDetalhePagina],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  async function montar(dados: TemplateDetalhe = detalhe) {
    const fixture = TestBed.createComponent(TemplateDetalhePagina);
    fixture.componentRef.setInput('id', 'id-modelo');
    fixture.detectChanges();

    await fixture.whenStable();

    httpMock.expectOne('/api/templates/id-modelo').flush(dados);
    httpMock.expectOne('/api/templates/categories').flush(['Onboarding']);
    httpMock.expectOne('/api/templates/tags').flush(['novo']);
    fixture.detectChanges();

    return fixture;
  }

  function texto(fixture: ComponentFixture<TemplateDetalhePagina>): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  it('mostra o histórico de versões, da mais nova para a mais antiga', async () => {
    const fixture = await montar();

    const numeros = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.versao .numero'),
    ).map((elemento) => elemento.textContent?.trim());

    expect(numeros).toEqual(['v2', 'v1']);
    expect(texto(fixture)).toContain('atual');
  });

  it('mostra o conteúdo de uma versão ao abri-la', async () => {
    const fixture = await montar();

    const cabecalhos = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>(
      '.versao-cabecalho',
    );
    cabecalhos[1].click();
    fixture.detectChanges();

    // A versão antiga continua acessível exatamente como foi criada.
    expect(texto(fixture)).toContain('<p>Olá {{ nome }}.</p>');
  });

  it('nova versão começa a partir do conteúdo atual', async () => {
    const fixture = await montar();

    fixture.componentInstance['abrirNovaVersao']();
    fixture.detectChanges();

    // Quase toda versão nova é um ajuste da anterior.
    expect(fixture.componentInstance['htmlDaNovaVersao']()).toBe(detalhe.versoes[0].html);

    // O editor embutido consulta o servidor ao receber conteúdo inicial.
    httpMock.match('/api/templates/analyze').forEach((r) => r.flush({ variaveis: [], erros: [] }));
    httpMock.match('/api/templates/preview').forEach((r) => r.flush({ html: '', erros: [] }));
  });

  it('deixa claro que salvar cria versão e não sobrescreve', async () => {
    const fixture = await montar();

    fixture.componentInstance['abrirNovaVersao']();
    fixture.detectChanges();

    expect(texto(fixture)).toContain('nada é sobrescrito');

    httpMock.match('/api/templates/analyze').forEach((r) => r.flush({ variaveis: [], erros: [] }));
    httpMock.match('/api/templates/preview').forEach((r) => r.flush({ html: '', erros: [] }));
  });

  it('editar dados não cria versão e avisa disso', async () => {
    const fixture = await montar();

    fixture.componentInstance['editandoDados'].set(true);
    fixture.componentInstance['nome'].set('Boas-vindas revisado');
    fixture.detectChanges();

    fixture.componentInstance['salvarDados']();

    const requisicao = httpMock.expectOne({ method: 'PUT', url: '/api/templates/id-modelo' });
    expect(requisicao.request.body.nome).toBe('Boas-vindas revisado');
    requisicao.flush({ ...detalhe, nome: 'Boas-vindas revisado' });

    await fixture.whenStable();
    fixture.detectChanges();

    expect(texto(fixture)).toContain('Nenhuma versão foi criada');
  });

  it('não oferece nova versão em modelo arquivado', async () => {
    const fixture = await montar({ ...detalhe, arquivado: true });

    const botoes = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('button'),
    ).map((botao) => botao.textContent?.trim());

    expect(botoes).not.toContain('Nova versão');
  });
});
