import { Component, computed, inject, signal } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthService } from '../core/auth/auth.service';
import { ICONES, NomeDeIcone } from './icones';
import { MARCA } from './marca';

interface ItemDeNavegacao {
  rotulo: string;
  rota: string;
  icone: NomeDeIcone;
  somenteAdmin?: boolean;
  /** Rota exata evita que "/" fique ativa em toda página. */
  exata?: boolean;
}

/**
 * Moldura das telas autenticadas: barra lateral de ícones, topo com marca e
 * navegação, e rodapé. Reproduz a estrutura do projeto original — no celular a
 * lateral vira uma barra inferior, como lá.
 */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly sanitizer = inject(DomSanitizer);

  protected readonly marca = MARCA;
  protected readonly usuario = this.auth.user;
  protected readonly saindo = signal(false);

  private readonly itens: ItemDeNavegacao[] = [
    { rotulo: 'Dashboard', rota: '/', icone: 'grid', exata: true },
    { rotulo: 'Modelos', rota: '/modelos', icone: 'lista' },
    { rotulo: 'Destinatários', rota: '/destinatarios', icone: 'pessoas' },
    { rotulo: 'Contas', rota: '/usuarios', icone: 'engrenagem', somenteAdmin: true },
  ];

  protected readonly navegacao = computed(() =>
    this.itens.filter((item) => !item.somenteAdmin || this.auth.isAdmin()),
  );

  /** Iniciais de quem está logado, no círculo amarelo do topo. */
  protected readonly iniciais = computed(() => {
    const email = this.usuario()?.email ?? '';
    const antesDoArroba = email.split('@')[0] ?? '';

    const partes = antesDoArroba
      .split(/[._-]+/)
      .filter(Boolean)
      .slice(0, 2);

    return partes.length > 0
      ? partes.map((parte) => parte.charAt(0).toUpperCase()).join('')
      : MARCA.sigla;
  });

  protected readonly perfil = computed(() => (this.auth.isAdmin() ? 'Administrador' : 'Usuário'));

  protected icone(nome: NomeDeIcone): SafeHtml {
    // Conteúdo é constante do próprio código, não entrada de usuário.
    return this.sanitizer.bypassSecurityTrustHtml(ICONES[nome]);
  }

  protected sair(): void {
    this.saindo.set(true);

    this.auth.logout().subscribe({
      next: () => this.router.navigateByUrl('/login'),
      error: () => this.router.navigateByUrl('/login'),
    });
  }
}
