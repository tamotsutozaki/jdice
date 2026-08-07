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

export interface UsuarioDaLista {
  id: string;
  email: string;
  role: UserRole;
  ativo: boolean;
  criadoEm: string;
  desativadoEm: string | null;
}

@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly http = inject(HttpClient);

  criar(usuario: NovoUsuario): Observable<UsuarioCriado> {
    return this.http.post<UsuarioCriado>('/api/auth/users', usuario);
  }

  listar(): Observable<UsuarioDaLista[]> {
    return this.http.get<UsuarioDaLista[]>('/api/auth/users');
  }

  /**
   * O registro é preservado e apenas marcado como inativo — o servidor não
   * apaga a linha, para que o histórico de quem criou o quê continue
   * fazendo sentido quando existirem modelos e disparos.
   */
  desativar(id: string): Observable<void> {
    return this.http.delete<void>(`/api/auth/users/${id}`);
  }
}
