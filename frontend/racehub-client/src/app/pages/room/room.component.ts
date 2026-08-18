import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';

import { RacesService } from '../../core/services/races.service';
import { RealtimeService } from '../../core/services/realtime.service';
import { ActiveRaceStore } from '../../core/services/active-race.store';
import { AuthService } from '../../core/services/auth.service';
import { FriendsService } from '../../core/services/friends.service';
import { CarsService } from '../../core/services/cars.service';
import { SelectedCarService } from '../../core/services/selected-car.service';
import { getCarSwatch } from '../../core/config/car-visuals';
import { RaceChatMessageDto, RaceDetailDto } from '../../core/models/race-api.models';
import { FriendDto } from '../../core/models/api.models';
import { CarDto } from '../../core/models/car.models';

interface ChatMsgView {
  senderId: string;
  displayName: string;
  content: string;
  sentAtUtc: string;
}

/** One seat at the grid — filled or waiting for a driver. */
interface SlotView {
  name: string;
  carName: string | null;
  isHost: boolean;
  isReady: boolean;
  isSelf: boolean;
  empty: boolean;
}

@Component({
  selector: 'rh-room',
  standalone: true,
  imports: [RouterLink, FormsModule],
  templateUrl: './room.component.html',
  styleUrl: './room.component.scss',
})
export class RoomComponent implements OnInit, OnDestroy {
  private readonly racesService = inject(RacesService);
  private readonly realtime = inject(RealtimeService);
  private readonly activeRaceStore = inject(ActiveRaceStore);
  private readonly authService = inject(AuthService);
  private readonly friendsService = inject(FriendsService);
  private readonly carsService = inject(CarsService);
  private readonly selectedCarService = inject(SelectedCarService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  raceId = this.route.snapshot.paramMap.get('id') ?? '';
  roomId = this.raceId;
  race: RaceDetailDto | null = null;
  loading = true;
  errorMessage: string | null = null;
  leaving = false;
  markingReady = false;
  starting = false;
  deleting = false;
  joining = false;
  changingCar = false;

  chatMessages: ChatMsgView[] = [];
  chatInput = '';
  currentUserId = '';

  showInvitePanel = false;
  friends: FriendDto[] = [];
  invitedUserIds = new Set<string>();
  inviteError: string | null = null;

  cars: CarDto[] = [];
  selectedCarId = '';

  private subscription = new Subscription();
  private roomPollHandle: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    this.currentUserId = this.authService.currentUser()?.userId ?? '';
    this.loadRace();

    this.subscription.add(
      this.realtime.playerJoined$.subscribe((race) => {
        if (race.id === this.raceId) {
          this.refreshRoomState();
        }
      }),
    );
    this.subscription.add(
      this.realtime.playerLeft$.subscribe((race) => {
        if (race.id === this.raceId) {
          this.refreshRoomState();
        }
      }),
    );
    this.subscription.add(
      this.realtime.playerReady$.subscribe((race) => {
        if (race.id === this.raceId) {
          this.refreshRoomState();
        }
      }),
    );
    this.subscription.add(
      this.realtime.roomClosed$.subscribe((event) => {
        if (event.raceId === this.raceId) {
          this.router.navigateByUrl('/lobby');
        }
      }),
    );
    this.subscription.add(
      this.realtime.roomDeleted$.subscribe((event) => {
        if (event.raceId === this.raceId) {
          this.router.navigateByUrl('/lobby');
        }
      }),
    );
    this.subscription.add(
      this.realtime.raceStarted$.subscribe((race) => {
        if (race.id === this.raceId) {
          this.refreshRoomState((updatedRace) => {
            this.activeRaceStore.setCurrentRace(updatedRace);
            this.router.navigateByUrl('/race');
          });
        }
      }),
    );
    this.subscription.add(
      this.realtime.raceChatMessage$.subscribe((dto) => {
        if (dto.senderId !== this.currentUserId) {
          this.chatMessages.push({
            senderId: dto.senderId,
            displayName: this.getDisplayName(dto.senderId),
            content: dto.content,
            sentAtUtc: dto.sentAtUtc,
          });
        }
      }),
    );
    this.subscription.add(
      this.realtime.raceInviteDeclined$.subscribe((dto) => {
        if (dto.raceId === this.raceId) {
          this.invitedUserIds.delete(dto.byUserId);
          this.inviteError = `${dto.byDisplayName} declined your invite.`;
        }
      }),
    );
    this.subscription.add(
      this.realtime.raceError$.subscribe((err) => {
        this.inviteError = err.error;
      }),
    );

    this.roomPollHandle = setInterval(() => {
      if (!this.raceId || this.loading || this.leaving || this.deleting || this.starting) {
        return;
      }

      this.refreshRoomState();
    }, 3000);
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
    if (this.roomPollHandle) {
      clearInterval(this.roomPollHandle);
      this.roomPollHandle = null;
    }
    void this.realtime.leaveRaceGroup(this.raceId);
  }

  get slots(): SlotView[] {
    if (!this.race) {
      return [];
    }

    const filled = this.race.players.map((player) => ({
      name: player.displayName,
      carName: player.carName,
      isHost: player.isHost,
      isReady: player.status === 'Ready',
      isSelf: player.userId === this.currentUserId,
      empty: false,
    }));

    const emptyCount = Math.max(0, this.race.maxPlayers - filled.length);
    const empty = Array.from({ length: emptyCount }, () => ({
      name: 'Open seat',
      carName: null,
      isHost: false,
      isReady: false,
      isSelf: false,
      empty: true,
    }));

    return [...filled, ...empty];
  }

  get readyCount(): number {
    return this.race?.players.filter((p) => p.status === 'Ready').length ?? 0;
  }

  get playerCount(): number {
    return this.race?.players.length ?? 0;
  }

  /** Short display code for the room header — "3F2A" beats a full guid. */
  get roomCode(): string {
    return (this.roomId ?? '').slice(0, 4).toUpperCase();
  }

  carIcon(carName: string | null): string {
    return carName ? getCarSwatch(carName).icon : '＋';
  }

  carSwatchStyle(carName: string | null): string {
    const color = carName ? getCarSwatch(carName).color : '#1a2436';
    return `linear-gradient(150deg, ${color}, #10131c)`;
  }

  initials(name: string): string {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]!.toUpperCase())
      .join('');
  }

  /** Click a car chip in the strip: select it and apply immediately. */
  pickCar(carId: string): void {
    if (this.changingCar || this.isSelfReady || carId === this.currentPlayer?.carId) return;
    this.selectedCarId = carId;
    this.changeCar();
  }

  get filledCount() {
    return this.race?.players.length ?? 0;
  }

  get isHost() {
    return !!this.race?.hostUserId && this.race.hostUserId === this.currentUserId;
  }

  get allReady() {
    return !!this.race && this.race.players.length > 0 && this.race.players.every((p) => p.status === 'Ready');
  }

  get isSelfReady() {
    return !!this.race?.players.find((p) => p.userId === this.currentUserId && p.status === 'Ready');
  }

  get isPlayer() {
    return !!this.race?.players.some((p) => p.userId === this.currentUserId);
  }

  get currentPlayer() {
    return this.race?.players.find((player) => player.userId === this.currentUserId) ?? null;
  }

  tagClass(tag: string | null): string {
    if (tag === 'Host') return 'tag-host';
    if (tag === 'Ready') return 'tag-ready';
    return 'tag-wait';
  }

  markReady(): void {
    if (this.markingReady || !this.isPlayer) return;

    this.markingReady = true;

    this.racesService.ready(this.raceId).subscribe({
      next: (race) => {
        this.markingReady = false;
        this.applyIfCurrentRace(race);
      },
      error: (err) => {
        this.markingReady = false;
        this.errorMessage = err?.message ?? 'Could not update readiness.';
      },
    });
  }

  changeCar(): void {
    if (this.changingCar || !this.isPlayer || this.isSelfReady || !this.selectedCarId) return;

    this.changingCar = true;
    this.errorMessage = null;

    this.racesService.changeCar(this.raceId, this.selectedCarId).subscribe({
      next: (race) => {
        this.changingCar = false;
        this.applyIfCurrentRace(race);
      },
      error: (err) => {
        this.changingCar = false;
        this.errorMessage = err?.message ?? 'Could not change your car.';
      },
    });
  }

  startRace(): void {
    if (this.starting || !this.isHost) return;

    this.starting = true;

    this.racesService.start(this.raceId).subscribe({
      next: (race) => {
        this.applyIfCurrentRace(race);
      },
      error: (err) => {
        this.starting = false;
        this.errorMessage = err?.message ?? 'Could not start the race.';
      },
      complete: () => {
        this.starting = false;
      },
    });
  }

  leaveRoom(): void {
    if (this.leaving) return;

    this.leaving = true;

    this.racesService.leave(this.raceId).subscribe({
      next: () => {
        this.leaving = false;
        this.router.navigateByUrl('/lobby');
      },
      error: (err) => {
        this.leaving = false;
        this.errorMessage = err?.message ?? 'Could not leave the room.';
      },
    });
  }

  joinRace(): void {
    if (this.joining) return;

    this.joining = true;
    this.errorMessage = null;

    this.carsService.getAll().subscribe({
      next: (allCars) => {
        const garageCars = allCars.filter((c) => c.owned || c.price <= 0);
        const carId = this.selectedCarService.resolveCarId(garageCars.map((c) => c.id));

        if (!carId) {
          this.joining = false;
          this.errorMessage = 'No cars available.';
          return;
        }

        this.racesService.join(this.raceId, carId).subscribe({
          next: (race) => {
            this.joining = false;
            this.applyIfCurrentRace(race);
          },
          error: (err) => {
            this.joining = false;
            this.errorMessage = err?.message ?? 'Could not join this race.';
          },
        });
      },
      error: () => {
        this.joining = false;
        this.errorMessage = 'Could not load cars.';
      },
    });
  }

  deleteRoom(): void {
    if (this.deleting || !this.isHost) return;

    this.deleting = true;

    this.racesService.delete(this.raceId).subscribe({
      next: () => {
        this.deleting = false;
        this.router.navigateByUrl('/lobby');
      },
      error: (err) => {
        this.deleting = false;
        this.errorMessage = err?.message ?? 'Could not delete the room.';
      },
    });
  }

  sendChat(): void {
    const text = this.chatInput.trim();
    if (!text) return;

    const temp: ChatMsgView = {
      senderId: this.currentUserId,
      displayName: this.authService.currentUser()?.displayName ?? 'Me',
      content: text,
      sentAtUtc: new Date().toISOString(),
    };

    this.chatMessages.push(temp);
    this.chatInput = '';

    void this.realtime.sendRaceMessage(this.raceId, text);
  }

  get myCarName(): string {
    return this.race?.players.find((p) => p.userId === this.currentUserId)?.carName ?? '';
  }

  /** Friends not already sitting in this room — no point inviting someone who's already here. */
  get inviteFriends(): FriendDto[] {
    const playerIds = new Set(this.race?.players.map((p) => p.userId) ?? []);
    return this.friends.filter((f) => !playerIds.has(f.userId));
  }

  toggleInvitePanel(): void {
    this.showInvitePanel = !this.showInvitePanel;
    this.inviteError = null;

    if (this.showInvitePanel && this.friends.length === 0) {
      this.friendsService.getFriends().subscribe({
        next: (friends) => (this.friends = friends),
        error: () => (this.inviteError = 'Could not load friends.'),
      });
    }
  }

  inviteFriend(friendUserId: string): void {
    this.inviteError = null;

    this.realtime
      .inviteFriendToRace(this.raceId, friendUserId)
      .then(() => this.invitedUserIds.add(friendUserId))
      .catch(() => (this.inviteError = 'Could not send the invite.'));
  }

  private loadRace(): void {
    const storedRace = this.activeRaceStore.currentRace();

    if (storedRace && storedRace.id === this.raceId && storedRace.players.some((p) => p.userId === this.currentUserId)) {
      this.applyIfCurrentRace(storedRace);
      this.loading = false;
    }

    this.carsService.getAll().subscribe({
      next: (cars) => (this.cars = cars.filter((car) => car.isActive && (car.owned || car.price <= 0))),
      error: () => (this.errorMessage = 'Could not load available cars.'),
    });

    this.loading = true;
    this.refreshRoomState();

    void this.realtime
      .ensureConnected()
      .then(() => this.realtime.joinRaceGroup(this.raceId))
      .catch(() => {
        this.errorMessage =
          'Live updates are unavailable right now — refresh to try reconnecting.';
      });
  }

  private applyIfCurrentRace(race: RaceDetailDto): void {
    if (race.id !== this.raceId) return;

    this.race = race;
    const currentPlayer = race.players.find((player) => player.userId === this.currentUserId);
    if (currentPlayer && !this.changingCar) {
      this.selectedCarId = currentPlayer.carId;
    }
    this.activeRaceStore.setCurrentRace(race);
  }

  private refreshRoomState(onSuccess?: (race: RaceDetailDto) => void): void {
    this.racesService.getById(this.raceId).subscribe({
      next: (race) => {
        this.applyIfCurrentRace(race);
        this.loading = false;
        onSuccess?.(race);

        if (race.status !== 'Waiting' && race.players.some((p) => p.userId === this.currentUserId)) {
          this.router.navigateByUrl('/race');
        }
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err?.message ?? 'Could not load this room.';
      },
    });
  }

  private getDisplayName(userId: string): string {
    const player = this.race?.players.find((p) => p.userId === userId);
    return player?.displayName ?? 'Racer';
  }
}
