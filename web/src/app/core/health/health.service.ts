import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';

export type HealthState = 'checking' | 'healthy' | 'unhealthy';

/**
 * O front sempre chama caminho relativo: em dev quem resolve é o proxy do
 * dev-server (proxy.conf.json), em container é o nginx. Assim não existe
 * URL de API embutida no bundle.
 */
@Injectable({ providedIn: 'root' })
export class HealthService {
  private readonly http = inject(HttpClient);

  check(endpoint: 'live' | 'ready'): Observable<HealthState> {
    return this.http.get(`/health/${endpoint}`, { responseType: 'text' }).pipe(
      map((body): HealthState => (body.trim() === 'Healthy' ? 'healthy' : 'unhealthy')),
      catchError(() => of<HealthState>('unhealthy')),
    );
  }
}
