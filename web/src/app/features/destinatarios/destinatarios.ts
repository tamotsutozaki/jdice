import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import {
  Destinatario,
  ListaDeDestinatarios,
  RecipientsService,
} from '../../core/recipients/recipients.service';

@Component({
  selector: 'app-destinatarios',
  imports: [FormsModule, RouterLink],
  templateUrl: './destinatarios.html',
  styleUrl: './destinatarios.scss',
})
export class Destinatarios {
  private readonly recipients = inject(RecipientsService);
  private readonly auth = inject(AuthService);

  protected readonly destinatarios = signal<Destinatario[]>([]);
  protected readonly listas = signal<ListaDeDestinatarios[]>([]);
  protected readonly total = signal(0);
  protected readonly pagina = signal(1);
  protected readonly totalDePaginas = signal(0);

  protected readonly carregando = signal(true);
  protected readonly erro = signal('');
  protected readonly aviso = signal('');
  protected readonly emAndamento = signal<string | null>(null);

  protected readonly busca = signal('');
  protected readonly listaId = signal('');
  protected readonly incluirDescadastrados = signal(false);

  protected readonly isAdmin = this.auth.isAdmin;

  protected readonly listaAtual = computed(() =>
    this.listas().find((lista) => lista.id === this.listaId()),
  );

  constructor() {
    this.carregar();
    this.recipients.listas().subscribe((listas) => this.listas.set(listas));
  }

  protected carregar(pagina = 1): void {
    this.carregando.set(true);
    this.erro.set('');
    this.pagina.set(pagina);

    this.recipients
      .listar({
        busca: this.busca(),
        listaId: this.listaId() || undefined,
        incluirDescadastrados: this.incluirDescadastrados(),
        pagina,
        tamanho: 25,
      })
      .subscribe({
        next: (resultado) => {
          this.destinatarios.set(resultado.itens);
          this.total.set(resultado.total);
          this.totalDePaginas.set(resultado.totalDePaginas);
          this.carregando.set(false);
        },
        error: () => {
          this.erro.set('Não foi possível carregar os destinatários.');
          this.carregando.set(false);
        },
      });
  }

  protected descadastrar(destinatario: Destinatario): void {
    this.executar(destinatario.id, this.recipients.descadastrar(destinatario.id), () =>
      // A frase diz o alcance real da ação: não é só sair desta lista.
      this.aviso.set(`${destinatario.email} não receberá mais nenhum envio.`),
    );
  }

  protected readmitir(destinatario: Destinatario): void {
    this.executar(destinatario.id, this.recipients.readmitir(destinatario.id), () =>
      this.aviso.set(`${destinatario.email} voltou a receber.`),
    );
  }

  protected remover(destinatario: Destinatario): void {
    this.executar(destinatario.id, this.recipients.remover(destinatario.id), () =>
      this.aviso.set(`${destinatario.email} foi removido do sistema.`),
    );
  }

  protected tirarDaLista(destinatario: Destinatario): void {
    const lista = this.listaAtual();

    if (!lista) {
      return;
    }

    this.executar(destinatario.id, this.recipients.removerDaLista(lista.id, destinatario.id), () =>
      this.aviso.set(`${destinatario.email} saiu de ${lista.nome}, mas continua cadastrado.`),
    );
  }

  protected campos(destinatario: Destinatario): { nome: string; valor: string }[] {
    return Object.entries(destinatario.campos).map(([nome, valor]) => ({ nome, valor }));
  }

  protected formatarData(valor: string | null): string {
    if (!valor) {
      return '';
    }

    const data = new Date(valor);

    return Number.isNaN(data.getTime())
      ? ''
      : data.toLocaleDateString('pt-BR', { dateStyle: 'short' });
  }

  private executar(
    id: string,
    acao: ReturnType<RecipientsService['descadastrar']>,
    aoConcluir: () => void,
  ) {
    this.emAndamento.set(id);
    this.erro.set('');
    this.aviso.set('');

    acao.subscribe({
      next: () => {
        this.emAndamento.set(null);
        aoConcluir();
        this.carregar(this.pagina());
      },
      error: (erro: HttpErrorResponse) => {
        this.emAndamento.set(null);
        this.erro.set(
          erro.status === 403
            ? 'Sua conta não tem permissão para esta ação.'
            : 'Não foi possível concluir. Tente novamente em instantes.',
        );
      },
    });
  }
}
