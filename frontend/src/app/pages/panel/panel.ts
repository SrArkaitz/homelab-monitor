import { DecimalPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ServiciosStore } from '../../core/servicios-store';
import { Estado } from '../../shared/estado';
import { haceTiempo, Reloj } from '../../shared/tiempo';

@Component({
  selector: 'app-panel',
  imports: [RouterLink, DecimalPipe, Estado],
  templateUrl: './panel.html',
  styleUrl: './panel.scss',
})
export class Panel {
  protected readonly store = inject(ServiciosStore);
  protected readonly ahora = inject(Reloj).ahora;
  protected readonly haceTiempo = haceTiempo;
}
