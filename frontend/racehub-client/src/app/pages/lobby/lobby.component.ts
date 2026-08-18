import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';

import { CarsService } from '../../core/services/cars.service';
import { SelectedCarService } from '../../core/services/selected-car.service';
import { FriendsService } from '../../core/services/friends.service';
import { RacesService } from '../../core/services/races.service';
import { TracksService } from '../../core/services/tracks.service';
import { RealtimeService } from '../../core/services/realtime.service';
import { ActiveRaceStore } from '../../core/services/active-race.store';
import { ProfileService } from '../../core/services/profile.service';
import { getCarSwatch } from '../../core/config/car-visuals';
import { FriendCurrentRaceDto, FriendDto, FriendOnlineDto, FriendOfflineDto, PendingFriendRequestDto, PersonalBestDto, TrackDto, CarDto } from '../../core/models/api.models';

interface FriendView {
  userId: string;
  displayName: string;
  online: boolean;
  status: string;
  currentRace?: FriendCurrentRaceDto;
}

/** Structured so the template can render an occupancy meter, not "3 / 8" text. */
interface RoomView {
  id: string;
  code: string;
  track: string;
  players: number;
  maxPlayers: number;
  icon: string;
}

const TRACK_ICONS = ['🏁', '🏜️', '🏙️', '🌲'];

@Component({
  selector: 'rh-lobby',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './lobby.component.html',
  styleUrl: './lobby.component.scss',
})
export class LobbyComponent implements OnInit, OnDestroy {
  private readonly racesService = inject(RacesService);
  private readonly carsService = inject(CarsService);
  private readonly selectedCarService = inject(SelectedCarService);
  private readonly tracksService = inject(TracksService);
  private readonly friendsService = inject(FriendsService);
  private readonly realtime = inject(RealtimeService);
  private readonly activeRaceStore = inject(ActiveRaceStore);
  private readonly profileService = inject(ProfileService);
  private readonly router = inject(Router);

  friends: FriendView[] = [];
  rooms: RoomView[] = [];
  loading = true;
  errorMessage: string | null = null;
  joiningRoomId: string | null = null;

  friendRequests: PendingFriendRequestDto[] = [];
  showFriendRequests = false;
  friendEmail = '';
  sendingRequest = false;
  requestError: string | null = null;

  showCreateRoom = false;
  tracks: TrackDto[] = [];
  cars: CarDto[] = [];
  createTrackId = '';
  createCarId = '';
  createMaxPlayers = 8;
  creatingRoom = false;
  createError: string | null = null;
  personalBests = new Map<string, number>();

  private subscription = new Subscription();

  ngOnInit(): void {
    this.loadOpenRaces();
    this.loadFriends();
    this.loadFriendRequests();

    this.subscription.add(
      this.realtime.roomClosed$.subscribe(() => this.loadOpenRaces()),
    );

    this.subscription.add(
      this.realtime.friendOnline$.subscribe((dto: FriendOnlineDto) => {
        this.updateFriendPresence(dto.userId, true, dto.displayName);
      }),
    );

    this.subscription.add(
      this.realtime.friendOffline$.subscribe((dto: FriendOfflineDto) => {
        this.updateFriendPresence(dto.userId, false);
      }),
    );
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
  }

  refresh(): void {
    this.loadOpenRaces();
  }

  get friendsOnlineCount(): number {
    return this.friends.filter((f) => f.online).length;
  }

  joinRoom(room: { id: string }): void {
    if (this.joiningRoomId) return;

    this.joiningRoomId = room.id;
    this.errorMessage = null;

    this.carsService.getAll().subscribe({
      next: (allCars) => {
        // Only cars from your garage can be raced — free starters
        // (price 0) plus anything purchased.
        const cars = allCars.filter((c) => c.owned || c.price <= 0);
        const carId = this.selectedCarService.resolveCarId(cars.map((c) => c.id));

        if (!carId) {
          this.joiningRoomId = null;
          this.errorMessage = 'No cars available to race with.';
          return;
        }

        this.racesService.join(room.id, carId).subscribe({
          next: (race) => {
            this.activeRaceStore.setCurrentRace(race);
            this.joiningRoomId = null;

            // Navigate as soon as the REST join succeeds — don't gate it on
            // the SignalR group-join below. RoomComponent re-joins the group
            // itself on load anyway, so this is a best-effort head start,
            // not a prerequisite; awaiting it here previously meant a single
            // dropped/reconnecting socket made the whole "Join" button hang
            // on "JOINING..." forever with the REST join having silently
            // already succeeded server-side.
            this.router.navigate(['/room', race.id]);
            void this.realtime.joinRaceGroup(race.id).catch(() => {
              /* best-effort — RoomComponent retries this on its own */
            });
          },
          error: (err) => {
            this.joiningRoomId = null;

            if (err?.errorCode === 'already_in_race') {
              // Not a real failure — they're already a player in this race
              // (e.g. clicking Join on a room they host/already joined).
              // Just take them there instead of showing an error.
              this.activeRaceStore.setCurrentRace(null);
              this.router.navigate(['/room', room.id]);
              return;
            }

            this.errorMessage = err?.message ?? 'Could not join that room.';
          },
        });
      },
      error: () => {
        this.joiningRoomId = null;
        this.errorMessage = 'Could not load cars.';
      },
    });
  }

  /** Same flow as joinRoom, triggered from a friend's "Join" button instead of the open-rooms list. */
  joinFriendRace(friend: FriendView): void {
    if (!friend.currentRace) return;
    this.joinRoom({ id: friend.currentRace.raceId });
  }

  openCreateRoom(): void {
    this.showCreateRoom = true;
    this.createError = null;

    if (this.tracks.length === 0) {
      this.tracksService.getAll().subscribe({
        next: (tracks) => {
          this.tracks = tracks;
          this.createTrackId ||= tracks[0]?.id ?? '';
        },
        error: () => {
          this.createError = 'Could not load tracks.';
        },
      });
    }

    // "Your PB: 1:23.45" hints on the track picker — best time per track,
    // read from StatisticsWorker's RaceHistoryEntry read model.
    if (this.personalBests.size === 0) {
      this.profileService.getPersonalBests().subscribe({
        next: (bests: PersonalBestDto[]) => {
          this.personalBests = new Map(bests.map((b) => [b.trackId, b.bestTimeMs]));
        },
        error: () => {
          /* PB hints are a nicety — silently skip them if unavailable */
        },
      });
    }

    if (this.cars.length === 0) {
      this.carsService.getAll().subscribe({
        next: (allCars) => {
          // The room's car picker lists your garage only — free starters
          // (price 0) plus purchased cars.
          const cars = allCars.filter((c) => c.owned || c.price <= 0);
          this.cars = cars;
          this.createCarId ||= this.selectedCarService.resolveCarId(cars.map((c) => c.id)) ?? '';
        },
        error: () => {
          this.createError = 'Could not load cars.';
        },
      });
    }
  }

  /** Formatted PB for the currently selected track, or null if never raced there. */
  selectedTrackPb(): string | null {
    const bestMs = this.createTrackId ? this.personalBests.get(this.createTrackId) : undefined;
    if (bestMs === undefined) {
      return null;
    }

    const totalSeconds = bestMs / 1000;
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = Math.floor(totalSeconds % 60);
    const centiseconds = Math.floor((totalSeconds % 1) * 100);

    return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}.${String(centiseconds).padStart(2, '0')}`;
  }

  // ---- View helpers for the visual create-room pickers ----

  difficultyLabel(difficulty: number): string {
    if (difficulty <= 1) return 'EASY';
    if (difficulty === 2) return 'MEDIUM';
    return 'HARD';
  }

  /** Distinct gradient per difficulty so track tiles aren't all the same blue. */
  trackGradient(difficulty: number): string {
    if (difficulty <= 1) return 'linear-gradient(150deg, #1d3a5f, #101a2e)';
    if (difficulty === 2) return 'linear-gradient(150deg, #4a3520, #1c1410)';
    return 'linear-gradient(150deg, #47203f, #1b0f1a)';
  }

  carIcon(carName: string): string {
    return getCarSwatch(carName).icon;
  }

  carColor(carName: string): string {
    return getCarSwatch(carName).color;
  }

  incPlayers(): void {
    this.createMaxPlayers = Math.min(8, this.createMaxPlayers + 1);
  }

  decPlayers(): void {
    this.createMaxPlayers = Math.max(2, this.createMaxPlayers - 1);
  }

  /** Initials for the round friend avatars ("Max P." → "MP"). */
  initials(name: string): string {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]!.toUpperCase())
      .join('');
  }

  closeCreateRoom(): void {
    if (this.creatingRoom) return;
    this.showCreateRoom = false;
  }

  createRoom(): void {
    if (this.creatingRoom || !this.createTrackId || !this.createCarId) return;

    this.creatingRoom = true;
    this.createError = null;

    this.racesService.create(this.createTrackId, this.createCarId, this.createMaxPlayers).subscribe({
      next: (race) => {
        this.activeRaceStore.setCurrentRace(race);
        this.creatingRoom = false;
        this.showCreateRoom = false;
        this.router.navigate(['/room', race.id]);
        void this.realtime.joinRaceGroup(race.id).catch(() => {
          /* best-effort — RoomComponent retries this on its own */
        });
      },
      error: (err) => {
        this.creatingRoom = false;
        this.createError = err?.message ?? 'Could not create room.';
      },
    });
  }

  sendFriendRequest(): void {
    const email = this.friendEmail.trim();
    if (!email || this.sendingRequest) return;

    this.sendingRequest = true;
    this.requestError = null;

    this.friendsService.sendRequest(email).subscribe({
      next: () => {
        this.friendEmail = '';
        this.sendingRequest = false;
        this.loadFriendRequests();
      },
      error: (err) => {
        this.sendingRequest = false;
        this.requestError = err?.message ?? 'Could not send friend request.';
      },
    });
  }

  respondToRequest(friendshipId: string, accept: boolean): void {
    this.friendsService.respondToRequest(friendshipId, accept).subscribe({
      next: () => {
        this.friendRequests = this.friendRequests.filter((r) => r.friendshipId !== friendshipId);

        // Accepting adds a new row to "Friends Online" that the pending-
        // requests list has no way to reflect on its own — reload it so
        // the friend actually shows up instead of the request just
        // vanishing with no visible result.
        if (accept) {
          this.loadFriends();
        }
      },
      error: (err) => {
        this.errorMessage = err?.message ?? 'Could not respond to request.';
      },
    });
  }

  private loadOpenRaces(): void {
    this.loading = true;

    this.racesService.getOpenRaces().subscribe({
      next: (races) => {
        this.rooms = races.map((race, index) => ({
          id: race.id,
          code: `ROOM #${race.id.slice(0, 4).toUpperCase()}`,
          track: race.trackName,
          players: race.currentPlayers,
          maxPlayers: race.maxPlayers,
          icon: TRACK_ICONS[index % TRACK_ICONS.length],
        }));
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err?.message ?? 'Could not load open rooms.';
      },
    });
  }

  private loadFriends(): void {
    this.friendsService.getFriends().subscribe({
      next: (friends: FriendDto[]) => {
        this.friends = friends.map((f) => ({
          userId: f.userId,
          displayName: f.displayName,
          // Was hardcoded to false before, which is why online friends showed
          // as offline until a live FriendOnline event happened to arrive
          // after this initial load — the actual snapshot from the API was
          // simply being discarded.
          online: f.isOnline,
          status: f.isOnline ? 'Online' : 'Offline',
          currentRace: f.currentRace,
        }));
      },
      error: () => {
        this.errorMessage = 'Could not load friends.';
      },
    });
  }

  private loadFriendRequests(): void {
    this.friendsService.getPendingRequests().subscribe({
      next: (requests) => {
        this.friendRequests = requests;
      },
      error: () => {
        // Silently fail - not critical
      },
    });
  }

  private updateFriendPresence(userId: string, online: boolean, displayName?: string): void {
    const existing = this.friends.find((f) => f.userId === userId);
    if (existing) {
      existing.online = online;
      existing.status = online ? 'Online' : 'Offline';
      if (displayName) {
        existing.displayName = displayName;
      }
    }
  }
}
