import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { LeaderboardEntryDto, TrackDto } from '../../core/models/api.models';
import { LeaderboardsService } from '../../core/services/leaderboards.service';
import { TracksService } from '../../core/services/tracks.service';
import { FriendsService } from '../../core/services/friends.service';
import { AuthService } from '../../core/services/auth.service';

type LeaderboardTab = 'GLOBAL' | 'WEEKLY' | 'TRACK' | 'FRIENDS';

@Component({
  selector: 'rh-leaderboard',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './leaderboard.component.html',
  styleUrl: './leaderboard.component.scss',
})
export class LeaderboardComponent {
  private readonly leaderboardsService = inject(LeaderboardsService);
  private readonly tracksService = inject(TracksService);
  private readonly friendsService = inject(FriendsService);
  private readonly authService = inject(AuthService);

  readonly tabs: LeaderboardTab[] = ['GLOBAL', 'WEEKLY', 'TRACK', 'FRIENDS'];
  activeTab = signal<LeaderboardTab>('GLOBAL');

  entries: LeaderboardEntryDto[] = [];
  loading = true;
  errorMessage: string | null = null;

  tracks: TrackDto[] = [];
  trackId = '';
  private loadedTracks = false;

  constructor() {
    this.loadLeaderboard();
  }

  selectTab(tab: LeaderboardTab): void {
    this.activeTab.set(tab);

    if (tab === 'TRACK' && !this.loadedTracks) {
      this.loadedTracks = true;
      this.tracksService.getAll().subscribe((tracks) => {
        this.tracks = tracks;
        this.trackId ||= tracks[0]?.id ?? '';
        this.loadLeaderboard();
      });
      return;
    }

    this.loadLeaderboard();
  }

  selectTrack(): void {
    this.loadLeaderboard();
  }

  rank(index: number): string {
    if (index === 0) return '🥇';
    if (index === 1) return '🥈';
    if (index === 2) return '🥉';
    return String(index + 1);
  }

  bestTime(entry: LeaderboardEntryDto): string {
    if (!entry.bestTime) return '--:--.--';
    return entry.bestTime;
  }

  isMe(entry: LeaderboardEntryDto): boolean {
    const currentUserId = this.authService.currentUser()?.userId;
    return !!currentUserId && entry.userId === currentUserId;
  }

  private loadLeaderboard(): void {
    this.loading = true;
    this.errorMessage = null;

    // GLOBAL ranks by RatingPoints — the Elo-style rating RankingWorker
    // maintains asynchronously off race.finished events, which is what
    // earns it the "leaderboard" name. WEEKLY/TRACK read
    // StatisticsWorker's RaceHistoryEntry read model instead.
    const request =
      this.activeTab() === 'TRACK' && this.trackId
        ? this.leaderboardsService.get('track', this.trackId)
        : this.activeTab() === 'WEEKLY'
          ? this.leaderboardsService.get('weekly')
          : this.leaderboardsService.get('global');

    request.subscribe({
      next: (entries) => {
        // FRIENDS has no server-side scope — it's the global rating ladder
        // filtered client-side to accepted friends (plus yourself, so you
        // can see where you stand among them).
        if (this.activeTab() === 'FRIENDS') {
          this.friendsService.getFriends().subscribe({
            next: (friends) => {
              const visibleIds = new Set<string>(friends.map((f) => f.userId));
              const me = this.authService.currentUser()?.userId;
              if (me) {
                visibleIds.add(me);
              }
              this.entries = entries.filter((e) => visibleIds.has(e.userId));
              this.loading = false;
            },
            error: () => {
              this.entries = [];
              this.loading = false;
              this.errorMessage = 'Could not load friends.';
            },
          });
          return;
        }

        this.entries = entries;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err?.message ?? 'Could not load leaderboard.';
      },
    });
  }
}
