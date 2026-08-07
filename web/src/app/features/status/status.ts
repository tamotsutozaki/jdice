import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';

interface DisparoRecente {
  id: string;
  nome: string;
  situacao: string;
  total: number;
  enviados: number;
  agendadoPara: string;
}

interface Painel {
  modelos: number;
  destinatarios: number;
  listas: number;
  disparosAgendados: number;
  emailsEnviados: number;
  enviadosNosUltimos30Dias: number;
  recentes: DisparoRecente[];
}

/**
 * Painel com números reais do banco. O projeto original mostrava "taxa de
 * abertura ~68%" e "735 aberturas estimadas" escritos no código — indicadores
 * inventados que faziam o sistema parecer medir coisas que não media.
 */
@Component({
  selector: 'app-status',
  imports: [RouterLink],
  templateUrl: './status.html',
  styleUrl: './status.scss',
})
export class Status {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  protected readonly painel = signal<Painel | null>(null);
  protected readonly carregando = signal(true);
  protected readonly erro = signal('');

  protected readonly isAdmin = this.auth.isAdmin;

  constructor() {
    this.http.get<Painel>('/api/dashboard').subscribe({
      next: (painel) => {
        this.painel.set(painel);
        this.carregando.set(false);
      },
      error: () => {
        this.erro.set('Não foi possível carregar o painel.');
        this.carregando.set(false);
      },
    });
  }

  protected rotuloDaSituacao(situacao: string): string {
    switch (situacao) {
      case 'Scheduled':
        return 'Agendado';
      case 'Running':
        return 'Enviando';
      case 'Completed':
        return 'Concluído';
      case 'Cancelled':
        return 'Cancelado';
      default:
        return situacao;
    }
  }

  protected formatarData(valor: string): string {
    const data = new Date(valor);

    return Number.isNaN(data.getTime())
      ? ''
      : data.toLocaleDateString('pt-BR', { dateStyle: 'short' });
  }
}
