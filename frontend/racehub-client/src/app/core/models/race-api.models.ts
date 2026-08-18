/**
 * Mirrors backend RaceHub.Application.DTOs.Races.* — the real room/race
 * shapes returned by RacesController and broadcast over RaceHub (SignalR).
 */

export type RaceStatusName = 'Waiting' | 'Starting' | 'Running' | 'Finished' | 'Cancelled';
export type PlayerStatusName = 'Waiting' | 'Ready' | 'Racing' | 'Finished' | 'Disconnected';

/** Row in the lobby's open-room list — GET /api/races. */
export interface OpenRaceDto {
  id: string;
  trackName: string;
  currentPlayers: number;
  maxPlayers: number;
  totalLaps: number;
}

export interface RacePlayerDto {
  userId: string;
  displayName: string;
  carId: string;
  carName: string;
  status: PlayerStatusName;
  isHost: boolean;
}

/** Full room state — GET /api/races/{id} and every REST + SignalR broadcast. */
export interface RaceDetailDto {
  id: string;
  trackId: string;
  trackName: string;
  totalLaps: number;
  hostUserId: string;
  status: RaceStatusName;
  maxPlayers: number;
  players: RacePlayerDto[];
}

// ---- In-race real-time payloads (SignalR broadcasts from RaceHub) ----

export interface PlayerProgressDto {
  raceId: string;
  userId: string;
  lap: number;
  checkpoint: number;
  progress: number; // 0-1, fraction of the current lap completed
}

export interface PlayerLapDto {
  raceId: string;
  userId: string;
  lapNumber: number;
  lapTimeMs: number;
  bestLapTimeMs: number;
}

export interface PlayerFinishedDto {
  raceId: string;
  userId: string;
  position: number;
  totalTimeMs: number;
  bestLapTimeMs: number | null;
}

export interface RaceResultRowDto {
  position: number;
  userId: string;
  displayName: string;
  totalTimeMs: number;
  bestLapTimeMs: number | null;
  experienceEarned: number;
  coinsEarned: number;
}

export interface RaceFinishedDto {
  raceId: string;
  results: RaceResultRowDto[];
}

export interface RaceCountdownDto {
  raceId: string;
  seconds: number;
}

export interface RaceBeginDto {
  raceId: string;
  serverStartUtc: string;
}

export interface RoomClosedDto {
  raceId: string;
}

export interface RoomDeletedDto {
  raceId: string;
}

export interface RaceErrorDto {
  error: string;
  code?: string;
}

export interface RaceChatMessageDto {
  senderId: string;
  content: string;
  sentAtUtc: string;
}

// ---- Friend invites (SignalR only, nothing persisted server-side) ----

/** "RaceInviteReceived" — pushed to the invitee when a friend invites them into their room. */
export interface RaceInviteReceivedDto {
  raceId: string;
  trackName: string;
  fromUserId: string;
  fromDisplayName: string;
  currentPlayers: number;
  maxPlayers: number;
}

/** "RaceInviteDeclined" — pushed back to the host who sent the invite. */
export interface RaceInviteDeclinedDto {
  raceId: string;
  byUserId: string;
  byDisplayName: string;
}

export interface RewardCreditedDto {
  userId: string;
  coinsAwarded: number;
  experienceAwarded: number;
  totalCoins: number;
  totalExperience: number;
  leveledUp: boolean;
  newLevel: number;
}

/**
 * "AchievementUnlocked" — pushed by AchievementNotificationRelayService when
 * AchievementsWorker grants a badge, so the toast pops live instead of on
 * the next profile visit.
 */
export interface AchievementUnlockedDto {
  userId: string;
  achievementKey: string;
  title: string;
  description: string;
}
