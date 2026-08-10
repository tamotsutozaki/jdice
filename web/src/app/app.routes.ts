import { Routes } from '@angular/router';

import { adminGuard, authGuard, guestGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/login/login').then((m) => m.Login),
  },
  {
    // Tudo que exige sessão vive dentro do shell, que traz a barra lateral,
    // o topo e o rodapé. O login fica de fora, ocupando a tela inteira.
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell').then((m) => m.Shell),
    children: [
      {
        path: '',
        loadComponent: () => import('./features/status/status').then((m) => m.Status),
      },
      {
        path: 'modelos',
        loadComponent: () => import('./features/templates/templates').then((m) => m.Templates),
      },
      {
        path: 'modelos/novo',
        loadComponent: () =>
          import('./features/templates/novo-template').then((m) => m.NovoTemplate),
      },
      {
        path: 'modelos/:id',
        loadComponent: () =>
          import('./features/templates/template-detalhe').then((m) => m.TemplateDetalhePagina),
      },
      {
        path: 'disparos',
        loadComponent: () => import('./features/disparos/disparos').then((m) => m.Disparos),
      },
      {
        path: 'disparos/novo',
        loadComponent: () => import('./features/disparos/novo-disparo').then((m) => m.NovoDisparo),
      },
      {
        path: 'disparos/:id',
        loadComponent: () =>
          import('./features/disparos/disparo-detalhe').then((m) => m.DisparoDetalhe),
      },
      {
        path: 'destinatarios',
        loadComponent: () =>
          import('./features/destinatarios/destinatarios').then((m) => m.Destinatarios),
      },
      {
        path: 'destinatarios/novo',
        loadComponent: () =>
          import('./features/destinatarios/novo-destinatario').then((m) => m.NovoDestinatario),
      },
      {
        path: 'destinatarios/importar',
        loadComponent: () =>
          import('./features/destinatarios/importar').then((m) => m.ImportarDestinatarios),
      },
      {
        path: 'destinatarios/listas',
        loadComponent: () =>
          import('./features/destinatarios/listas').then((m) => m.ListasDeDestinatarios),
      },
      {
        path: 'destinatarios/:id/editar',
        loadComponent: () =>
          import('./features/destinatarios/editar-destinatario').then((m) => m.EditarDestinatario),
      },
      {
        path: 'perfil',
        loadComponent: () => import('./features/perfil/perfil').then((m) => m.Perfil),
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
        // Endereço desconhecido dentro da área logada: mostra o 404 dentro do
        // shell, em vez de redirecionar em silêncio para o painel.
        path: '**',
        loadComponent: () =>
          import('./features/nao-encontrado/nao-encontrado').then((m) => m.NaoEncontrado),
      },
    ],
  },
];
