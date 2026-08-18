import { Injectable, signal } from '@angular/core';

const STORAGE_KEY = 'racehub.selectedCarId';

/**
 * Single source of truth for "which car does the current user race with".
 *
 * Previously every call site that needed a car for a race (createRoom,
 * joinRoom, joinFriendRace, acceptInvite, joinRace) independently fetched
 * the car catalog and just used cars[0] — the Garage's "equip" selection
 * lived only in a local component signal that reset the moment you
 * navigated away, so it was never actually consulted anywhere. That's why
 * changing your car in the Garage had no effect once you actually raced.
 *
 * This persists the choice in localStorage (survives reloads/navigation)
 * and exposes it as a signal so GarageComponent's UI stays reactive.
 */
@Injectable({ providedIn: 'root' })
export class SelectedCarService {
  private readonly _selectedCarId = signal<string | null>(this.readFromStorage());

  readonly selectedCarId = this._selectedCarId.asReadonly();

  getSelectedCarId(): string | null {
    return this._selectedCarId();
  }

  setSelectedCarId(carId: string): void {
    this._selectedCarId.set(carId);

    try {
      localStorage.setItem(STORAGE_KEY, carId);
    } catch {
      // Storage can be unavailable (private browsing, quota) — the
      // in-memory signal still keeps the selection working for this tab.
    }
  }

  /**
   * Resolves the car to actually race with: whatever's selected, as long
   * as it still exists in the current catalog (a previously-equipped car
   * could in principle be removed/deactivated); otherwise falls back to
   * the first available car so join/create flows always have something
   * usable instead of failing outright.
   */
  resolveCarId(availableCarIds: string[]): string | undefined {
    const selected = this._selectedCarId();

    if (selected && availableCarIds.includes(selected)) {
      return selected;
    }

    return availableCarIds[0];
  }

  private readFromStorage(): string | null {
    try {
      return localStorage.getItem(STORAGE_KEY);
    } catch {
      return null;
    }
  }
}
