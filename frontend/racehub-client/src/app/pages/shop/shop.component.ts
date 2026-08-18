import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';

import { CarsService } from '../../core/services/cars.service';
import { ProfileService } from '../../core/services/profile.service';
import { CarDto } from '../../core/models/car.models';
import { getCarSwatch } from '../../core/config/car-visuals';

export type CarTier = 'STARTER' | 'ENTRY' | 'SPORT' | 'PERFORMANCE' | 'SUPER' | 'HYPER';

/** Price bands shared by the shop badges — mirrors the backend catalog tiers. */
export function tierForPrice(price: number): CarTier {
  if (price <= 0) return 'STARTER';
  if (price < 2000) return 'ENTRY';
  if (price < 8000) return 'SPORT';
  if (price < 15000) return 'PERFORMANCE';
  if (price < 30000) return 'SUPER';
  return 'HYPER';
}

/**
 * Brand is derived from the car *name* rather than being a backend field —
 * the catalog names lead with the marque ("Porsche 911 GT3 RS"), so the
 * first token is the brand for everything except the hyphenated and
 * two-word marques handled explicitly below. Legacy cars have no marque
 * and show as "RaceHub Originals".
 */
const MULTI_WORD_BRANDS = ['Mercedes-AMG', 'Aston Martin', 'Alfa Romeo', 'Rolls-Royce'];

export function brandForName(name: string): string {
  const multi = MULTI_WORD_BRANDS.find((b) => name.startsWith(b + ' '));
  if (multi) return multi;

  const first = name.split(' ')[0];
  const branded = ['Mini', 'VW', 'Toyota', 'Honda', 'Hyundai', 'BMW', 'Nissan', 'Audi', 'Lexus', 'Chevrolet', 'Porsche', 'McLaren', 'Ferrari', 'Lamborghini', 'Bugatti', 'Koenigsegg'];
  return branded.includes(first) ? first : 'RaceHub';
}

interface ShopCarView {
  id: string;
  name: string;
  brand: string;
  tier: CarTier;
  icon: string;
  color: string;
  price: number;
  topSpeed: number;
  acceleration: number;
  handling: number;
}

@Component({
  selector: 'rh-shop',
  standalone: true,
  imports: [DecimalPipe],
  templateUrl: './shop.component.html',
  styleUrl: './shop.component.scss',
})
export class ShopComponent {
  private readonly carsService = inject(CarsService);
  private readonly profileService = inject(ProfileService);

  readonly cars = signal<CarDto[]>([]);
  readonly buyingId = signal<string | null>(null);
  readonly buyError = signal<string | null>(null);
  readonly coinBalance = signal<number | null>(null);

  /** Tier filter — null shows everything. STARTER never appears because
   * the shop only lists paid, not-yet-owned cars (free starters live in
   * the garage, and owned cars leave the shop once purchased). */
  readonly tierFilter = signal<CarTier | null>(null);

  readonly tiers: (CarTier | null)[] = [null, 'ENTRY', 'SPORT', 'PERFORMANCE', 'SUPER', 'HYPER'];

  constructor() {
    this.carsService.getAll().subscribe((data) => this.cars.set(data));
    this.refreshBalance();
  }

  readonly shopItems = computed(() =>
    this.cars()
      // The shop is exactly "what you can buy next": paid cars you don't
      // own yet. Free starters and anything already in your garage are
      // filtered out — the garage is the collection view.
      .filter((car) => car.price > 0 && !car.owned)
      .map((car) => {
        const swatch = getCarSwatch(car.name);
        return {
          id: car.id,
          name: car.name,
          brand: brandForName(car.name),
          tier: tierForPrice(car.price),
          icon: swatch.icon,
          color: swatch.color,
          price: car.price,
          topSpeed: car.topSpeed,
          acceleration: car.acceleration,
          handling: car.handling,
        } satisfies ShopCarView;
      })
      // Cheapest first so the shop reads as a progression: what you can
      // afford now at the top, the end-game grins at the bottom.
      .sort((a, b) => a.price - b.price || a.name.localeCompare(b.name)),
  );

  readonly visibleItems = computed(() => {
    const tier = this.tierFilter();
    return tier ? this.shopItems().filter((i) => i.tier === tier) : this.shopItems();
  });

  setTier(tier: CarTier | null): void {
    this.tierFilter.set(tier);
  }

  swatchStyle(item: ShopCarView): string {
    return `linear-gradient(150deg, ${item.color}, #10131c)`;
  }

  buyCar(carId: string): void {
    this.buyingId.set(carId);
    this.buyError.set(null);

    this.carsService.buy(carId).subscribe({
      next: () => {
        this.cars.update((list) =>
          list.map((c) => (c.id === carId ? { ...c, owned: true } : c)),
        );
        this.buyingId.set(null);
        // BuyCar spent real coins server-side — pull the authoritative
        // balance rather than guessing it client-side.
        this.refreshBalance();
      },
      error: (err) => {
        this.buyError.set(err?.error?.message ?? err?.error?.errorCode ?? 'Purchase failed.');
        this.buyingId.set(null);
      },
    });
  }

  private refreshBalance(): void {
    this.profileService.getMyProfile().subscribe((profile) => {
      if (profile) {
        this.coinBalance.set(profile.coins);
      }
    });
  }
}
