import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { ListaDeDestinatarios, RecipientsService } from '../../core/recipients/recipients.service';

interface CampoLivre {
  nome: string;
  valor: string;
}

@Component({
  selector: 'app-novo-destinatario',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './novo-destinatario.html',
  styleUrl: './novo-destinatario.scss',
})
export class NovoDestinatario {
  private readonly recipients = inject(RecipientsService);
  private readonly router = inject(Router);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly listas = signal<ListaDeDestinatarios[]>([]);
  protected readonly campos = signal<CampoLivre[]>([]);
  protected readonly enviando = signal(false);
  protected readonly erro = signal('');

  protected readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    nome: [''],
    listaId: [''],
  });

  constructor() {
    this.recipients.listas().subscribe((listas) => this.listas.set(listas));
  }

  protected adicionarCampo(): void {
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

    const { email, nome, listaId } = this.form.getRawValue();

    const campos = Object.fromEntries(
      this.campos()
        .filter((campo) => campo.nome.trim() && campo.valor.trim())
        .map((campo) => [campo.nome.trim(), campo.valor.trim()]),
    );

    this.recipients.criar({ email, nome, campos, listaId: listaId || undefined }).subscribe({
      next: () => this.router.navigate(['/destinatarios']),
      error: (erro: HttpErrorResponse) => {
        this.enviando.set(false);
        this.erro.set(
          erro.status === 409
            ? 'Já existe um destinatário com esse e-mail.'
            : 'Não foi possível salvar. Revise os dados e tente de novo.',
        );
      },
    });
  }
}
