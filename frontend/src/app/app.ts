import { Component, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { ServiciosStore } from './core/servicios-store';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink],
  template: `
    <header>
      <div class="contenedor barra">
        <a routerLink="/" class="marca">
          <svg viewBox="0 0 32 32" aria-hidden="true">
            <polyline points="4,17 10,17 13,9 18,24 21,14 24,17 28,17" />
          </svg>
          <span>homelab<b>monitor</b></span>
        </a>

        <div class="conexion" [class]="store.conexion()">
          <span class="punto"></span>
          @switch (store.conexion()) {
            @case ('conectado') { En directo }
            @case ('conectando') { Conectando… }
            @case ('reconectando') { Reconectando… }
            @case ('desconectado') {
              Sin conexión
              <button type="button" class="reintentar" (click)="store.reconectar()">Reintentar</button>
            }
          }
        </div>
      </div>
    </header>

    <main class="contenedor">
      <router-outlet />
    </main>
  `,
  styles: `
    header { border-bottom: 1px solid var(--borde); background: var(--superficie); position: sticky; top: 0; z-index: 10; }
    .barra { display: flex; align-items: center; justify-content: space-between; height: 3.5rem; }
    .marca { display: flex; align-items: center; gap: 0.55rem; text-decoration: none; font: 500 1rem var(--mono); }
    .marca b { color: var(--acento); font-weight: 500; }
    .marca svg { width: 1.6rem; height: 1.6rem; fill: none; stroke: var(--acento); stroke-width: 2.6; stroke-linecap: round; stroke-linejoin: round; }
    .conexion { display: flex; align-items: center; gap: 0.45rem; font: 0.8rem var(--mono); color: var(--texto-3); }
    .punto { width: 0.5rem; height: 0.5rem; border-radius: 50%; background: var(--gris); }
    .conectado .punto { background: var(--verde); }
    .desconectado .punto { background: var(--rojo); }
    .reintentar { background: none; border: none; color: var(--acento); font: inherit; cursor: pointer; text-decoration: underline; padding: 0; }
    main { padding-top: 1.75rem; padding-bottom: 3rem; }
  `,
})
export class App {
  protected readonly store = inject(ServiciosStore);
}
