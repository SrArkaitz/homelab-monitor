import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    title: 'Panel · Homelab Monitor',
    loadComponent: () => import('./pages/panel/panel').then((m) => m.Panel),
  },
  {
    path: 'servicios/nuevo',
    title: 'Nuevo servicio · Homelab Monitor',
    loadComponent: () => import('./pages/formulario/formulario').then((m) => m.Formulario),
  },
  {
    path: 'servicios/:id',
    title: 'Servicio · Homelab Monitor',
    loadComponent: () => import('./pages/detalle/detalle').then((m) => m.Detalle),
  },
  {
    path: 'servicios/:id/editar',
    title: 'Editar servicio · Homelab Monitor',
    loadComponent: () => import('./pages/formulario/formulario').then((m) => m.Formulario),
  },
  { path: '**', redirectTo: '' },
];
