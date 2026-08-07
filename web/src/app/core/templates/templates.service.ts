import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface TemplateVersao {
  id: string;
  numero: number;
  html: string;
  variaveis: string[];
  criadoPor: string;
  criadoEm: string;
}

export interface TemplateDaLista {
  id: string;
  nome: string;
  categoria: string;
  tags: string[];
  totalDeVersoes: number;
  versaoAtual: number | null;
  variaveis: string[];
  arquivado: boolean;
  atualizadoEm: string;
}

export interface TemplateDetalhe {
  id: string;
  nome: string;
  categoria: string;
  tags: string[];
  arquivado: boolean;
  criadoPor: string;
  criadoEm: string;
  atualizadoEm: string;
  versoes: TemplateVersao[];
}

export interface FiltroDeTemplates {
  busca?: string;
  categoria?: string;
  tag?: string;
  incluirArquivados?: boolean;
}

export interface ResultadoDePreview {
  html: string | null;
  erros: string[];
}

export interface ResultadoDeAnalise {
  variaveis: string[];
  erros: string[];
}

@Injectable({ providedIn: 'root' })
export class TemplatesService {
  private readonly http = inject(HttpClient);

  listar(filtro: FiltroDeTemplates = {}): Observable<TemplateDaLista[]> {
    let params = new HttpParams();

    if (filtro.busca) {
      params = params.set('busca', filtro.busca);
    }
    if (filtro.categoria) {
      params = params.set('categoria', filtro.categoria);
    }
    if (filtro.tag) {
      params = params.set('tag', filtro.tag);
    }
    if (filtro.incluirArquivados) {
      params = params.set('incluirArquivados', 'true');
    }

    return this.http.get<TemplateDaLista[]>('/api/templates', { params });
  }

  categorias(): Observable<string[]> {
    return this.http.get<string[]>('/api/templates/categories');
  }

  obter(id: string): Observable<TemplateDetalhe> {
    return this.http.get<TemplateDetalhe>(`/api/templates/${id}`);
  }

  criar(modelo: {
    nome: string;
    categoria: string;
    tags: string[];
    html: string;
  }): Observable<TemplateDetalhe> {
    return this.http.post<TemplateDetalhe>('/api/templates', modelo);
  }

  /** Único caminho para mudar o conteúdo: versões existentes não são alteradas. */
  criarVersao(id: string, html: string): Observable<TemplateVersao> {
    return this.http.post<TemplateVersao>(`/api/templates/${id}/versions`, { html });
  }

  atualizarDados(
    id: string,
    dados: { nome: string; categoria: string; tags: string[] },
  ): Observable<TemplateDetalhe> {
    return this.http.put<TemplateDetalhe>(`/api/templates/${id}`, dados);
  }

  arquivar(id: string): Observable<void> {
    return this.http.delete<void>(`/api/templates/${id}`);
  }

  desarquivar(id: string): Observable<void> {
    return this.http.post<void>(`/api/templates/${id}/unarchive`, {});
  }

  analisar(html: string): Observable<ResultadoDeAnalise> {
    return this.http.post<ResultadoDeAnalise>('/api/templates/analyze', { html });
  }

  preview(html: string, valores: Record<string, string>): Observable<ResultadoDePreview> {
    return this.http.post<ResultadoDePreview>('/api/templates/preview', { html, valores });
  }
}
