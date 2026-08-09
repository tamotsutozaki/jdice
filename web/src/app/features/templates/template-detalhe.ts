import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import {
  LIMITES_TEMPLATE,
  separarTags,
  TemplateDetalhe,
  TemplatesService,
} from '../../core/templates/templates.service';
import { EditorConteudo } from './editor-conteudo';

@Component({
  selector: 'app-template-detalhe',
  imports: [FormsModule, RouterLink, EditorConteudo],
  templateUrl: './template-detalhe.html',
  styleUrl: './template-detalhe.scss',
})
export class TemplateDetalhePagina {
  private readonly templates = inject(TemplatesService);

  /** Vem da rota, via withComponentInputBinding. */
  readonly id = input.required<string>();

  protected readonly modelo = signal<TemplateDetalhe | null>(null);
  protected readonly carregando = signal(true);
  protected readonly erro = signal('');
  protected readonly aviso = signal('');

  protected readonly versaoAberta = signal<number | null>(null);
  protected readonly criandoVersao = signal(false);
  protected readonly htmlDaNovaVersao = signal('');
  protected readonly conteudoValido = signal(false);
  protected readonly salvandoVersao = signal(false);

  protected readonly editandoDados = signal(false);
  protected readonly nome = signal('');
  protected readonly categoria = signal('');
  protected readonly tags = signal('');
  protected readonly salvandoDados = signal(false);

  protected readonly limites = LIMITES_TEMPLATE;
  protected readonly categorias = signal<string[]>([]);
  protected readonly tagsUsadas = signal<string[]>([]);

  protected readonly versaoAtual = computed(() => this.modelo()?.versoes[0] ?? null);

  // Mesma regra do backend, avisada enquanto se digita: mais de 10 tags ou uma
  // tag longa demais seria recusada no salvamento.
  protected readonly erroTags = computed(() => {
    const tags = separarTags(this.tags());

    if (tags.length > LIMITES_TEMPLATE.maxTags) {
      return `No máximo ${LIMITES_TEMPLATE.maxTags} tags.`;
    }

    if (tags.some((tag) => tag.length > LIMITES_TEMPLATE.maxTag)) {
      return `Cada tag deve ter até ${LIMITES_TEMPLATE.maxTag} caracteres.`;
    }

    return '';
  });

  constructor() {
    // O id vem da rota e não muda enquanto a tela está aberta.
    queueMicrotask(() => this.carregar());
    this.templates.categorias().subscribe((categorias) => this.categorias.set(categorias));
    this.templates.tags().subscribe((tags) => this.tagsUsadas.set(tags));
  }

  protected carregar(): void {
    this.carregando.set(true);
    this.erro.set('');

    this.templates.obter(this.id()).subscribe({
      next: (modelo) => {
        this.modelo.set(modelo);
        this.nome.set(modelo.nome);
        this.categoria.set(modelo.categoria);
        this.tags.set(modelo.tags.join(', '));
        this.carregando.set(false);
      },
      error: () => {
        this.erro.set('Não foi possível carregar o modelo.');
        this.carregando.set(false);
      },
    });
  }

  protected alternarVersao(numero: number): void {
    this.versaoAberta.update((atual) => (atual === numero ? null : numero));
  }

  protected abrirNovaVersao(): void {
    // Parte do conteúdo atual: quase toda versão nova é um ajuste da anterior,
    // e reescrever do zero seria trabalho desnecessário.
    this.htmlDaNovaVersao.set(this.versaoAtual()?.html ?? '');
    this.criandoVersao.set(true);
    this.aviso.set('');
  }

  protected cancelarNovaVersao(): void {
    this.criandoVersao.set(false);
  }

  protected salvarNovaVersao(): void {
    if (!this.conteudoValido() || this.salvandoVersao()) {
      return;
    }

    this.salvandoVersao.set(true);
    this.erro.set('');

    this.templates.criarVersao(this.id(), this.htmlDaNovaVersao()).subscribe({
      next: (versao) => {
        this.salvandoVersao.set(false);
        this.criandoVersao.set(false);
        this.aviso.set(`Versão ${versao.numero} criada. A anterior continua guardada.`);
        this.carregar();
      },
      error: (erro: HttpErrorResponse) => {
        this.salvandoVersao.set(false);
        this.erro.set(
          erro.status === 409
            ? 'Este modelo está arquivado e não recebe novas versões.'
            : 'Não foi possível criar a versão.',
        );
      },
    });
  }

  protected salvarDados(): void {
    if (this.salvandoDados() || this.erroTags() || !this.nome().trim()) {
      return;
    }

    this.salvandoDados.set(true);
    this.erro.set('');

    this.templates
      .atualizarDados(this.id(), {
        nome: this.nome(),
        categoria: this.categoria(),
        tags: separarTags(this.tags()),
      })
      .subscribe({
        next: (modelo) => {
          this.salvandoDados.set(false);
          this.editandoDados.set(false);
          this.modelo.set(modelo);
          this.aviso.set('Dados atualizados. Nenhuma versão foi criada.');
        },
        error: (erro: HttpErrorResponse) => {
          this.salvandoDados.set(false);
          this.erro.set(
            erro.status === 409
              ? 'Já existe um modelo com esse nome.'
              : 'Não foi possível salvar os dados.',
          );
        },
      });
  }

  protected formatarDataHora(valor: string): string {
    const data = new Date(valor);

    return Number.isNaN(data.getTime())
      ? ''
      : data.toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
  }
}
