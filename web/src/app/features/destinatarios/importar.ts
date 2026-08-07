import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import {
  ListaDeDestinatarios,
  RecipientsService,
  ResultadoDaImportacao,
} from '../../core/recipients/recipients.service';

@Component({
  selector: 'app-importar-destinatarios',
  imports: [FormsModule, RouterLink],
  templateUrl: './importar.html',
  styleUrl: './importar.scss',
})
export class ImportarDestinatarios {
  private readonly recipients = inject(RecipientsService);

  protected readonly listas = signal<ListaDeDestinatarios[]>([]);
  protected readonly listaId = signal('');
  protected readonly arquivo = signal<File | null>(null);
  protected readonly enviando = signal(false);
  protected readonly erro = signal('');
  protected readonly resultado = signal<ResultadoDaImportacao | null>(null);

  constructor() {
    this.recipients.listas().subscribe((listas) => this.listas.set(listas));
  }

  protected aoEscolherArquivo(evento: Event): void {
    const entrada = evento.target as HTMLInputElement;

    this.arquivo.set(entrada.files?.[0] ?? null);
    this.resultado.set(null);
    this.erro.set('');
  }

  protected importar(): void {
    const arquivo = this.arquivo();

    if (!arquivo || this.enviando()) {
      return;
    }

    this.enviando.set(true);
    this.erro.set('');
    this.resultado.set(null);

    this.recipients.importar(arquivo, this.listaId() || undefined).subscribe({
      next: (resultado) => {
        this.enviando.set(false);
        this.resultado.set(resultado);
      },
      error: (erro: HttpErrorResponse) => {
        this.enviando.set(false);
        this.erro.set(
          typeof erro.error?.detail === 'string'
            ? erro.error.detail
            : 'Não foi possível processar o arquivo.',
        );
      },
    });
  }

  protected nomeDoArquivo(): string {
    return this.arquivo()?.name ?? '';
  }

  protected nomeDaListaEscolhida(): string {
    return this.listas().find((lista) => lista.id === this.listaId())?.nome ?? '';
  }
}
