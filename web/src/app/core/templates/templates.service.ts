import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AbstractControl, ValidationErrors } from '@angular/forms';
import { Observable } from 'rxjs';

/** Espelham os limites do domínio (Template.cs), para a UI avisar antes do 400. */
export const LIMITES_TEMPLATE = {
  maxNome: 120,
  maxCategoria: 60,
  maxTag: 40,
  maxTags: 10,
} as const;

/** Quebra o campo de texto em tags limpas, do mesmo jeito que o envio faz. */
export function separarTags(texto: string): string[] {
  return texto
    .split(',')
    .map((tag) => tag.trim())
    .filter(Boolean);
}

/**
 * Recusa mais de 10 tags ou uma tag longa demais antes de bater no backend —
 * o servidor rejeitaria com 400, mas o aviso local evita a ida perdida.
 */
export function tagsValidas(control: AbstractControl): ValidationErrors | null {
  const tags = separarTags(control.value ?? '');

  if (tags.length > LIMITES_TEMPLATE.maxTags) {
    return { tagsDemais: LIMITES_TEMPLATE.maxTags };
  }

  if (tags.some((tag) => tag.length > LIMITES_TEMPLATE.maxTag)) {
    return { tagLonga: LIMITES_TEMPLATE.maxTag };
  }

  return null;
}

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

  tags(): Observable<string[]> {
    return this.http.get<string[]>('/api/templates/tags');
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
