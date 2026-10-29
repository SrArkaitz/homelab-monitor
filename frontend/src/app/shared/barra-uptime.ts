import { Component, computed, input } from '@angular/core';
import { Comprobacion } from '../core/modelos';

type Tramo = { clase: 'ok' | 'parcial' | 'caido' | 'vacio'; titulo: string };

/** Franja de tramos de colores: verde si todo fue bien, ámbar si hubo fallos, rojo si estuvo caído. */
@Component({
  selector: 'app-barra-uptime',
  template: `
    <div class="barra">
      @for (tramo of tramos(); track $index) {
        <span [class]="tramo.clase" [attr.title]="tramo.titulo"></span>
      }
    </div>
  `,
  styles: `
    .barra { display: flex; gap: 2px; height: 2rem; }
    span { flex: 1; border-radius: 2px; background: var(--gris-suave); }
    .ok { background: var(--verde); }
    .parcial { background: var(--ambar); }
    .caido { background: var(--rojo); }
  `,
})
export class BarraUptime {
  readonly comprobaciones = input.required<Comprobacion[]>();
  readonly horas = input(24);
  readonly numeroTramos = input(48);

  protected readonly tramos = computed<Tramo[]>(() => {
    const n = this.numeroTramos();
    const fin = Date.now();
    const inicio = fin - this.horas() * 3_600_000;
    const tamano = (fin - inicio) / n;
    const grupos = Array.from({ length: n }, () => ({ total: 0, ok: 0 }));

    for (const c of this.comprobaciones()) {
      const i = Math.floor((new Date(c.fecha).getTime() - inicio) / tamano);
      if (i >= 0 && i < n) {
        grupos[i].total++;
        if (c.ok) grupos[i].ok++;
      }
    }

    return grupos.map((g, i): Tramo => {
      const desde = new Date(inicio + i * tamano).toLocaleString('es', { weekday: 'short', hour: '2-digit', minute: '2-digit' });
      if (g.total === 0) return { clase: 'vacio', titulo: `${desde} · sin datos` };
      const porcentaje = Math.round((g.ok / g.total) * 100);
      const clase: Tramo['clase'] = g.ok === g.total ? 'ok' : g.ok === 0 ? 'caido' : 'parcial';
      return { clase, titulo: `${desde} · ${porcentaje}% (${g.ok}/${g.total})` };
    });
  });
}
