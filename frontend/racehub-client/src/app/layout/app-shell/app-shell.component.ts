import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Subscription } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { NotificationsService, NotificationDto } from '../../core/services/notifications.service';
import { ProfileService } from '../../core/services/profile.service';
import { RealtimeService } from '../../core/services/realtime.service';
import { RacesService } from '../../core/services/races.service';
import { SelectedCarService } from '../../core/services/selected-car.service';
import { ActiveRaceStore } from '../../core/services/active-race.store';
import { ProfileDto } from '../../core/models/profile.models';
import { RaceInviteReceivedDto } from '../../core/models/race-api.models';

@Component({
  selector: 'rh-app-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app-shell.component.html',
  styleUrl: './app-shell.component.scss',
})
export class AppShellComponent implements OnInit, OnDestroy {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notificationsService = inject(NotificationsService);
  private readonly profileService = inject(ProfileService);
  private readonly realtime = inject(RealtimeService);
  private readonly racesService = inject(RacesService);
  private readonly selectedCarService = inject(SelectedCarService);
  private readonly activeRaceStore = inject(ActiveRaceStore);

  coins = 0;
  gems = 320;
  notificationCount = 0;
  notifications: NotificationDto[] = [];
  showNotifications = false;
  levelUpToast: { message: string } | null = null;
  achievementToast: { title: string; description: string } | null = null;

  pendingInvites: RaceInviteReceivedDto[] = [];
  respondingToInvite = false;
  inviteError: string | null = null;

  readonly currentUser = this.authService.currentUser;

  private subscription = new Subscription();

  constructor() {
    this.loadNotifications();
    this.loadProfile();
  }

  ngOnInit(): void {
    void this.realtime.ensureConnected();

    this.subscription.add(
      this.realtime.raceInviteReceived$.subscribe((invite) => {
        this.pendingInvites = [
          ...this.pendingInvites.filter((i) => i.raceId !== invite.raceId || i.fromUserId !== invite.fromUserId),
          invite,
        ];
      }),
    );

    this.subscription.add(
      this.realtime.rewardCredited$.subscribe((evt) => {
        this.coins = evt.totalCoins;

        if (evt.leveledUp) {
          this.levelUpToast = { message: `Level up! You're now Level ${evt.newLevel}.` };
          setTimeout(() => { this.levelUpToast = null; }, 4000);
        }
      }),
    );

    // Badge unlocks arrive a moment after the race finishes — the
    // AchievementsWorker consumes race.finished off the bus, so this toast
    // landing "late" is the eventual-consistency window made visible.
    this.subscription.add(
      this.realtime.achievementUnlocked$.subscribe((evt) => {
        this.achievementToast = { title: evt.title, description: evt.description };
        setTimeout(() => { this.achievementToast = null; }, 5000);
      }),
    );
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
  }

  logout(): void {
    this.authService.logout().subscribe(() => {
      this.router.navigateByUrl('/auth');
    });
  }

  toggleNotifications(): void {
    this.showNotifications = !this.showNotifications;
    if (this.showNotifications) {
      this.notificationsService.markAllAsRead().subscribe();
      this.notificationCount = 0;
    }
  }

  acceptInvite(invite: RaceInviteReceivedDto): void {
    if (this.respondingToInvite) return;
    this.respondingToInvite = true;
    this.inviteError = null;

    this.racesService.acceptInvite(invite.raceId).subscribe({
      next: (race) => {
        const currentUserId = this.authService.currentUser()?.userId;

        if (!currentUserId || !race.players.some((player) => player.userId === currentUserId)) {
          this.respondingToInvite = false;
          this.inviteError = 'The room did not add you. Please accept the invite again.';
          return;
        }

        this.activeRaceStore.setCurrentRace(race);
        this.dismissInvite(invite);
        this.respondingToInvite = false;

        this.router.navigate(['/room', race.id]);
        void this.realtime.joinRaceGroup(race.id).catch(() => {
          /* best-effort - RoomComponent retries this on its own */
        });

        const selectedCarId = this.selectedCarService.getSelectedCarId();
        if (selectedCarId) {
          this.racesService.changeCar(race.id, selectedCarId).subscribe({
            next: (updated) => this.activeRaceStore.setCurrentRace(updated),
            error: () => {
              /* best-effort — worst case they keep the default car for this race */
            },
          });
        }
      },
      error: (err) => {
        this.respondingToInvite = false;

        if (err?.errorCode === 'already_in_race') {
          this.activeRaceStore.setCurrentRace(null);
          this.router.navigate(['/room', invite.raceId]);
          return;
        }

        this.inviteError = err?.message ?? 'Could not accept this invite. Please try again.';
      },
    });
  }

  declineInvite(invite: RaceInviteReceivedDto): void {
    void this.realtime.declineRaceInvite(invite.raceId, invite.fromUserId);
    this.dismissInvite(invite);
  }

  private dismissInvite(invite: RaceInviteReceivedDto): void {
    this.pendingInvites = this.pendingInvites.filter(
      (i) => !(i.raceId === invite.raceId && i.fromUserId === invite.fromUserId),
    );
  }

  private loadNotifications(): void {
    this.notificationsService.getUnreadCount().subscribe((count) => {
      this.notificationCount = count;
    });

    this.notificationsService.getAll().subscribe((items) => {
      this.notifications = items;
    });
  }

  private loadProfile(): void {
    this.profileService.getMyProfile().subscribe((profile) => {
      if (profile) {
        this.coins = profile.coins;
      }
    });
  }
}