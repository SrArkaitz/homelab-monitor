import { Component, computed, input } from '@angular/core';
import { Comprobacion } from '../core/modelos';

const ANCHO = 800;
const ALTO = 180;
const MARGEN = { arriba: 12, derecha: 8, abajo: 26, izquierda: 44 };

/** Gráfica SVG de latencia en el periodo, con los fallos marcados en rojo. Sin librerías. */
@Component({
  selector: 'app-grafica-latencia',
  template: `
    @if (datos().puntos.length === 0 && datos().fallos.length === 0) {
      <p class="vacio">Todavía no hay comprobaciones en este periodo.</p>
    } @else {
      <svg [attr.viewBox]="'0 0 ' + ancho + ' ' + alto" role="img" [attr.aria-label]="'Latencia de las últimas ' + horas() + ' horas'">
        @for (linea of datos().lineasY; track linea.y) {
          <line class="rejilla" [attr.x1]="margen.izquierda" [attr.x2]="ancho - margen.derecha" [attr.y1]="linea.y" [attr.y2]="linea.y" />
          <text class="eje" [attr.x]="margen.izquierda - 8" [attr.y]="linea.y + 4" text-anchor="end">{{ linea.texto }}</text>
        }
        @for (marca of datos().marcasX; track marca.x) {
          <text class="eje" [attr.x]="marca.x" [attr.y]="alto - 6" text-anchor="middle">{{ marca.texto }}</text>
        }
        <path class="area" [attr.d]="datos().area" />
        <path class="linea" [attr.d]="datos().linea" />
        @for (x of datos().fallos; track $index) {
          <line class="fallo" [attr.x1]="x" [attr.x2]="x" [attr.y1]="margen.arriba" [attr.y2]="alto - margen.abajo" />
        }
      </svg>
    }
  `,
  styles: `
    :host { display: block; }
    svg { width: 100%; height: auto; display: block; overflow: visible; }
    .rejilla { stroke: var(--borde); stroke-width: 1; }
    .eje { fill: var(--texto-3); font: 11px var(--mono); }
    .linea { fill: none; stroke: var(--verde); stroke-width: 1.8; stroke-linejoin: round; vector-effect: non-scaling-stroke; }
    .area { fill: var(--verde-suave); }
    .fallo { stroke: var(--rojo); stroke-width: 2; opacity: 0.75; }
    .vacio { color: var(--texto-3); margin: 2rem 0; text-align: center; }
  `,
})
export class GraficaLatencia {
  readonly comprobaciones = input.required<Comprobacion[]>();
  readonly horas = input(24);

  protected readonly ancho = ANCHO;
  protected readonly alto = ALTO;
  protected readonly margen = MARGEN;

  protected readonly datos = computed(() => {
    const fin = Date.now();
    const inicio = fin - this.horas() * 3_600_000;
    const anchoUtil = ANCHO - MARGEN.izquierda - MARGEN.derecha;
    const altoUtil = ALTO - MARGEN.arriba - MARGEN.abajo;
    const x = (fecha: string) => MARGEN.izquierda + ((new Date(fecha).getTime() - inicio) / (fin - inicio)) * anchoUtil;

    const correctas = this.comprobaciones().filter((c) => c.ok);
    const maximo = escalaBonita(Math.max(50, ...correctas.map((c) => c.latenciaMs)));
    const y = (ms: number) => MARGEN.arriba + altoUtil - (Math.min(ms, maximo) / maximo) * altoUtil;

    const puntos = correctas.map((c) => ({ x: x(c.fecha), y: y(c.latenciaMs) }));
    const linea = puntos.map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' ');
    const base = MARGEN.arriba + altoUtil;
    const area = puntos.length
      ? `${linea} L${puntos[puntos.length - 1].x.toFixed(1)},${base} L${puntos[0].x.toFixed(1)},${base} Z`
      : '';

    const lineasY = [0, 0.5, 1].map((f) => ({ y: y(maximo * f), texto: `${Math.round(maximo * f)} ms` }));

    const pasos = this.horas() <= 24 ? 4 : 7;
    const marcasX = Array.from({ length: pasos + 1 }, (_, i) => {
      const t = inicio + ((fin - inicio) * i) / pasos;
      const fecha = new Date(t);
      const texto =
        this.horas() <= 24
          ? fecha.toLocaleTimeString('es', { hour: '2-digit', minute: '2-digit' })
          : fecha.toLocaleDateString('es', { weekday: 'short', day: 'numeric' });
      return { x: MARGEN.izquierda + (anchoUtil * i) / pasos, texto };
    });

    const fallos = this.comprobaciones()
      .filter((c) => !c.ok)
      .map((c) => x(c.fecha));

    return { puntos, linea, area, lineasY, marcasX, fallos };
  });
}

/** Redondea hacia arriba a 1, 2 o 5 por potencia de diez: 87 → 100, 130 → 200. */
function escalaBonita(valor: number): number {
  const potencia = 10 ** Math.floor(Math.log10(valor));
  const base = valor / potencia;
  const bonito = base <= 1 ? 1 : base <= 2 ? 2 : base <= 5 ? 5 : 10;
  return bonito * potencia;
}
