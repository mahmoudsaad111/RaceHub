import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { ActiveRaceStore } from '../../core/services/active-race.store';
import { RealtimeService } from '../../core/services/realtime.service';
import { RaceFinishedDto } from '../../core/models/race-api.models';

function medalFor(position: number): { cls: string; label: string } {
  switch (position) {
    case 1: return { cls: 'medal-1', label: '🥇' };
    case 2: return { cls: 'medal-2', label: '🥈' };
    case 3: return { cls: 'medal-3', label: '🥉' };
    default: return { cls: 'medal-none', label: String(position) };
  }
}

@Component({
  selector: 'rh-results',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './results.component.html',
  styleUrl: './results.component.scss',
})
export class ResultsComponent {
  private readonly activeRaceStore = inject(ActiveRaceStore);
  private readonly realtime = inject(RealtimeService);

  results: { position: number; medalClass: string; medalLabel: string; player: string; time: string; bestLap: string; reward: string; coins: string; credited: boolean }[] = [];

  constructor() {
    const finished = this.activeRaceStore.lastResults();

    if (finished) {
      this.results = finished.results.map((row) => {
        const medal = medalFor(row.position);
        const reward = `+${row.experienceEarned}`;
        const coins = `+${row.coinsEarned}`;

        return {
          position: row.position,
          medalClass: medal.cls,
          medalLabel: medal.label,
          player: row.displayName,
          time: formatTime(row.totalTimeMs),
          bestLap: row.bestLapTimeMs != null ? formatTime(row.bestLapTimeMs) : '--:--.--',
          reward,
          coins,
          credited: false,
        };
      });
    }

    this.realtime.rewardCredited$.subscribe((evt) => {
      const finished = this.activeRaceStore.lastResults();
      const userName = finished?.results.find((row) => row.userId === evt.userId)?.displayName;

      if (userName) {
        this.results = this.results.map((r) =>
          r.player === userName ? { ...r, credited: true } : r,
        );
      }
    });
  }

  trackByResult(_index: number, row: { player: string; position: number }) {
    return `${row.player}-${row.position}`;
  }
}

function formatTime(ms: number): string {
  const totalSeconds = ms / 1000;
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  const centiseconds = Math.floor((seconds - Math.floor(seconds)) * 100);

  return `${String(minutes).padStart(2, '0')}:${String(Math.floor(seconds)).padStart(2, '0')}.${String(centiseconds).padStart(2, '0')}`;
}