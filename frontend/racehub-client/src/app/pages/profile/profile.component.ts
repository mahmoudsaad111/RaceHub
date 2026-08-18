import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';

import { ProfileService } from '../../core/services/profile.service';
import { AchievementsService } from '../../core/services/achievements.service';
import { ProfileDto } from '../../core/models/profile.models';
import { AchievementDto } from '../../core/models/api.models';

interface RecentRaceView {
  icon: string;
  track: string;
  position: string;
  time: string;
}

function formatRaceTime(timeSpan: string | null): string {
  if (!timeSpan) {
    return '--:--.--';
  }

  const withoutDays = timeSpan.includes('.') && timeSpan.split(':')[0].includes('.')
    ? timeSpan.substring(timeSpan.indexOf('.') + 1)
    : timeSpan;

  const [h, m, sFull] = withoutDays.split(':');
  const [s, fraction = '0'] = sFull.split('.');

  const totalMinutes = Number(h) * 60 + Number(m);
  const centiseconds = fraction.padEnd(2, '0').slice(0, 2);

  return `${String(totalMinutes).padStart(2, '0')}:${s.padStart(2, '0')}.${centiseconds}`;
}

const POSITION_SUFFIXES: Record<number, string> = { 1: 'st', 2: 'nd', 3: 'rd' };

function ordinal(position: number): string {
  const suffix = position >= 11 && position <= 13 ? 'th' : (POSITION_SUFFIXES[position % 10] ?? 'th');
  return `${position}${suffix}`;
}

@Component({
  selector: 'rh-profile',
  standalone: true,
  imports: [DecimalPipe],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
})
export class ProfileComponent {
  private readonly profileService = inject(ProfileService);
  private readonly achievementsService = inject(AchievementsService);

  private readonly profile = signal<ProfileDto | null>(null);
  readonly achievements = signal<AchievementDto[]>([]);

  readonly displayName = computed(() => this.profile()?.displayName ?? 'Racer');
  readonly level = computed(() => this.profile()?.level ?? 1);
  readonly xpCurrent = computed(() => this.profile()?.xpIntoLevel ?? 0);
  readonly xpTotal = computed(() => this.profile()?.xpForNextLevel ?? 1000);
  readonly coins = computed(() => this.profile()?.coins ?? 0);
  readonly wins = computed(() => this.profile()?.wins ?? 0);
  readonly races = computed(() => this.profile()?.totalRaces ?? 0);
  readonly bestTime = computed(() => formatRaceTime(this.profile()?.bestLapTime ?? null));
  readonly rating = computed(() => this.profile()?.ratingPoints ?? 0);

  readonly unlockedCount = computed(() => this.achievements().filter((a) => a.unlocked).length);

  readonly recentRaces = computed<RecentRaceView[]>(() =>
    (this.profile()?.recentRaces ?? []).map((r) => ({
      icon: '🏁',
      track: r.trackName,
      position: ordinal(r.finishingPosition),
      time: formatRaceTime(r.totalRaceTime),
    })),
  );

  readonly xpPercent = computed(() => {
    const total = this.xpTotal();
    return total > 0 ? Math.round((this.xpCurrent() / total) * 100) : 0;
  });

  constructor() {
    this.profileService.getMyProfile().subscribe((profile) => this.profile.set(profile));
    this.achievementsService.getMine().subscribe((achievements) => this.achievements.set(achievements));
  }
}
