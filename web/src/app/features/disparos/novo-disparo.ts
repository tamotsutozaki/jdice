import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { CampaignsService } from '../../core/campaigns/campaigns.service';
import { ListaDeDestinatarios, RecipientsService } from '../../core/recipients/recipients.service';
import { TemplateDaLista, TemplatesService } from '../../core/templates/templates.service';

/**
 * Montagem do disparo em passos, com um resumo do que vai acontecer antes de
 * confirmar. É a tela mais delicada do sistema: depois de confirmar, o e-mail
 * sai e não tem como voltar.
 */
@Component({
  selector: 'app-novo-disparo',
  imports: [FormsModule, RouterLink],
  templateUrl: './novo-disparo.html',
  styleUrl: './novo-disparo.scss',
})
export class NovoDisparo {
  private readonly campaigns = inject(CampaignsService);
  private readonly templates = inject(TemplatesService);
  private readonly recipients = inject(RecipientsService);
  private readonly router = inject(Router);

  protected readonly modelos = signal<TemplateDaLista[]>([]);
  protected readonly listas = signal<ListaDeDestinatarios[]>([]);

  protected readonly templateId = signal('');
  protected readonly assunto = signal('');
  protected readonly nome = signal('');
  protected readonly remetente = signal('');
  protected readonly valoresComuns = signal<{ chave: string; valor: string }[]>([]);
  protected readonly listasEscolhidas = signal<string[]>([]);
  protected readonly quando = signal<'agora' | 'agendar'>('agora');
  protected readonly data = signal('');
  protected readonly hora = signal('');

  protected readonly enviando = signal(false);
  protected readonly erro = signal('');
  protected readonly confirmando = signal(false);

  protected readonly fusoDoNavegador = Intl.DateTimeFormat().resolvedOptions().timeZone;

  protected readonly modeloEscolhido = computed(() =>
    this.modelos().find((modelo) => modelo.id === this.templateId()),
  );

  /** Soma dos ativos das listas escolhidas — estimativa, não garantia. */
  protected readonly totalEstimado = computed(() =>
    this.listas()
      .filter((lista) => this.listasEscolhidas().includes(lista.id))
      .reduce((soma, lista) => soma + lista.totalAtivos, 0),
  );

  protected readonly podeConfirmar = computed(
    () =>
      this.templateId().length > 0 &&
      this.assunto().trim().length > 0 &&
      this.listasEscolhidas().length > 0 &&
      (this.quando() === 'agora' || (this.data().length > 0 && this.hora().length > 0)),
  );

  constructor() {
    this.templates.listar().subscribe((modelos) =>
      // Modelo arquivado ou sem versão não pode ser disparado; não vale
      // oferecer para depois recusar.
      this.modelos.set(modelos.filter((modelo) => !modelo.arquivado && modelo.versaoAtual)),
    );

    this.recipients.listas().subscribe((listas) => this.listas.set(listas));
  }

  protected alternarLista(id: string): void {
    this.listasEscolhidas.update((atual) =>
      atual.includes(id) ? atual.filter((item) => item !== id) : [...atual, id],
    );
  }

  protected adicionarValor(): void {
    this.valoresComuns.update((atual) => [...atual, { chave: '', valor: '' }]);
  }

  protected removerValor(indice: number): void {
    this.valoresComuns.update((atual) => atual.filter((_, i) => i !== indice));
  }

  protected definirValor(indice: number, parte: 'chave' | 'valor', conteudo: string): void {
    this.valoresComuns.update((atual) =>
      atual.map((item, i) => (i === indice ? { ...item, [parte]: conteudo } : item)),
    );
  }

  protected confirmar(): void {
    if (!this.podeConfirmar() || this.enviando()) {
      return;
    }

    this.enviando.set(true);
    this.erro.set('');

    const valores = Object.fromEntries(
      this.valoresComuns()
        .filter((item) => item.chave.trim() && item.valor.trim())
        .map((item) => [item.chave.trim(), item.valor.trim()]),
    );

    this.campaigns
      .criar({
        nome: this.nome() || undefined,
        templateId: this.templateId(),
        assunto: this.assunto(),
        remetente: this.remetente().trim() || undefined,
        valoresComuns: Object.keys(valores).length > 0 ? valores : undefined,
        listaIds: this.listasEscolhidas(),
        agendar: this.quando() === 'agendar' ? this.montarDataHora() : undefined,
        fusoHorario: this.fusoDoNavegador,
      })
      .subscribe({
        next: (disparo) => this.router.navigate(['/disparos', disparo.id]),
        error: (erro: HttpErrorResponse) => {
          this.enviando.set(false);
          this.confirmando.set(false);
          this.erro.set(this.mensagemDe(erro));
        },
      });
  }

  /** Converte data e hora locais para o instante absoluto que a API espera. */
  private montarDataHora(): string {
    return new Date(`${this.data()}T${this.hora()}:00`).toISOString();
  }

  private mensagemDe(erro: HttpErrorResponse): string {
    const detalhe = typeof erro.error?.detail === 'string' ? erro.error.detail : '';

    switch (erro.status) {
      case 400:
        return detalhe || 'Nenhum destinatário disponível para este disparo.';
      case 409:
        return detalhe || 'O modelo escolhido não pode ser usado.';
      case 404:
        return 'O modelo escolhido não existe mais.';
      default:
        return 'Não foi possível criar o disparo. Tente novamente em instantes.';
    }
  }
}
