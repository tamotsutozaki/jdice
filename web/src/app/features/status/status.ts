import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { HealthService, HealthState } from '../../core/health/health.service';

/**
 * Painel inicial da área logada: situação do sistema e atalhos. Ganha conteúdo
 * de verdade — contagens de disparo, últimos envios — quando essas coisas
 * existirem.
 */
@Component({
  selector: 'app-status',
  imports: [RouterLink],
  templateUrl: './status.html',
  styleUrl: './status.scss',
})
export class Status {
  private readonly health = inject(HealthService);
  private readonly auth = inject(AuthService);

  protected readonly live = signal<HealthState>('checking');
  protected readonly ready = signal<HealthState>('checking');

  protected readonly isAdmin = this.auth.isAdmin;

  constructor() {
    this.health.check('live').subscribe((state) => this.live.set(state));
    this.health.check('ready').subscribe((state) => this.ready.set(state));
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
