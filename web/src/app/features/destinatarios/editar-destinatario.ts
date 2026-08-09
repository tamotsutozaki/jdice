import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { LIMITES_DESTINATARIO, RecipientsService } from '../../core/recipients/recipients.service';

interface CampoLivre {
  nome: string;
  valor: string;
}

/**
 * Corrige nome e campos de um destinatário já cadastrado. O e-mail é a
 * identidade da pessoa e não muda por aqui — mudar de e-mail é outra pessoa.
 */
@Component({
  selector: 'app-editar-destinatario',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './editar-destinatario.html',
  styleUrl: './novo-destinatario.scss',
})
export class EditarDestinatario {
  private readonly recipients = inject(RecipientsService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly limites = LIMITES_DESTINATARIO;

  private readonly id = this.route.snapshot.paramMap.get('id') ?? '';

  protected readonly email = signal('');
  protected readonly campos = signal<CampoLivre[]>([]);
  protected readonly carregando = signal(true);
  protected readonly enviando = signal(false);
  protected readonly erro = signal('');
  protected readonly naoEncontrado = signal(false);

  protected readonly form = this.formBuilder.nonNullable.group({
    nome: ['', [Validators.maxLength(LIMITES_DESTINATARIO.maxNome)]],
  });

  constructor() {
    this.recipients.obter(this.id).subscribe({
      next: (destinatario) => {
        this.email.set(destinatario.email);
        this.form.setValue({ nome: destinatario.nome });
        this.campos.set(
          Object.entries(destinatario.campos).map(([nome, valor]) => ({ nome, valor })),
        );
        this.carregando.set(false);
      },
      error: (erro: HttpErrorResponse) => {
        this.carregando.set(false);
        this.naoEncontrado.set(erro.status === 404);
        this.erro.set(
          erro.status === 404
            ? 'Destinatário não encontrado.'
            : 'Não foi possível carregar o destinatário.',
        );
      },
    });
  }

  protected adicionarCampo(): void {
    if (this.campos().length >= LIMITES_DESTINATARIO.maxCampos) {
      return;
    }

    this.campos.update((atual) => [...atual, { nome: '', valor: '' }]);
  }

  protected removerCampo(indice: number): void {
    this.campos.update((atual) => atual.filter((_, i) => i !== indice));
  }

  protected definirCampo(indice: number, parte: 'nome' | 'valor', conteudo: string): void {
    this.campos.update((atual) =>
      atual.map((campo, i) => (i === indice ? { ...campo, [parte]: conteudo } : campo)),
    );
  }

  protected salvar(): void {
    if (this.form.invalid || this.enviando()) {
      this.form.markAllAsTouched();
      return;
    }

    this.enviando.set(true);
    this.erro.set('');

    const { nome } = this.form.getRawValue();

    const campos = Object.fromEntries(
      this.campos()
        .filter((campo) => campo.nome.trim() && campo.valor.trim())
        .map((campo) => [campo.nome.trim(), campo.valor.trim()]),
    );

    this.recipients.atualizar(this.id, { nome, campos }).subscribe({
      next: () => this.router.navigate(['/destinatarios']),
      error: () => {
        this.enviando.set(false);
        this.erro.set('Não foi possível salvar. Revise os dados e tente de novo.');
      },
    });
  }
}
