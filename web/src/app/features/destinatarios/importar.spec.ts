import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { ResultadoDaImportacao } from '../../core/recipients/recipients.service';
import { ImportarDestinatarios } from './importar';

describe('ImportarDestinatarios', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ImportarDestinatarios],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  function montar() {
    const fixture = TestBed.createComponent(ImportarDestinatarios);
    fixture.detectChanges();

    httpMock
      .expectOne('/api/recipient-lists')
      .flush([
        { id: 'lista-1', nome: 'Clientes', descricao: '', totalDeMembros: 0, totalAtivos: 0 },
      ]);
    fixture.detectChanges();

    return fixture;
  }

  type Interno = {
    arquivo: { set(v: File | null): void };
    listaId: { set(v: string): void };
    importar(): void;
  };

  function interno(fixture: ComponentFixture<ImportarDestinatarios>): Interno {
    return fixture.componentInstance as unknown as Interno;
  }

  function texto(fixture: ComponentFixture<ImportarDestinatarios>): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  const resultadoComErros: ResultadoDaImportacao = {
    totalDeLinhas: 5,
    importados: 3,
    criados: 2,
    atualizados: 1,
    jaNaLista: 0,
    recusados: [
      { linha: 3, motivo: "E-mail inválido: 'sem-arroba'." },
      { linha: 5, motivo: 'E-mail em branco.' },
    ],
    colunasLivres: ['empresa'],
  };

  it('explica o formato esperado do arquivo', () => {
    const fixture = montar();

    // Quem monta a planilha precisa saber o que o sistema espera antes de
    // tentar e errar.
    expect(texto(fixture)).toContain('email');
    expect(texto(fixture)).toContain('acentuação salva pelo Excel');
  });

  it('envia o arquivo e a lista escolhida', () => {
    const fixture = montar();

    interno(fixture).arquivo.set(new File(['email\nana@x.com'], 'lista.csv'));
    interno(fixture).listaId.set('lista-1');
    interno(fixture).importar();

    const requisicao = httpMock.expectOne(
      (r) => r.url === '/api/recipients/import' && r.params.get('listaId') === 'lista-1',
    );

    expect(requisicao.request.body).toBeInstanceOf(FormData);
    requisicao.flush(resultadoComErros);
  });

  it('mostra as linhas recusadas com número e motivo', async () => {
    const fixture = montar();

    interno(fixture).arquivo.set(new File(['x'], 'lista.csv'));
    interno(fixture).importar();

    httpMock.expectOne((r) => r.url === '/api/recipients/import').flush(resultadoComErros);

    await fixture.whenStable();
    fixture.detectChanges();

    // Sem o número da linha, achar o erro numa planilha grande é impossível.
    expect(texto(fixture)).toContain('Linha 3');
    expect(texto(fixture)).toContain('sem-arroba');
    expect(texto(fixture)).toContain('Linha 5');
  });

  it('deixa claro que o resto do arquivo foi importado', async () => {
    const fixture = montar();

    interno(fixture).arquivo.set(new File(['x'], 'lista.csv'));
    interno(fixture).importar();

    httpMock.expectOne((r) => r.url === '/api/recipients/import').flush(resultadoComErros);

    await fixture.whenStable();
    fixture.detectChanges();

    expect(texto(fixture)).toContain('O resto do arquivo foi importado');
  });

  it('avisa quando tudo entrou sem problema', async () => {
    const fixture = montar();

    interno(fixture).arquivo.set(new File(['x'], 'lista.csv'));
    interno(fixture).importar();

    httpMock
      .expectOne((r) => r.url === '/api/recipients/import')
      .flush({
        ...resultadoComErros,
        recusados: [],
      });

    await fixture.whenStable();
    fixture.detectChanges();

    expect(texto(fixture)).toContain('Todas as linhas do arquivo foram importadas');
  });

  it('mostra as colunas que viraram campos', async () => {
    const fixture = montar();

    interno(fixture).arquivo.set(new File(['x'], 'lista.csv'));
    interno(fixture).importar();

    httpMock.expectOne((r) => r.url === '/api/recipients/import').flush(resultadoComErros);

    await fixture.whenStable();
    fixture.detectChanges();

    expect(texto(fixture)).toContain('empresa');
  });
});
