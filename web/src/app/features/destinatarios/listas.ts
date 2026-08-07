import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { ListaDeDestinatarios, RecipientsService } from '../../core/recipients/recipients.service';

@Component({
  selector: 'app-listas-de-destinatarios',
  imports: [FormsModule, RouterLink],
  templateUrl: './listas.html',
  styleUrl: './listas.scss',
})
export class ListasDeDestinatarios {
  private readonly recipients = inject(RecipientsService);
  private readonly auth = inject(AuthService);

  protected readonly listas = signal<ListaDeDestinatarios[]>([]);
  protected readonly carregando = signal(true);
  protected readonly erro = signal('');
  protected readonly aviso = signal('');
  protected readonly emAndamento = signal<string | null>(null);
  protected readonly confirmando = signal<string | null>(null);

  protected readonly criando = signal(false);
  protected readonly nome = signal('');
  protected readonly descricao = signal('');
  protected readonly salvando = signal(false);

  protected readonly isAdmin = this.auth.isAdmin;

  constructor() {
    this.carregar();
  }

  protected carregar(): void {
    this.carregando.set(true);
    this.erro.set('');

    this.recipients.listas().subscribe({
      next: (listas) => {
        this.listas.set(listas);
        this.carregando.set(false);
      },
      error: () => {
        this.erro.set('Não foi possível carregar as listas.');
        this.carregando.set(false);
      },
    });
  }

  protected criar(): void {
    if (!this.nome().trim() || this.salvando()) {
      return;
    }

    this.salvando.set(true);
    this.erro.set('');

    this.recipients.criarLista({ nome: this.nome(), descricao: this.descricao() }).subscribe({
      next: (lista) => {
        this.salvando.set(false);
        this.criando.set(false);
        this.nome.set('');
        this.descricao.set('');
        this.aviso.set(`Lista ${lista.nome} criada.`);
        this.carregar();
      },
      error: (erro: HttpErrorResponse) => {
        this.salvando.set(false);
        this.erro.set(
          erro.status === 409
            ? 'Já existe uma lista com esse nome.'
            : 'Não foi possível criar a lista.',
        );
      },
    });
  }

  protected remover(lista: ListaDeDestinatarios): void {
    this.emAndamento.set(lista.id);
    this.confirmando.set(null);
    this.erro.set('');
    this.aviso.set('');

    this.recipients.removerLista(lista.id).subscribe({
      next: () => {
        this.emAndamento.set(null);
        // A frase importa: apagar a lista não apaga as pessoas.
        this.aviso.set(`Lista ${lista.nome} apagada. Os destinatários continuam cadastrados.`);
        this.carregar();
      },
      error: () => {
        this.emAndamento.set(null);
        this.erro.set('Não foi possível apagar a lista.');
      },
    });
  }
}
