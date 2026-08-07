import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { UserRole } from '../../core/auth/auth.service';
import { PASSWORD_POLICY } from '../../core/auth/password-policy';
import { UsersService } from '../../core/auth/users.service';

@Component({
  selector: 'app-novo-usuario',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './novo-usuario.html',
  styleUrl: './novo-usuario.scss',
})
export class NovoUsuario {
  private readonly users = inject(UsersService);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly politica = PASSWORD_POLICY;
  protected readonly perfis: readonly UserRole[] = ['User', 'Admin'];

  protected readonly enviando = signal(false);
  protected readonly erro = signal('');
  protected readonly criado = signal('');

  protected readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(320)]],
    senha: [
      '',
      [
        Validators.required,
        Validators.minLength(PASSWORD_POLICY.minimumLength),
        Validators.maxLength(PASSWORD_POLICY.maximumLength),
      ],
    ],
    role: ['User' as UserRole, [Validators.required]],
  });

  protected criar(): void {
    if (this.form.invalid || this.enviando()) {
      this.form.markAllAsTouched();
      return;
    }

    this.enviando.set(true);
    this.erro.set('');
    this.criado.set('');

    this.users.criar(this.form.getRawValue()).subscribe({
      next: (usuario) => {
        this.enviando.set(false);
        this.criado.set(usuario.email);

        // Limpa só as credenciais: quem cadastra várias contas seguidas
        // costuma manter o mesmo perfil.
        this.form.patchValue({ email: '', senha: '' });
        this.form.markAsUntouched();
      },
      error: (erro: HttpErrorResponse) => {
        this.enviando.set(false);
        this.erro.set(this.mensagemDe(erro));
      },
    });
  }

  private mensagemDe(erro: HttpErrorResponse): string {
    switch (erro.status) {
      case 409:
        return 'Já existe uma conta com esse e-mail.';
      case 400:
        return `A senha precisa ter entre ${PASSWORD_POLICY.minimumLength} e ${PASSWORD_POLICY.maximumLength} caracteres.`;
      case 403:
        // O guard barra antes, mas a sessão pode ter mudado de perfil no
        // meio do caminho — o servidor é quem dá a palavra final.
        return 'Sua conta não tem permissão para cadastrar usuários.';
      default:
        return 'Não foi possível cadastrar. Tente novamente em instantes.';
    }
  }
}
