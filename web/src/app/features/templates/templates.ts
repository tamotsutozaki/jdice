import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { TemplateDaLista, TemplatesService } from '../../core/templates/templates.service';

@Component({
  selector: 'app-templates',
  imports: [FormsModule, RouterLink],
  templateUrl: './templates.html',
  styleUrl: './templates.scss',
})
export class Templates {
  private readonly templates = inject(TemplatesService);
  private readonly auth = inject(AuthService);

  protected readonly modelos = signal<TemplateDaLista[]>([]);
  protected readonly categorias = signal<string[]>([]);
  protected readonly tags = signal<string[]>([]);
  protected readonly carregando = signal(true);
  protected readonly erro = signal('');
  protected readonly aviso = signal('');
  protected readonly emAndamento = signal<string | null>(null);

  protected readonly busca = signal('');
  protected readonly categoria = signal('');
  protected readonly tag = signal('');
  protected readonly incluirArquivados = signal(false);

  protected readonly isAdmin = this.auth.isAdmin;

  constructor() {
    this.carregar();
    this.templates.categorias().subscribe((categorias) => this.categorias.set(categorias));
    this.templates.tags().subscribe((tags) => this.tags.set(tags));
  }

  protected carregar(): void {
    this.carregando.set(true);
    this.erro.set('');

    this.templates
      .listar({
        busca: this.busca(),
        categoria: this.categoria(),
        tag: this.tag(),
        incluirArquivados: this.incluirArquivados(),
      })
      .subscribe({
        next: (modelos) => {
          this.modelos.set(modelos);
          this.carregando.set(false);
        },
        error: () => {
          this.erro.set('Não foi possível carregar os modelos.');
          this.carregando.set(false);
        },
      });
  }

  protected arquivar(modelo: TemplateDaLista): void {
    this.emAndamento.set(modelo.id);
    this.erro.set('');
    this.aviso.set('');

    this.templates.arquivar(modelo.id).subscribe({
      next: () => {
        this.emAndamento.set(null);
        this.aviso.set(`${modelo.nome} saiu de circulação. As versões continuam guardadas.`);
        this.carregar();
      },
      error: () => {
        this.emAndamento.set(null);
        this.erro.set('Não foi possível arquivar o modelo.');
      },
    });
  }

  protected desarquivar(modelo: TemplateDaLista): void {
    this.emAndamento.set(modelo.id);
    this.erro.set('');
    this.aviso.set('');

    this.templates.desarquivar(modelo.id).subscribe({
      next: () => {
        this.emAndamento.set(null);
        this.aviso.set(`${modelo.nome} voltou a ficar disponível.`);
        this.carregar();
      },
      error: () => {
        this.emAndamento.set(null);
        this.erro.set('Não foi possível trazer o modelo de volta.');
      },
    });
  }

  protected formatarData(valor: string): string {
    const data = new Date(valor);

    return Number.isNaN(data.getTime())
      ? ''
      : data.toLocaleDateString('pt-BR', { dateStyle: 'short' });
  }
}
