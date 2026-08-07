import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/status/status').then((m) => m.Status),
  },
  {
    path: '**',
    redirectTo: '',
  },
];
