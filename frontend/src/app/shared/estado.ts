import { Component, computed, input } from '@angular/core';
import { EstadoServicio } from '../core/modelos';

/** Punto de color con el estado del servicio. Late cuando está caído. */
@Component({
  selector: 'app-estado',
  template: `<span class="punto" [class]="clase()" [attr.title]="texto()"></span>
    @if (conTexto()) {
      <span class="texto">{{ texto() }}</span>
    }`,
  styles: `
    :host { display: inline-flex; align-items: center; gap: 0.5rem; }
    .punto { width: 0.65rem; height: 0.65rem; border-radius: 50%; flex-shrink: 0; background: var(--gris); }
    .activo { background: var(--verde); box-shadow: 0 0 0 3px var(--verde-suave); }
    .caido { background: var(--rojo); animation: latido 1.4s ease-in-out infinite; }
    .pausado { background: transparent; border: 2px solid var(--gris); }
    .texto { font-size: 0.85rem; color: var(--texto-2); }
    @keyframes latido {
      0%, 100% { box-shadow: 0 0 0 0 var(--rojo-suave); }
      50% { box-shadow: 0 0 0 6px transparent; }
    }
    @media (prefers-reduced-motion: reduce) { .caido { animation: none; } }
  `,
})
export class Estado {
  readonly estado = input.required<EstadoServicio>();
  readonly activo = input(true);
  readonly conTexto = input(false);

  protected readonly clase = computed(() =>
    !this.activo() ? 'pausado' : this.estado() === 'Activo' ? 'activo' : this.estado() === 'Caido' ? 'caido' : 'desconocido',
  );

  protected readonly texto = computed(() =>
    !this.activo() ? 'Pausado' : { Activo: 'Funcionando', Caido: 'Caído', Desconocido: 'Sin datos' }[this.estado()],
  );
}
