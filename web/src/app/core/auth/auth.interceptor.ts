import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { AuthService } from './auth.service';

/**
 * Não injeta token em header nenhum: o cookie httpOnly é enviado pelo próprio
 * navegador. O interceptor existe para garantir o envio de credenciais e para
 * reagir quando a sessão expira no meio do uso.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const comCredenciais = request.clone({ withCredentials: true });

  return next(comCredenciais).pipe(
    catchError((error: HttpErrorResponse) => {
      // 401 no /auth/me é resposta esperada de quem não está logado — quem
      // trata é o guard. Redirecionar aqui criaria laço na tela de login.
      const ehVerificacaoDeSessao = request.url.endsWith('/api/auth/me');
      const ehTentativaDeLogin = request.url.endsWith('/api/auth/login');

      if (error.status === 401 && !ehVerificacaoDeSessao && !ehTentativaDeLogin) {
        auth.clearSession();
        router.navigate(['/login'], { queryParams: { destino: router.url } });
      }

      return throwError(() => error);
    }),
  );
};
