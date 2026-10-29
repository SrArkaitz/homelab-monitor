import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Api, mensajeDeError } from '../../core/api';
import { GuardarServicio, TipoComprobacion } from '../../core/modelos';
import { ServiciosStore } from '../../core/servicios-store';

type Campo = 'nombre' | 'destino' | 'intervaloSegundos' | 'timeoutSegundos';

@Component({
  selector: 'app-formulario',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './formulario.html',
  styleUrl: './formulario.scss',
})
export class Formulario {
  private readonly api = inject(Api);
  private readonly store = inject(ServiciosStore);
  private readonly router = inject(Router);

  /** Solo existe al editar (ruta servicios/:id/editar). */
  readonly id = input<string>();

  protected readonly editando = computed(() => this.id() !== undefined);
  protected readonly guardando = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group(
    {
      nombre: ['', [Validators.required, Validators.maxLength(100)]],
      tipo: ['Http' as TipoComprobacion],
      destino: ['', [Validators.required, Validators.maxLength(500)]],
      intervaloSegundos: [60, [Validators.required, Validators.min(10), Validators.max(86_400)]],
      timeoutSegundos: [10, [Validators.required, Validators.min(1), Validators.max(60)]],
      ignorarCertificado: [false],
      activo: [true],
    },
    { validators: [destinoValido, timeoutMenorQueIntervalo] },
  );

  protected readonly tipo = toSignal(this.form.controls.tipo.valueChanges, { initialValue: this.form.controls.tipo.value });

  constructor() {
    effect(() => {
      const id = this.id();
      if (id === undefined) return;

      this.api.obtener(Number(id)).subscribe({
        next: (s) =>
          this.form.setValue({
            nombre: s.nombre,
            tipo: s.tipo,
            destino: s.destino,
            intervaloSegundos: s.intervaloSegundos,
            timeoutSegundos: s.timeoutSegundos,
            ignorarCertificado: s.ignorarCertificado,
            activo: s.activo,
          }),
        error: (e) => this.error.set(mensajeDeError(e)),
      });
    });
  }

  protected guardar(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const datos: GuardarServicio = this.form.getRawValue();
    if (datos.tipo === 'Tcp') datos.ignorarCertificado = false;

    const id = this.id();
    const peticion = id === undefined ? this.api.crear(datos) : this.api.editar(Number(id), datos);

    this.guardando.set(true);
    this.error.set(null);
    peticion.subscribe({
      next: (s) => {
        this.store.aplicar(s);
        void this.router.navigate(['/servicios', s.id]);
      },
      error: (e) => {
        this.error.set(mensajeDeError(e));
        this.guardando.set(false);
      },
    });
  }

  protected invalido(campo: Campo): boolean {
    const control = this.form.controls[campo];
    return control.invalid && control.touched;
  }
}

/** Misma regla que en el backend: URL http(s) para HTTP, host:puerto para TCP. */
function destinoValido(grupo: AbstractControl): ValidationErrors | null {
  const tipo = grupo.get('tipo')?.value as TipoComprobacion;
  const destino = String(grupo.get('destino')?.value ?? '').trim();
  if (!destino) return null;

  if (tipo === 'Http') {
    return /^https?:\/\/[^\s]+$/i.test(destino) ? null : { destino: 'Debe empezar por http:// o https://' };
  }

  const coincide = /^([^\s:]+|\[[^\]]+\]|[^\s]+):(\d{1,5})$/.exec(destino);
  const puerto = coincide ? Number(coincide[2]) : 0;
  return puerto > 0 && puerto <= 65535 ? null : { destino: 'Formato host:puerto, por ejemplo 192.168.1.138:22' };
}

function timeoutMenorQueIntervalo(grupo: AbstractControl): ValidationErrors | null {
  const timeout = Number(grupo.get('timeoutSegundos')?.value);
  const intervalo = Number(grupo.get('intervaloSegundos')?.value);
  return timeout < intervalo ? null : { timeout: 'El timeout tiene que ser menor que el intervalo' };
}
