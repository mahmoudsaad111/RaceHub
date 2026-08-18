import { DecimalPipe, NgStyle } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';

import { CarsService } from '../../core/services/cars.service';
import { SelectedCarService } from '../../core/services/selected-car.service';
import { CarDto } from '../../core/models/car.models';
import { getCarSwatch } from '../../core/config/car-visuals';

interface GarageCarView {
  id: string;
  name: string;
  icon: string;
  color: string;
  equipped: boolean;
  /** Free starters (price 0) count as owned by everyone — no UserCar row needed. */
  owned: boolean;
  price: number;
  topSpeed: number;
  acceleration: number;
  handling: number;
  braking: number;
  nitroCapacity: number;
}

/** A car belongs in the garage when the user bought it (owned flag) or it's a free starter (price 0). */
function isOwned(car: CarDto): boolean {
  return car.owned || car.price <= 0;
}

@Component({
  selector: 'rh-garage',
  standalone: true,
  imports: [DecimalPipe, NgStyle],
  templateUrl: './garage.component.html',
  styleUrl: './garage.component.scss',
})
export class GarageComponent {
  private readonly carsService = inject(CarsService);
  private readonly selectedCarService = inject(SelectedCarService);

  private readonly rawCars = signal<CarDto[]>([]);

  /** The garage shows exactly "my cars": the five free starters every
   * account gets, plus everything purchased from the shop. Buying happens
   * in the shop, not here. */
  readonly cars = computed<GarageCarView[]>(() =>
    this.rawCars()
      .filter((car) => isOwned(car))
      .map((car) => {
        const swatch = getCarSwatch(car.name);
        const selectedId = this.selectedCarService.selectedCarId();
        const equipped = selectedId ? car.id === selectedId : false;

        return {
          id: car.id,
          name: car.name,
          icon: swatch.icon,
          color: swatch.color,
          equipped,
          owned: true,
          price: car.price,
          topSpeed: car.topSpeed,
          acceleration: car.acceleration,
          handling: car.handling,
          braking: car.braking,
          nitroCapacity: car.nitroCapacity,
        };
      })
      .sort((a, b) => a.price - b.price || a.name.localeCompare(b.name)),
  );

  readonly stats = computed(() => {
    const equipped = this.cars().find((c) => c.equipped);

    if (!equipped) {
      return [];
    }

    return [
      { label: 'Speed', value: equipped.topSpeed },
      { label: 'Acceleration', value: equipped.acceleration },
      { label: 'Handling', value: equipped.handling },
      { label: 'Nitro', value: equipped.nitroCapacity },
    ];
  });

  readonly ownedCount = computed(() => this.cars().length);

  constructor() {
    this.carsService.getAll().subscribe((cars) => {
      this.rawCars.set(cars);

      // Default selection: last equipped, else first owned (free starters
      // make this non-empty for brand-new accounts too).
      if (!this.selectedCarService.getSelectedCarId()) {
        const defaultCar = cars.find((c) => isOwned(c));
        if (defaultCar) {
          this.selectedCarService.setSelectedCarId(defaultCar.id);
        }
      }
    });
  }

  selectCar(carId: string): void {
    this.selectedCarService.setSelectedCarId(carId);
  }

  carSwatchStyle(color: string): Record<string, string> {
    return { background: `linear-gradient(150deg, ${color}, #10131c)` };
  }
}
