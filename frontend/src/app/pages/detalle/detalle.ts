import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { Api, mensajeDeError } from '../../core/api';
import { Comprobacion, GuardarServicio, Servicio } from '../../core/modelos';
import { ServiciosStore } from '../../core/servicios-store';
import { BarraUptime } from '../../shared/barra-uptime';
import { Estado } from '../../shared/estado';
import { GraficaLatencia } from '../../shared/grafica-latencia';
import { duracion, haceTiempo, Reloj } from '../../shared/tiempo';

@Component({
  selector: 'app-detalle',
  imports: [RouterLink, DatePipe, DecimalPipe, Estado, GraficaLatencia, BarraUptime],
  templateUrl: './detalle.html',
  styleUrl: './detalle.scss',
})
export class Detalle {
  private readonly api = inject(Api);
  private readonly store = inject(ServiciosStore);
  private readonly router = inject(Router);
  protected readonly ahora = inject(Reloj).ahora;
  protected readonly haceTiempo = haceTiempo;
  protected readonly duracion = duracion;

  /** Viene del parámetro :id de la ruta gracias a withComponentInputBinding(). */
  readonly id = input.required<string>();

  protected readonly periodos = [
    { horas: 6, texto: '6 h' },
    { horas: 24, texto: '24 h' },
    { horas: 168, texto: '7 días' },
  ];
  protected readonly horas = signal(24);
  protected readonly historial = signal<Comprobacion[]>([]);
  protected readonly cargandoHistorial = signal(true);
  protected readonly ocupado = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly noEncontrado = signal(false);

  private readonly idNumerico = computed(() => Number(this.id()));

  /** El servicio sale del store, así se actualiza solo con cada mensaje de SignalR. */
  protected readonly servicio = computed(() => this.store.porId(this.idNumerico()));

  protected readonly ultimosFallos = computed(() =>
    this.historial()
      .filter((c) => !c.ok)
      .slice(-8)
      .reverse(),
  );

  constructor() {
    // Si el servicio no está en el store (entrada directa por URL), lo pedimos.
    effect(() => {
      const id = this.idNumerico();
      if (!untracked(() => this.store.porId(id))) {
        this.api.obtener(id).subscribe({
          next: (s) => this.store.aplicar(s),
          error: () => this.noEncontrado.set(true),
        });
      }
    });

    // Recarga el histórico al cambiar el periodo o al llegar una comprobación nueva.
    effect(() => {
      const id = this.idNumerico();
      const horas = this.horas();
      void this.servicio()?.ultimaComprobacion;
      untracked(() => this.cargarHistorial(id, horas));
    });
  }

  protected comprobarAhora(): void {
    this.ejecutar(this.api.comprobarAhora(this.idNumerico()), (s) => this.store.aplicar(s));
  }

  protected alternarPausa(s: Servicio): void {
    const datos: GuardarServicio = {
      nombre: s.nombre,
      destino: s.destino,
      tipo: s.tipo,
      intervaloSegundos: s.intervaloSegundos,
      timeoutSegundos: s.timeoutSegundos,
      ignorarCertificado: s.ignorarCertificado,
      activo: !s.activo,
    };
    this.ejecutar(this.api.editar(s.id, datos), (actualizado) => this.store.aplicar(actualizado));
  }

  protected eliminar(s: Servicio): void {
    if (!confirm(`¿Eliminar "${s.nombre}" y todo su histórico?`)) {
      return;
    }
    this.ejecutar(this.api.eliminar(s.id), () => {
      this.store.quitar(s.id);
      void this.router.navigate(['/']);
    });
  }

  private cargarHistorial(id: number, horas: number): void {
    this.api.historial(id, horas).subscribe({
      next: (h) => {
        this.historial.set(h);
        this.cargandoHistorial.set(false);
      },
      error: (e) => {
        this.error.set(mensajeDeError(e));
        this.cargandoHistorial.set(false);
      },
    });
  }

  private ejecutar<T>(peticion: Observable<T>, alTerminar: (r: T) => void): void {
    this.ocupado.set(true);
    this.error.set(null);
    peticion.subscribe({
      next: (r) => {
        alTerminar(r);
        this.ocupado.set(false);
      },
      error: (e) => {
        this.error.set(mensajeDeError(e));
        this.ocupado.set(false);
      },
    });
  }
}
