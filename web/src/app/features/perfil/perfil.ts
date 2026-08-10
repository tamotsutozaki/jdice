import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';

/** Mesma política do backend (PasswordPolicy): 12 a 128 caracteres. */
const SENHA_MINIMA = 12;
const SENHA_MAXIMA = 128;

/**
 * Perfil da própria conta: mostra quem está logado e permite trocar a senha.
 * Antes, o usuário recebia uma senha do admin e não tinha como mudá-la depois.
 */
@Component({
  selector: 'app-perfil',
  imports: [ReactiveFormsModule],
  templateUrl: './perfil.html',
  styleUrl: './perfil.scss',
})
export class Perfil {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly senhaMinima = SENHA_MINIMA;
  protected readonly usuario = this.auth.user;
  protected readonly saindo = signal(false);

  protected readonly perfil = computed(() => (this.auth.isAdmin() ? 'Administrador' : 'Usuário'));

  protected readonly enviando = signal(false);
  protected readonly erro = signal('');
  protected readonly aviso = signal('');

  protected readonly form = this.formBuilder.nonNullable.group(
    {
      senhaAtual: ['', [Validators.required]],
      novaSenha: [
        '',
        [
          Validators.required,
          Validators.minLength(SENHA_MINIMA),
          Validators.maxLength(SENHA_MAXIMA),
        ],
      ],
      confirmar: ['', [Validators.required]],
    },
    { validators: [conferem] },
  );

  protected sair(): void {
    this.saindo.set(true);

    this.auth.logout().subscribe({
      next: () => this.router.navigateByUrl('/login'),
      error: () => this.router.navigateByUrl('/login'),
    });
  }

  protected trocar(): void {
    if (this.form.invalid || this.enviando()) {
      this.form.markAllAsTouched();
      return;
    }

    this.enviando.set(true);
    this.erro.set('');
    this.aviso.set('');

    const { senhaAtual, novaSenha } = this.form.getRawValue();

    this.auth.trocarSenha(senhaAtual, novaSenha).subscribe({
      next: () => {
        this.enviando.set(false);
        this.aviso.set('Senha alterada. Use a nova senha no próximo login.');
        this.form.reset();
      },
      error: (erro: HttpErrorResponse) => {
        this.enviando.set(false);
        const detalhe = typeof erro.error?.detail === 'string' ? erro.error.detail : '';
        this.erro.set(
          erro.status === 400
            ? detalhe || 'Verifique a senha atual e a nova senha.'
            : 'Não foi possível trocar a senha. Tente novamente.',
        );
      },
    });
  }
}

/** A nova senha e a confirmação precisam bater — erro comum e frustrante. */
function conferem(grupo: AbstractControl): { [key: string]: boolean } | null {
  const nova = grupo.get('novaSenha')?.value;
  const confirmar = grupo.get('confirmar')?.value;

  return nova && confirmar && nova !== confirmar ? { naoConferem: true } : null;
}
