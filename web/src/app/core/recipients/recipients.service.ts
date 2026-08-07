import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface Destinatario {
  id: string;
  email: string;
  nome: string;
  campos: Record<string, string>;
  inscrito: boolean;
  descadastradoEm: string | null;
  criadoEm: string;
}

export interface Pagina<T> {
  itens: T[];
  total: number;
  pagina: number;
  tamanhoDaPagina: number;
  totalDePaginas: number;
}

export interface ListaDeDestinatarios {
  id: string;
  nome: string;
  descricao: string;
  totalDeMembros: number;
  totalAtivos: number;
  criadoEm: string;
  atualizadoEm: string;
}

export interface LinhaRecusada {
  linha: number;
  motivo: string;
}

export interface ResultadoDaImportacao {
  totalDeLinhas: number;
  importados: number;
  criados: number;
  atualizados: number;
  jaNaLista: number;
  recusados: LinhaRecusada[];
  colunasLivres: string[];
}

export interface FiltroDeDestinatarios {
  listaId?: string;
  busca?: string;
  incluirDescadastrados?: boolean;
  pagina?: number;
  tamanho?: number;
}

@Injectable({ providedIn: 'root' })
export class RecipientsService {
  private readonly http = inject(HttpClient);

  listar(filtro: FiltroDeDestinatarios = {}): Observable<Pagina<Destinatario>> {
    let params = new HttpParams();

    if (filtro.listaId) {
      params = params.set('listaId', filtro.listaId);
    }
    if (filtro.busca) {
      params = params.set('busca', filtro.busca);
    }
    if (filtro.incluirDescadastrados) {
      params = params.set('incluirDescadastrados', 'true');
    }
    params = params.set('pagina', String(filtro.pagina ?? 1));
    params = params.set('tamanho', String(filtro.tamanho ?? 50));

    return this.http.get<Pagina<Destinatario>>('/api/recipients', { params });
  }

  criar(destinatario: {
    email: string;
    nome: string;
    campos: Record<string, string>;
    listaId?: string;
  }): Observable<Destinatario> {
    return this.http.post<Destinatario>('/api/recipients', destinatario);
  }

  /** Vale para todas as listas: o descadastro é da pessoa, não do agrupamento. */
  descadastrar(id: string): Observable<void> {
    return this.http.post<void>(`/api/recipients/${id}/unsubscribe`, {});
  }

  readmitir(id: string): Observable<void> {
    return this.http.post<void>(`/api/recipients/${id}/resubscribe`, {});
  }

  remover(id: string): Observable<void> {
    return this.http.delete<void>(`/api/recipients/${id}`);
  }

  importar(arquivo: File, listaId?: string): Observable<ResultadoDaImportacao> {
    const corpo = new FormData();
    corpo.append('arquivo', arquivo);

    const params = listaId ? new HttpParams().set('listaId', listaId) : undefined;

    return this.http.post<ResultadoDaImportacao>('/api/recipients/import', corpo, { params });
  }

  listas(): Observable<ListaDeDestinatarios[]> {
    return this.http.get<ListaDeDestinatarios[]>('/api/recipient-lists');
  }

  criarLista(lista: { nome: string; descricao: string }): Observable<ListaDeDestinatarios> {
    return this.http.post<ListaDeDestinatarios>('/api/recipient-lists', lista);
  }

  removerLista(id: string): Observable<void> {
    return this.http.delete<void>(`/api/recipient-lists/${id}`);
  }

  /** Tira da lista sem descadastrar — sair de um grupo não é sair de tudo. */
  removerDaLista(listaId: string, destinatarioId: string): Observable<void> {
    return this.http.delete<void>(`/api/recipient-lists/${listaId}/members/${destinatarioId}`);
  }
}
