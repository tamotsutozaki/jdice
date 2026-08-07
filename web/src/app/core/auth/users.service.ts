import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { UserRole } from './auth.service';

export interface NovoUsuario {
  email: string;
  senha: string;
  role: UserRole;
}

export interface UsuarioCriado {
  id: string;
  email: string;
  role: UserRole;
}

@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly http = inject(HttpClient);

  criar(usuario: NovoUsuario): Observable<UsuarioCriado> {
    return this.http.post<UsuarioCriado>('/api/auth/users', usuario);
  }
}
