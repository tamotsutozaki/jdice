import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import {
  LIMITES_TEMPLATE,
  separarTags,
  tagsValidas,
  TemplatesService,
} from '../../core/templates/templates.service';
import { EditorConteudo } from './editor-conteudo';

@Component({
  selector: 'app-novo-template',
  imports: [ReactiveFormsModule, RouterLink, EditorConteudo],
  templateUrl: './novo-template.html',
  styleUrl: './novo-template.scss',
})
export class NovoTemplate {
  private readonly templates = inject(TemplatesService);
  private readonly router = inject(Router);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly limites = LIMITES_TEMPLATE;

  protected readonly html = signal('');
  protected readonly conteudoValido = signal(false);
  protected readonly enviando = signal(false);
  protected readonly erro = signal('');

  protected readonly categorias = signal<string[]>([]);
  protected readonly tagsUsadas = signal<string[]>([]);

  protected readonly form = this.formBuilder.nonNullable.group({
    nome: ['', [Validators.required, Validators.maxLength(LIMITES_TEMPLATE.maxNome)]],
    categoria: ['', [Validators.maxLength(LIMITES_TEMPLATE.maxCategoria)]],
    tags: ['', [tagsValidas]],
  });

  constructor() {
    this.templates.categorias().subscribe((categorias) => this.categorias.set(categorias));
    this.templates.tags().subscribe((tags) => this.tagsUsadas.set(tags));
  }

  protected criar(): void {
    if (this.form.invalid || !this.conteudoValido() || this.enviando()) {
      this.form.markAllAsTouched();
      return;
    }

    this.enviando.set(true);
    this.erro.set('');

    const { nome, categoria, tags } = this.form.getRawValue();

    this.templates
      .criar({
        nome,
        categoria,
        tags: separarTags(tags),
        html: this.html(),
      })
      .subscribe({
        next: (modelo) => this.router.navigate(['/modelos', modelo.id]),
        error: (erro: HttpErrorResponse) => {
          this.enviando.set(false);
          this.erro.set(this.mensagemDe(erro));
        },
      });
  }

  private mensagemDe(erro: HttpErrorResponse): string {
    const detalhe = typeof erro.error?.detail === 'string' ? erro.error.detail : '';

    switch (erro.status) {
      case 409:
        return detalhe || 'Já existe um modelo com esse nome.';
      case 400:
        return detalhe || 'Revise os campos e o conteúdo do modelo.';
      default:
        return 'Não foi possível salvar. Tente novamente em instantes.';
    }
  }
}
