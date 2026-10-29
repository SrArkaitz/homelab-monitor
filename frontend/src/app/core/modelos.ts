export type TipoComprobacion = 'Http' | 'Tcp';
export type EstadoServicio = 'Desconocido' | 'Activo' | 'Caido';

export interface Servicio {
  id: number;
  nombre: string;
  destino: string;
  tipo: TipoComprobacion;
  intervaloSegundos: number;
  timeoutSegundos: number;
  ignorarCertificado: boolean;
  activo: boolean;
  estado: EstadoServicio;
  ultimaComprobacion: string | null;
  estadoDesde: string | null;
  uptime24h: number | null;
  latenciaMedia24hMs: number | null;
  ultimaLatenciaMs: number | null;
  ultimoError: string | null;
}

export interface GuardarServicio {
  nombre: string;
  destino: string;
  tipo: TipoComprobacion;
  intervaloSegundos: number;
  timeoutSegundos: number;
  ignorarCertificado: boolean;
  activo: boolean;
}

export interface Comprobacion {
  fecha: string;
  ok: boolean;
  codigoHttp: number | null;
  latenciaMs: number;
  error: string | null;
}

/** Errores de la API en formato ProblemDetails (RFC 9457). */
export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}
