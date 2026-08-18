/** Mirrors backend RaceHub.Application.DTOs.Users.RecentRaceDto. */
export interface RecentRaceDto {
  raceId: string;
  trackName: string;
  finishingPosition: number;
  totalRaceTime: string; // TimeSpan serializes as "hh:mm:ss.fff"
  createdAtUtc: string;
}

/** Mirrors backend RaceHub.Application.DTOs.Users.ProfileDto. */
export interface ProfileDto {
  userId: string;
  email: string;
  displayName: string;
  experience: number;
  coins: number;
  level: number;
  xpIntoLevel: number;
  xpForNextLevel: number;
  totalRaces: number;
  wins: number;
  bestLapTime: string | null;
  recentRaces: RecentRaceDto[];
  /** Elo-style rating maintained asynchronously by RankingWorker. */
  ratingPoints: number;
}
