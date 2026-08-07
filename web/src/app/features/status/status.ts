import { Component, inject, signal } from '@angular/core';

import { HealthService, HealthState } from '../../core/health/health.service';

/**
 * Página de esqueleto da Fase 0. Existe para provar que o front alcança a API
 * ponta a ponta — é substituída pelo login na Fase 1.
 */
@Component({
  selector: 'app-status',
  templateUrl: './status.html',
  styleUrl: './status.scss',
})
export class Status {
  private readonly health = inject(HealthService);

  protected readonly live = signal<HealthState>('checking');
  protected readonly ready = signal<HealthState>('checking');

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
