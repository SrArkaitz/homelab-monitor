import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Api, mensajeDeError } from './api';
import { Servicio } from './modelos';

export type EstadoConexion = 'conectando' | 'conectado' | 'reconectando' | 'desconectado';

/**
 * Estado compartido de la app: la lista de servicios, siempre al día.
 * Carga la lista por HTTP una vez y luego la mantiene con los mensajes de SignalR.
 */
@Injectable({ providedIn: 'root' })
export class ServiciosStore {
  private readonly api = inject(Api);
  private readonly hub: HubConnection;

  private readonly _servicios = signal<Servicio[]>([]);
  private readonly _cargando = signal(true);
  private readonly _error = signal<string | null>(null);
  private readonly _conexion = signal<EstadoConexion>('conectando');

  readonly servicios = computed(() =>
    [...this._servicios()].sort((a, b) => ordenEstado(a) - ordenEstado(b) || a.nombre.localeCompare(b.nombre, 'es')),
  );
  readonly cargando = this._cargando.asReadonly();
  readonly error = this._error.asReadonly();
  readonly conexion = this._conexion.asReadonly();

  readonly resumen = computed(() => {
    const vigilados = this._servicios().filter((s) => s.activo);
    const conUptime = vigilados.filter((s) => s.uptime24h !== null);
    return {
      total: vigilados.length,
      activos: vigilados.filter((s) => s.estado === 'Activo').length,
      caidos: vigilados.filter((s) => s.estado === 'Caido').length,
      desconocidos: vigilados.filter((s) => s.estado === 'Desconocido').length,
      uptimeMedio: conUptime.length
        ? conUptime.reduce((suma, s) => suma + (s.uptime24h ?? 0), 0) / conUptime.length
        : null,
    };
  });

  constructor() {
    this.hub = new HubConnectionBuilder()
      .withUrl('/hubs/estado')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    this.hub.on('ServicioActualizado', (servicio: Servicio) => this.aplicar(servicio));
    this.hub.on('ServicioEliminado', (id: number) => this.quitar(id));

    this.hub.onreconnecting(() => this._conexion.set('reconectando'));
    this.hub.onreconnected(() => {
      this._conexion.set('conectado');
      // Al volver puede que nos hayamos perdido mensajes: recargamos la lista.
      this.cargar();
    });
    this.hub.onclose(() => this._conexion.set('desconectado'));

    inject(DestroyRef).onDestroy(() => void this.hub.stop());

    this.cargar();
    void this.conectar();
  }

  cargar(): void {
    this.api.listar().subscribe({
      next: (servicios) => {
        this._servicios.set(servicios);
        this._error.set(null);
        this._cargando.set(false);
      },
      error: (e) => {
        this._error.set(mensajeDeError(e));
        this._cargando.set(false);
      },
    });
  }

  porId(id: number): Servicio | undefined {
    return this._servicios().find((s) => s.id === id);
  }

  /** Inserta o reemplaza un servicio (respuesta de la API o mensaje del hub). */
  aplicar(servicio: Servicio): void {
    this._servicios.update((lista) =>
      lista.some((s) => s.id === servicio.id)
        ? lista.map((s) => (s.id === servicio.id ? servicio : s))
        : [...lista, servicio],
    );
  }

  quitar(id: number): void {
    this._servicios.update((lista) => lista.filter((s) => s.id !== id));
  }

  async reconectar(): Promise<void> {
    if (this.hub.state === HubConnectionState.Disconnected) {
      await this.conectar();
      this.cargar();
    }
  }

  private async conectar(): Promise<void> {
    this._conexion.set('conectando');
    try {
      await this.hub.start();
      this._conexion.set('conectado');
    } catch {
      this._conexion.set('desconectado');
    }
  }
}

/** Primero los caídos, luego los desconocidos, luego los activos y al final los pausados. */
function ordenEstado(s: Servicio): number {
  if (!s.activo) return 3;
  return { Caido: 0, Desconocido: 1, Activo: 2 }[s.estado];
}
