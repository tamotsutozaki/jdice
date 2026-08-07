import { Component, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';

import {
  ResultadoDeAnalise,
  ResultadoDePreview,
  TemplatesService,
} from '../../core/templates/templates.service';

/**
 * Editor de conteúdo com preview ao lado. As variáveis são descobertas pelo
 * servidor, não por busca de texto no navegador: só ele percorre a árvore do
 * template e enxerga o que está dentro de condicionais e laços.
 */
@Component({
  selector: 'app-editor-conteudo',
  imports: [FormsModule],
  templateUrl: './editor-conteudo.html',
  styleUrl: './editor-conteudo.scss',
})
export class EditorConteudo {
  private readonly templates = inject(TemplatesService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly htmlInicial = input('');
  readonly htmlMudou = output<string>();
  readonly validoMudou = output<boolean>();

  protected readonly html = signal('');
  protected readonly variaveis = signal<string[]>([]);
  protected readonly erros = signal<string[]>([]);
  protected readonly valores = signal<Record<string, string>>({});
  protected readonly previewHtml = signal<SafeHtml | null>(null);
  protected readonly analisando = signal(false);

  private readonly pedidoDePreview = new Subject<void>();

  constructor() {
    effect(() => {
      const inicial = this.htmlInicial();

      if (inicial && !this.html()) {
        this.html.set(inicial);
        this.analisar(inicial);
      }
    });

    // Espera a digitação parar antes de ir ao servidor: sem isso, cada tecla
    // vira uma requisição.
    toObservable(this.html)
      .pipe(
        debounceTime(400),
        distinctUntilChanged(),
        // O catchError fica DENTRO do switchMap de propósito. Tratado só no
        // subscribe, um único erro encerraria este observable e o editor
        // pararia de analisar para sempre — foi o que aconteceu quando a
        // análise do conteúdo vazio voltou 400 na montagem do componente.
        switchMap((conteudo) => {
          if (!conteudo.trim()) {
            return of<ResultadoDeAnalise>({ variaveis: [], erros: [] });
          }

          this.analisando.set(true);

          return this.templates.analisar(conteudo).pipe(
            catchError(() =>
              of<ResultadoDeAnalise>({
                variaveis: [],
                erros: ['Não foi possível verificar o conteúdo agora.'],
              }),
            ),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe((analise) => {
        this.analisando.set(false);
        this.variaveis.set(analise.variaveis);
        this.erros.set(analise.erros);
        this.validoMudou.emit(analise.erros.length === 0 && this.html().trim().length > 0);
        this.limparValoresOrfaos(analise.variaveis);
        this.pedidoDePreview.next();
      });

    this.pedidoDePreview
      .pipe(
        debounceTime(200),
        switchMap(() => {
          if (!this.html().trim()) {
            return of<ResultadoDePreview>({ html: null, erros: [] });
          }

          return this.templates
            .preview(this.html(), this.valores())
            .pipe(catchError(() => of<ResultadoDePreview>({ html: null, erros: [] })));
        }),
        takeUntilDestroyed(),
      )
      .subscribe((resultado) => {
        this.previewHtml.set(
          resultado.html === null
            ? null
            : // O preview vai para um iframe isolado e sem permissão de
              // script; sem o bypass, o Angular removeria os estilos que
              // fazem um e-mail parecer o que ele é.
              this.sanitizer.bypassSecurityTrustHtml(resultado.html),
        );
      });
  }

  protected aoDigitar(conteudo: string): void {
    this.html.set(conteudo);
    this.htmlMudou.emit(conteudo);
  }

  protected definirValor(variavel: string, valor: string): void {
    this.valores.update((atual) => ({ ...atual, [variavel]: valor }));
    this.pedidoDePreview.next();
  }

  protected valorDe(variavel: string): string {
    return this.valores()[variavel] ?? '';
  }

  private analisar(conteudo: string): void {
    this.templates
      .analisar(conteudo)
      .pipe(catchError(() => of<ResultadoDeAnalise>({ variaveis: [], erros: [] })))
      .subscribe((analise) => {
        this.variaveis.set(analise.variaveis);
        this.erros.set(analise.erros);
        this.validoMudou.emit(analise.erros.length === 0 && conteudo.trim().length > 0);
        this.pedidoDePreview.next();
      });
  }

  /** Variável que sumiu do conteúdo não deve continuar ocupando o formulário. */
  private limparValoresOrfaos(variaveis: string[]): void {
    this.valores.update((atual) =>
      Object.fromEntries(Object.entries(atual).filter(([nome]) => variaveis.includes(nome))),
    );
  }
}
