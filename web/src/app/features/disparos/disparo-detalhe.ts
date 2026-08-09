import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, DestroyRef, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { CampaignsService, Disparo, Entrega } from '../../core/campaigns/campaigns.service';

/** Teto de entregas que o backend devolve numa consulta (GET .../deliveries). */
const LIMITE_DE_ENTREGAS = 200;

/** De quanto em quanto tempo a tela se atualiza sozinha enquanto o disparo corre. */
const INTERVALO_DE_ATUALIZACAO = 4000;

@Component({
  selector: 'app-disparo-detalhe',
  imports: [FormsModule, RouterLink],
  templateUrl: './disparo-detalhe.html',
  styleUrl: './disparo-detalhe.scss',
})
export class DisparoDetalhe {
  private readonly campaigns = inject(CampaignsService);
  private readonly destroyRef = inject(DestroyRef);

  readonly id = input.required<string>();

  protected readonly disparo = signal<Disparo | null>(null);
  protected readonly entregas = signal<Entrega[]>([]);
  protected readonly carregando = signal(true);
  protected readonly erro = signal('');
  protected readonly aviso = signal('');
  protected readonly emAndamento = signal(false);
  protected readonly confirmandoCancelamento = signal(false);

  // A consulta de entregas tem teto; quando o disparo tem mais que isso, a tela
  // avisa que está mostrando só uma parte, em vez de fingir que são todas.
  protected readonly limiteDeEntregas = LIMITE_DE_ENTREGAS;

  protected readonly entregasTruncadas = computed(() => {
    const total = this.disparo()?.resumo.total ?? 0;
    return this.entregas().length < total;
  });

  protected readonly remarcando = signal(false);
  protected readonly data = signal('');
  protected readonly hora = signal('');

  protected readonly filtroDeEntregas = signal('');

  protected readonly fusoDoNavegador = Intl.DateTimeFormat().resolvedOptions().timeZone;

  protected readonly podeMexer = computed(() => this.disparo()?.situacao === 'Scheduled');

  protected readonly entregasFiltradas = computed(() => {
    const filtro = this.filtroDeEntregas();

    return filtro
      ? this.entregas().filter((entrega) => entrega.situacao === filtro)
      : this.entregas();
  });

  protected readonly progresso = computed(() => {
    const resumo = this.disparo()?.resumo;

    if (!resumo || resumo.total === 0) {
      return 0;
    }

    return Math.round(((resumo.enviados + resumo.falhados + resumo.pulados) / resumo.total) * 100);
  });

  constructor() {
    queueMicrotask(() => this.carregar());

    // Enquanto o worker processa, a tela se atualiza sozinha: sem isso, o número
    // ficava congelado e só mexia ao clicar em "Atualizar", dando a impressão de
    // que o disparo tinha travado. O refresh é silencioso, para não piscar
    // "Carregando..." a cada ciclo.
    const timer = setInterval(() => {
      if (this.disparo()?.situacao === 'Running') {
        this.carregar(true);
      }
    }, INTERVALO_DE_ATUALIZACAO);

    this.destroyRef.onDestroy(() => clearInterval(timer));
  }

  protected carregar(silencioso = false): void {
    if (!silencioso) {
      this.carregando.set(true);
    }
    this.erro.set('');

    this.campaigns.obter(this.id()).subscribe({
      next: (disparo) => {
        this.disparo.set(disparo);
        this.carregando.set(false);
      },
      error: () => {
        this.erro.set('Não foi possível carregar o disparo.');
        this.carregando.set(false);
      },
    });

    this.campaigns.entregas(this.id()).subscribe({
      next: (entregas) => this.entregas.set(entregas),
      // Uma falha só nas entregas não pode derrubar a tela toda: o resumo acima
      // ainda vale. A lista fica vazia e o resto continua.
      error: () => this.entregas.set([]),
    });
  }

  protected cancelar(): void {
    this.emAndamento.set(true);
    this.confirmandoCancelamento.set(false);
    this.erro.set('');
    this.aviso.set('');

    this.campaigns.cancelar(this.id()).subscribe({
      next: () => {
        this.emAndamento.set(false);
        this.aviso.set('Disparo cancelado. Nenhum e-mail será enviado.');
        this.carregar();
      },
      error: (erro: HttpErrorResponse) => {
        this.emAndamento.set(false);
        this.erro.set(
          erro.status === 409
            ? 'O disparo já começou e não pode mais ser cancelado.'
            : 'Não foi possível cancelar.',
        );
      },
    });
  }

  protected remarcar(): void {
    if (!this.data() || !this.hora()) {
      return;
    }

    this.emAndamento.set(true);
    this.erro.set('');
    this.aviso.set('');

    const quando = new Date(`${this.data()}T${this.hora()}:00`).toISOString();

    this.campaigns.reagendar(this.id(), quando, this.fusoDoNavegador).subscribe({
      next: () => {
        this.emAndamento.set(false);
        this.remarcando.set(false);
        this.aviso.set('Disparo remarcado.');
        this.carregar();
      },
      error: (erro: HttpErrorResponse) => {
        this.emAndamento.set(false);
        this.erro.set(
          erro.status === 409
            ? 'O disparo já começou e não pode mais ser remarcado.'
            : 'Não foi possível remarcar.',
        );
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

  protected rotuloDaEntrega(situacao: Entrega['situacao']): string {
    switch (situacao) {
      case 'Pending':
        return 'Na fila';
      case 'Sending':
        return 'Enviando';
      case 'Sent':
        return 'Entregue';
      case 'Failed':
        return 'Falhou';
      case 'Skipped':
        return 'Não enviado';
    }
  }

  protected formatarDataHora(valor: string | null): string {
    if (!valor) {
      return '';
    }

    const data = new Date(valor);

    return Number.isNaN(data.getTime())
      ? ''
      : data.toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
  }
}
