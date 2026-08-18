import { Injectable, signal } from '@angular/core';

import { RaceDetailDto, RaceFinishedDto } from '../models/race-api.models';

/**
 * Minimal signal-based store carrying the "current race" across screens
 * that don't have a route param for it (race/results). Deliberately not a
 * full state-management library — just enough to hand RaceDetailDto from
 * RoomComponent to RaceComponent, and RaceFinishedDto from RaceComponent
 * to ResultsComponent, without a round-trip back to the API.
 */
@Injectable({ providedIn: 'root' })
export class ActiveRaceStore {
  readonly currentRace = signal<RaceDetailDto | null>(null);
  readonly lastResults = signal<RaceFinishedDto | null>(null);

  setCurrentRace(race: RaceDetailDto | null): void {
    this.currentRace.set(race);
  }

  setLastResults(results: RaceFinishedDto | null): void {
    this.lastResults.set(results);
  }
}
