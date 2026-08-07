import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly enviando = signal(false);
  protected readonly erro = signal('');

  protected readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    senha: ['', [Validators.required]],
  });

  protected entrar(): void {
    if (this.form.invalid || this.enviando()) {
      this.form.markAllAsTouched();
      return;
    }

    this.enviando.set(true);
    this.erro.set('');

    const { email, senha } = this.form.getRawValue();

    this.auth.login(email, senha).subscribe({
      next: async () => {
        // Carrega o usuário antes de navegar para que a tela de destino já
        // tenha a sessão resolvida e não pisque em branco.
        await this.auth.ensureSessionLoaded();

        const destino = this.route.snapshot.queryParamMap.get('destino') ?? '/';
        await this.router.navigateByUrl(destino);
      },
      error: (erro: { status?: number }) => {
        this.enviando.set(false);
        this.erro.set(this.mensagemDe(erro.status));
      },
    });
  }

  private mensagemDe(status?: number): string {
    switch (status) {
      case 401:
        // Mensagem única para e-mail inexistente e senha errada: distinguir os
        // dois casos revelaria quem tem conta no sistema.
        return 'E-mail ou senha incorretos.';
      case 429:
        // Sem explicar que é limite de tentativas, a pessoa fica insistindo e
        // achando que a senha está errada — e cada insistência renova o bloqueio.
        return 'Muitas tentativas seguidas. Aguarde um minuto antes de tentar de novo.';
      default:
        return 'Não foi possível entrar. Tente novamente em instantes.';
    }
  }
}
