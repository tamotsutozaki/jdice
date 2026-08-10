import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { Pagina } from '../recipients/recipients.service';

export interface ResumoDeEntregas {
  total: number;
  pendentes: number;
  enviando: number;
  enviados: number;
  falhados: number;
  pulados: number;
}

export type SituacaoDoDisparo = 'Scheduled' | 'Running' | 'Completed' | 'Cancelled';

export interface Disparo {
  id: string;
  nome: string;
  assunto: string;
  templateId: string;
  versaoDoModelo: number;
  situacao: SituacaoDoDisparo;
  agendadoPara: string;
  fusoHorario: string;
  criadoEm: string;
  iniciadoEm: string | null;
  concluidoEm: string | null;
  resumo: ResumoDeEntregas;
}

export interface TentativaDeEntrega {
  numero: number;
  quando: string;
  erro: string;
}

export interface Entrega {
  id: string;
  email: string;
  situacao: 'Pending' | 'Sending' | 'Sent' | 'Failed' | 'Skipped';
  tentativas: number;
  erro: string;
  enviadoEm: string | null;
  historico: TentativaDeEntrega[];
}

export interface NovoDisparo {
  nome?: string;
  templateId: string;
  templateVersionId?: string;
  assunto: string;
  remetente?: string;
  valoresComuns?: Record<string, string>;
  listaIds: string[];
  destinatarioIds?: string[];
  agendar?: string;
  fusoHorario?: string;
}

@Injectable({ providedIn: 'root' })
export class CampaignsService {
  private readonly http = inject(HttpClient);

  listar(situacao?: string, pagina = 1): Observable<Pagina<Disparo>> {
    let params = new HttpParams().set('pagina', String(pagina)).set('tamanho', '25');

    if (situacao) {
      params = params.set('situacao', situacao);
    }

    return this.http.get<Pagina<Disparo>>('/api/campaigns', { params });
  }

  obter(id: string): Observable<Disparo> {
    return this.http.get<Disparo>(`/api/campaigns/${id}`);
  }

  entregas(id: string): Observable<Entrega[]> {
    return this.http.get<Entrega[]>(`/api/campaigns/${id}/deliveries`);
  }

  criar(disparo: NovoDisparo): Observable<Disparo> {
    return this.http.post<Disparo>('/api/campaigns', disparo);
  }

  /** Só vale antes de começar: depois que a primeira mensagem saiu, não volta. */
  cancelar(id: string): Observable<void> {
    return this.http.post<void>(`/api/campaigns/${id}/cancel`, {});
  }

  reagendar(id: string, agendar: string, fusoHorario: string): Observable<void> {
    return this.http.post<void>(`/api/campaigns/${id}/reschedule`, { agendar, fusoHorario });
  }
}
