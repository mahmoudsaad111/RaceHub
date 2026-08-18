import { Injectable } from '@angular/core';

import {
  Car, CarStat, ChatMessage, FriendStatus, LeaderboardRow,
  OpenRoom, RaceResultRow, RecentRace, RoomSlot, ShopItem,
} from '../models/race.models';

/**
 * Placeholder data for the UI while the real API isn't built yet.
 * Every method here is a stand-in for a future HttpClient call, e.g.
 * `getOpenRooms()` will eventually call `GET /api/races`.
 * Swap the bodies for real HTTP calls once RaceHub.API exists —
 * the components only depend on this service, not on where the
 * data comes from.
 */
@Injectable({ providedIn: 'root' })
export class MockDataService {
  getOpenRooms(): OpenRoom[] {
    return [
      { id: '1842', code: 'ROOM #1842', track: 'Classic Track', occupancy: '3 / 8', icon: '🏁' },
      { id: '1843', code: 'ROOM #1843', track: 'Desert Track', occupancy: '2 / 6', icon: '🏜️' },
      { id: '1844', code: 'ROOM #1844', track: 'City Track', occupancy: '5 / 8', icon: '🏙️' },
      { id: '1845', code: 'ROOM #1845', track: 'Forest Track', occupancy: '1 / 4', icon: '🌲' },
    ];
  }

  getFriends(): FriendStatus[] {
    return [
      { name: 'Ahmed', status: 'In Lobby', online: true },
      { name: 'Omar', status: 'In Room #1843', online: true },
      { name: 'Youssef', status: 'Online', online: true },
      { name: 'Karim', status: 'Online', online: true },
    ];
  }

  getRoomSlots(): RoomSlot[] {
    return [
      { name: 'Mahmoud', icon: '🏎️', tag: 'Host' },
      { name: 'Ahmed', icon: '🚙', tag: 'Ready' },
      { name: 'Omar', icon: '🚕', tag: 'Ready' },
      { name: 'Waiting…', icon: '＋', tag: null },
      { name: 'Waiting…', icon: '＋', tag: null },
      { name: 'Waiting…', icon: '＋', tag: null },
    ];
  }

  getRoomChat(): ChatMessage[] {
    return [
      { author: 'Mahmoud', text: 'Hi guys! 👋' },
      { author: 'Ahmed', text: 'Ready when you are' },
      { author: 'Omar', text: "Let's go! 🏎️💨" },
    ];
  }

  getRaceResults(): RaceResultRow[] {
    return [
      { position: 1, medalClass: 'medal-1', medalLabel: '🥇', player: 'Mahmoud', time: '02:15.42', bestLap: '00:20.15', reward: '+150' },
      { position: 2, medalClass: 'medal-2', medalLabel: '🥈', player: 'Ahmed', time: '02:17.81', bestLap: '00:20.63', reward: '+100' },
      { position: 3, medalClass: 'medal-3', medalLabel: '🥉', player: 'Omar', time: '02:20.11', bestLap: '00:20.95', reward: '+75' },
      { position: 4, medalClass: 'medal-none', medalLabel: '4', player: 'Bot Racer', time: '02:25.44', bestLap: '00:21.30', reward: '+50' },
      { position: 5, medalClass: 'medal-none', medalLabel: '5', player: 'Speedy Bot', time: '02:28.77', bestLap: '00:21.85', reward: '+25' },
    ];
  }

  getLeaderboard(): LeaderboardRow[] {
    return [
      { rank: '🥇', player: 'Mahmoud', wins: '45', bestTime: '00:20.15' },
      { rank: '🥈', player: 'Ahmed', wins: '38', bestTime: '00:20.63' },
      { rank: '🥉', player: 'Omar', wins: '32', bestTime: '00:20.95' },
      { rank: '4', player: 'Youssef', wins: '29', bestTime: '00:21.10' },
      { rank: '5', player: 'Karim', wins: '27', bestTime: '00:21.22' },
      { rank: '12', player: 'You', wins: '12', bestTime: '00:22.45', isMe: true },
    ];
  }

  getGarageCars(): Car[] {
    return [
      { name: 'Speedster', icon: '🏎️', color: '#e0453f', equipped: true, price: null },
      { name: 'Lightning', icon: '⚡🚗', color: '#2f6fe0', equipped: false, price: '15,000' },
      { name: 'Phantom', icon: '🚙', color: '#e0b23f', equipped: false, price: '20,000' },
      { name: 'Shadow', icon: '🚓', color: '#2fa34e', equipped: false, price: '25,000' },
      { name: 'Thunder', icon: '🚕', color: '#8b4fe0', equipped: false, price: '35,000' },
    ];
  }

  getCarStats(): CarStat[] {
    return [
      { label: 'Speed', value: 80 },
      { label: 'Acceleration', value: 65 },
      { label: 'Handling', value: 72 },
      { label: 'Nitro', value: 55 },
    ];
  }

  getShopItems(): ShopItem[] {
    return [
      { icon: '🪙', label: '2,500 Coins', price: '$1.99', badge: null },
      { icon: '🪙', label: '7,000 Coins', price: '$4.99', badge: 'POPULAR' },
      { icon: '🪙', label: '16,000 Coins', price: '$9.99', badge: 'BEST VALUE' },
      { icon: '💎', label: '60 Gems', price: '$1.99', badge: null },
      { icon: '💎', label: '180 Gems', price: '$4.99', badge: null },
      { icon: '💎', label: '400 Gems', price: '$9.99', badge: null },
    ];
  }

  getRecentRaces(): RecentRace[] {
    return [
      { icon: '🔴', track: 'Classic Track', position: '1st', time: '02:15.42' },
      { icon: '🟠', track: 'Desert Track', position: '2nd', time: '02:18.33' },
      { icon: '🟢', track: 'City Track', position: '1st', time: '02:10.55' },
    ];
  }
}
