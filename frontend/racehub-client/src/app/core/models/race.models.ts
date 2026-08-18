/**
 * Shared view-model interfaces for the RaceHub UI.
 * These intentionally mirror the shapes the ASP.NET Core API
 * (see /api/races, /api/leaderboards in the README) is expected
 * to return once the backend is implemented.
 */

export interface OpenRoom {
  /** Real race id (GUID) — used for API calls and routing, never shown as-is. */
  id: string;
  /** Friendly display label, e.g. "ROOM #A1B2". */
  code: string;
  track: string;
  occupancy: string; // e.g. "3 / 8"
  icon: string;
}

export interface FriendStatus {
  name: string;
  status: string;
  online: boolean;
}

export interface RoomSlot {
  name: string;
  icon: string;
  tag: 'Host' | 'Ready' | 'Waiting' | null;
}

export interface ChatMessage {
  author: string;
  text: string;
}

export interface RaceResultRow {
  position: number;
  medalClass: 'medal-1' | 'medal-2' | 'medal-3' | 'medal-none';
  medalLabel: string;
  player: string;
  time: string;
  bestLap: string;
  reward: string;
}

export interface LeaderboardRow {
  rank: string;
  player: string;
  wins: string;
  bestTime: string;
  isMe?: boolean;
}

export interface Car {
  name: string;
  icon: string;
  color: string;
  equipped: boolean;
  price: string | null;
}

export interface CarStat {
  label: string;
  value: number; // percentage 0-100
}

export interface ShopItem {
  icon: string;
  label: string;
  price: string;
  badge: 'POPULAR' | 'BEST VALUE' | null;
}

export interface RecentRace {
  icon: string;
  track: string;
  position: string;
  time: string;
}
