/**
 * Mirrors RaceHub.Application.DTOs.* records exactly (System.Text.Json's
 * default camelCase policy is what actually reaches the wire, matching
 * ApiResponse<T> in api-response.model.ts). Split from race.models.ts,
 * which holds MockDataService's UI-only view-models — those get replaced
 * page-by-page as each screen switches over to these real DTOs.
 */

// ---- Tracks ----

export interface TrackCheckpointDto {
  sequence: number;
  positionX: number;
  positionY: number;
  width: number;
}

export interface TrackDto {
  id: string;
  name: string;
  description: string;
  totalLaps: number;
  difficulty: number;
  checkpoints: TrackCheckpointDto[];
}

// ---- Cars ----

export interface CarDto {
  id: string;
  name: string;
  topSpeed: number;
  acceleration: number;
  handling: number;
  braking: number;
  nitroCapacity: number;
  isActive: boolean;
}

// ---- Races ----

/** Row in the lobby's open-room list. Mirrors OpenRaceDto. */
export interface OpenRaceDto {
  id: string;
  trackName: string;
  currentPlayers: number;
  maxPlayers: number;
  totalLaps: number;
}

/** Mirrors RacePlayerDto. */
export interface RacePlayerDto {
  userId: string;
  displayName: string;
  carId: string;
  carName: string;
  /** "Waiting" | "Ready" | "Racing" | "Finished" */
  status: string;
  isHost: boolean;
}

/** Full room state. Mirrors RaceDetailDto — used by the room screen and every SignalR room broadcast. */
export interface RaceDetailDto {
  id: string;
  trackId: string;
  trackName: string;
  totalLaps: number;
  hostUserId: string;
  /** "Waiting" | "Starting" | "Running" | "Finished" */
  status: string;
  maxPlayers: number;
  players: RacePlayerDto[];
}

/** Body for POST /api/races. */
export interface CreateRaceRequest {
  trackId: string;
  carId: string;
  maxPlayers: number;
}

/** Body for POST /api/races/{id}/join. */
export interface JoinRaceRequest {
  carId: string;
}

/** Result of POST /api/races/{id}/leave. */
export interface LeaveRaceResult {
  roomClosed: boolean;
  raceDetail?: RaceDetailDto;
}

// ---- Real-time (SignalR) payloads — mirror the DTOs RaceHub broadcasts ----

/** "PlayerProgress" — fire-and-forget, not persisted, just moves opponents' cars. */
export interface PlayerProgressDto {
  userId: string;
  lap: number;
  checkpoint: number;
  progress: number; // 0-1 fraction of current lap
}

/** "PlayerLapCompleted" */
export interface PlayerLapDto {
  userId: string;
  lapNumber: number;
  lapTimeMs: number;
  bestLapTimeMs: number;
}

/** "PlayerFinished" */
export interface PlayerFinishedDto {
  userId: string;
  position: number;
  totalTimeMs: number;
  bestLapTimeMs?: number;
}

/** One row of the final standings. Mirrors RaceResultRowDto. */
export interface RaceResultRowDto {
  position: number;
  userId: string;
  displayName: string;
  totalTimeMs: number;
  bestLapTimeMs?: number;
  experienceEarned: number;
  coinsEarned: number;
}

/** "RaceFinished" */
export interface RaceFinishedDto {
  raceId: string;
  results: RaceResultRowDto[];
}

/** "RaceCountdown" */
export interface RaceCountdownDto {
  raceId: string;
  seconds: number;
}

/** "RaceBegin" */
export interface RaceBeginDto {
  raceId: string;
  serverStartUtc: string;
}

/** "RoomClosed" */
export interface RoomClosedDto {
  raceId: string;
}

/** "RaceError" — sent back to the caller only, when a hub call fails validation. */
export interface RaceErrorDto {
  error: string;
  code?: string;
}

// ---- Friends ----

/** Room a friend is currently in, if any — mirrors FriendCurrentRaceDto. */
export interface FriendCurrentRaceDto {
  raceId: string;
  trackName: string;
  currentPlayers: number;
  maxPlayers: number;
  /** "Waiting" | "Starting" | "Running" */
  status: string;
}

export interface FriendDto {
  userId: string;
  displayName: string;
  isOnline: boolean;
  currentRace?: FriendCurrentRaceDto;
}

export interface PendingFriendRequestDto {
  friendshipId: string;
  requesterId: string;
  requesterDisplayName: string;
  createdAtUtc: string;
}

export interface FriendOnlineDto {
  userId: string;
  displayName: string;
}

export interface FriendOfflineDto {
  userId: string;
}

// ---- Leaderboards ----

export interface LeaderboardEntryDto {
  userId: string;
  displayName: string;
  wins: number;
  totalRaces: number;
  bestTime?: string;
  /** Elo-style rating from RankingWorker — only set on the global scope. */
  ratingPoints?: number;
}

// ---- Achievements ----

/** Mirrors RaceHub.Application.DTOs.Achievements.AchievementDto. */
export interface AchievementDto {
  key: string;
  title: string;
  description: string;
  unlocked: boolean;
  unlockedAtUtc: string | null;
}

// ---- Personal bests ----

/** Mirrors RaceHub.Application.DTOs.Tracks.PersonalBestDto. */
export interface PersonalBestDto {
  trackId: string;
  trackName: string;
  bestTimeMs: number;
}

// ---- Users ----

export interface RecentRaceDto {
  raceId: string;
  trackName: string;
  finishingPosition: number;
  totalRaceTime: string; // TimeSpan serialized as "hh:mm:ss.fff"
  createdAtUtc: string;
}

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
  bestLapTime?: string;
  recentRaces: RecentRaceDto[];
  ratingPoints?: number;
}
