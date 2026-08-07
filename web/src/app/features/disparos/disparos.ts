import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { CampaignsService, Disparo } from '../../core/campaigns/campaigns.service';

@Component({
  selector: 'app-disparos',
  imports: [FormsModule, RouterLink],
  templateUrl: './disparos.html',
  styleUrl: './disparos.scss',
})
export class Disparos {
  private readonly campaigns = inject(CampaignsService);

  protected readonly disparos = signal<Disparo[]>([]);
  protected readonly total = signal(0);
  protected readonly pagina = signal(1);
  protected readonly totalDePaginas = signal(0);
  protected readonly carregando = signal(true);
  protected readonly erro = signal('');
  protected readonly situacao = signal('');

  constructor() {
    this.carregar();
  }

  protected carregar(pagina = 1): void {
    this.carregando.set(true);
    this.erro.set('');
    this.pagina.set(pagina);

    this.campaigns.listar(this.situacao() || undefined, pagina).subscribe({
      next: (resultado) => {
        this.disparos.set(resultado.itens);
        this.total.set(resultado.total);
        this.totalDePaginas.set(resultado.totalDePaginas);
        this.carregando.set(false);
      },
      error: () => {
        this.erro.set('Não foi possível carregar os disparos.');
        this.carregando.set(false);
      },
    });
  }

  protected rotuloDaSituacao(situacao: Disparo['situacao']): string {
    switch (situacao) {
      case 'Scheduled':
        return 'Agendado';
      case 'Running':
        return 'Enviando';
      case 'Completed':
        return 'Concluído';
      case 'Cancelled':
        return 'Cancelado';
    }
  }

  /** Percentual concluído, para a barra de progresso. */
  protected progresso(disparo: Disparo): number {
    const { total, enviados, falhados, pulados } = disparo.resumo;

    return total === 0 ? 0 : Math.round(((enviados + falhados + pulados) / total) * 100);
  }

  protected formatarDataHora(valor: string): string {
    const data = new Date(valor);

    return Number.isNaN(data.getTime())
      ? ''
      : data.toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
  }
}
