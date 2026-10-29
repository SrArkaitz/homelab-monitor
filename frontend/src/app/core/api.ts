import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { Comprobacion, GuardarServicio, ProblemDetails, Servicio } from './modelos';

@Injectable({ providedIn: 'root' })
export class Api {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/servicios';

  listar(): Observable<Servicio[]> {
    return this.http.get<Servicio[]>(this.base);
  }

  obtener(id: number): Observable<Servicio> {
    return this.http.get<Servicio>(`${this.base}/${id}`);
  }

  historial(id: number, horas: number): Observable<Comprobacion[]> {
    return this.http.get<Comprobacion[]>(`${this.base}/${id}/comprobaciones`, { params: { horas } });
  }

  crear(datos: GuardarServicio): Observable<Servicio> {
    return this.http.post<Servicio>(this.base, datos);
  }

  editar(id: number, datos: GuardarServicio): Observable<Servicio> {
    return this.http.put<Servicio>(`${this.base}/${id}`, datos);
  }

  eliminar(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  comprobarAhora(id: number): Observable<Servicio> {
    return this.http.post<Servicio>(`${this.base}/${id}/comprobar`, null);
  }
}

/** Convierte un error HTTP en un mensaje legible para el usuario. */
export function mensajeDeError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return 'No se puede conectar con la API.';
    }
    const problema = error.error as ProblemDetails | null;
    if (problema?.errors) {
      return Object.values(problema.errors).flat().join(' ');
    }
    return problema?.detail ?? problema?.title ?? `Error ${error.status}`;
  }
  return 'Ha ocurrido un error inesperado.';
}
