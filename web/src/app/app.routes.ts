import { Routes } from '@angular/router';

import { adminGuard, authGuard, guestGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/login/login').then((m) => m.Login),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./features/status/status').then((m) => m.Status),
  },
  {
    path: 'usuarios',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/usuarios/usuarios').then((m) => m.Usuarios),
  },
  {
    path: 'usuarios/novo',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/usuarios/novo-usuario').then((m) => m.NovoUsuario),
  },
  {
    path: '**',
    redirectTo: '',
  },
];
