import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = async (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (await auth.ensureSessionLoaded()) {
    return true;
  }

  // Guarda para onde a pessoa queria ir, para devolvê-la ao destino depois
  // de entrar em vez de largá-la numa página inicial genérica.
  return router.createUrlTree(['/login'], {
    queryParams: { destino: state.url },
  });
};

/** Bloqueia rotas de administração para quem está logado como usuário comum. */
export const adminGuard: CanActivateFn = async (route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const user = await auth.ensureSessionLoaded();

  if (!user) {
    return router.createUrlTree(['/login'], { queryParams: { destino: state.url } });
  }

  return user.role === 'Admin' ? true : router.createUrlTree(['/']);
};

/** Impede que quem já está logado veja a tela de login de novo. */
export const guestGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return (await auth.ensureSessionLoaded()) ? router.createUrlTree(['/']) : true;
};
