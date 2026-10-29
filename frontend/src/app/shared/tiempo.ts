import { Injectable, signal } from '@angular/core';

/** Una señal con la hora actual que avanza sola, para que los "hace 2 min" se refresquen. */
@Injectable({ providedIn: 'root' })
export class Reloj {
  readonly ahora = signal(Date.now());

  constructor() {
    setInterval(() => this.ahora.set(Date.now()), 15_000);
  }
}

export function haceTiempo(fecha: string | null, ahora: number): string {
  if (!fecha) return 'nunca';
  const segundos = Math.max(0, Math.round((ahora - new Date(fecha).getTime()) / 1000));
  if (segundos < 10) return 'ahora mismo';
  if (segundos < 60) return `hace ${segundos} s`;
  const minutos = Math.floor(segundos / 60);
  if (minutos < 60) return `hace ${minutos} min`;
  const horas = Math.floor(minutos / 60);
  if (horas < 24) return `hace ${horas} h`;
  const dias = Math.floor(horas / 24);
  return `hace ${dias} ${dias === 1 ? 'día' : 'días'}`;
}

export function duracion(desde: string | null, ahora: number): string {
  if (!desde) return '—';
  const minutos = Math.max(0, Math.floor((ahora - new Date(desde).getTime()) / 60_000));
  if (minutos < 1) return 'menos de 1 min';
  if (minutos < 60) return `${minutos} min`;
  const horas = Math.floor(minutos / 60);
  if (horas < 24) return `${horas} h ${minutos % 60} min`;
  const dias = Math.floor(horas / 24);
  return `${dias} d ${horas % 24} h`;
}
