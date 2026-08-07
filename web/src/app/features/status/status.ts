import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { HealthService, HealthState } from '../../core/health/health.service';

/**
 * Área logada da Fase 1: confirma quem entrou e mostra a saúde do sistema.
 * Vira o dashboard de verdade quando houver o que mostrar nele.
 */
@Component({
  selector: 'app-status',
  templateUrl: './status.html',
  styleUrl: './status.scss',
})
export class Status {
  private readonly health = inject(HealthService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly live = signal<HealthState>('checking');
  protected readonly ready = signal<HealthState>('checking');
  protected readonly saindo = signal(false);

  protected readonly user = this.auth.user;
  protected readonly isAdmin = this.auth.isAdmin;

  constructor() {
    this.health.check('live').subscribe((state) => this.live.set(state));
    this.health.check('ready').subscribe((state) => this.ready.set(state));
  }

  protected sair(): void {
    this.saindo.set(true);
    this.auth.logout().subscribe({
      next: () => this.router.navigateByUrl('/login'),
      error: () => this.router.navigateByUrl('/login'),
    });
  }

  protected label(state: HealthState): string {
    switch (state) {
      case 'checking':
        return 'verificando...';
      case 'healthy':
        return 'no ar';
      case 'unhealthy':
        return 'indisponível';
    }
  }
}
