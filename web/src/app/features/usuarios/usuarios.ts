import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { UsersService, UsuarioDaLista } from '../../core/auth/users.service';

@Component({
  selector: 'app-usuarios',
  imports: [RouterLink],
  templateUrl: './usuarios.html',
  styleUrl: './usuarios.scss',
})
export class Usuarios {
  private readonly users = inject(UsersService);
  private readonly auth = inject(AuthService);

  protected readonly usuarios = signal<UsuarioDaLista[]>([]);
  protected readonly carregando = signal(true);
  protected readonly erro = signal('');
  protected readonly aviso = signal('');

  /** Id com operação em curso, para desabilitar só a linha correspondente. */
  protected readonly emAndamento = signal<string | null>(null);

  /** Id aguardando confirmação: desativar conta não deve acontecer num clique só. */
  protected readonly confirmando = signal<string | null>(null);

  protected readonly ativos = computed(() => this.usuarios().filter((u) => u.ativo).length);

  constructor() {
    this.carregar();
  }

  protected carregar(): void {
    this.carregando.set(true);
    this.erro.set('');

    this.users.listar().subscribe({
      next: (usuarios) => {
        this.usuarios.set(usuarios);
        this.carregando.set(false);
      },
      error: () => {
        this.erro.set('Não foi possível carregar as contas.');
        this.carregando.set(false);
      },
    });
  }

  protected ehVoce(usuario: UsuarioDaLista): boolean {
    return this.auth.user()?.id === usuario.id;
  }

  protected pedirConfirmacao(id: string): void {
    this.confirmando.set(id);
    this.aviso.set('');
    this.erro.set('');
  }

  protected cancelar(): void {
    this.confirmando.set(null);
  }

  protected desativar(usuario: UsuarioDaLista): void {
    this.emAndamento.set(usuario.id);
    this.confirmando.set(null);
    this.erro.set('');
    this.aviso.set('');

    this.users.desativar(usuario.id).subscribe({
      next: () => {
        this.emAndamento.set(null);
        this.aviso.set(`${usuario.email} não pode mais entrar.`);
        this.carregar();
      },
      error: (erro: HttpErrorResponse) => {
        this.emAndamento.set(null);
        this.erro.set(this.mensagemDe(erro));
      },
    });
  }

  protected reativar(usuario: UsuarioDaLista): void {
    this.emAndamento.set(usuario.id);
    this.erro.set('');
    this.aviso.set('');

    // Reativar não pede confirmação: devolver acesso é reversível com um
    // clique, ao contrário de tirá-lo.
    this.users.reativar(usuario.id).subscribe({
      next: () => {
        this.emAndamento.set(null);
        this.aviso.set(`${usuario.email} voltou a ter acesso.`);
        this.carregar();
      },
      error: (erro: HttpErrorResponse) => {
        this.emAndamento.set(null);
        this.erro.set(this.mensagemDe(erro));
      },
    });
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

  private mensagemDe(erro: HttpErrorResponse): string {
    // O backend distingue os motivos no corpo; usamos o detalhe quando vem,
    // porque "último administrador" e "própria conta" pedem explicações
    // diferentes de quem está operando.
    const detalhe = typeof erro.error?.detail === 'string' ? erro.error.detail : '';

    switch (erro.status) {
      case 409:
        return detalhe || 'Essa conta não pode ser desativada.';
      case 404:
        return 'Conta não encontrada. A lista pode estar desatualizada.';
      case 403:
        return 'Sua conta não tem permissão para desativar contas.';
      default:
        return 'Não foi possível desativar. Tente novamente em instantes.';
    }
  }
}
