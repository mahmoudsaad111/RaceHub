import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';

import { FriendsService } from '../../core/services/friends.service';
import { RealtimeService } from '../../core/services/realtime.service';
import { FriendDto, PendingFriendRequestDto } from '../../core/models/api.models';

/** Tracks which friend/request a given online status or action applies to. */
interface FriendView extends FriendDto {
  online: boolean;
}

@Component({
  selector: 'rh-friends',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './friends.component.html',
  styleUrl: './friends.component.scss',
})
export class FriendsComponent implements OnInit, OnDestroy {
  private readonly friendsService = inject(FriendsService);
  private readonly realtime = inject(RealtimeService);

  friends = signal<FriendView[]>([]);
  pendingRequests = signal<PendingFriendRequestDto[]>([]);
  loading = signal(true);
  errorMessage = signal<string | null>(null);

  addEmail = '';
  sending = false;
  sendMessage: string | null = null;
  sendError: string | null = null;

  respondingId: string | null = null;

  private readonly onlineIds = new Set<string>();
  private subscription = new Subscription();

  ngOnInit(): void {
    this.loadAll();

    // Friend presence pushed from RaceHub.OnConnectedAsync/OnDisconnectedAsync
    // (see backend RaceHub.cs NotifyFriends) — keeps the online dot live
    // without polling.
    this.subscription.add(
      this.realtime.friendOnline$.subscribe((event) => {
        this.onlineIds.add(event.userId);
        this.applyOnlineStatus();
      }),
    );
    this.subscription.add(
      this.realtime.friendOffline$.subscribe((event) => {
        this.onlineIds.delete(event.userId);
        this.applyOnlineStatus();
      }),
    );

    void this.realtime.ensureConnected();
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
  }

  sendRequest(): void {
    const email = this.addEmail.trim();

    if (!email || this.sending) {
      return;
    }

    this.sending = true;
    this.sendMessage = null;
    this.sendError = null;

    this.friendsService.sendRequest(email).subscribe({
      next: () => {
        this.sending = false;
        this.sendMessage = `Friend request sent to ${email}.`;
        this.addEmail = '';
      },
      error: (err) => {
        this.sending = false;
        this.sendError = err?.message ?? 'Could not send that friend request.';
      },
    });
  }

  respond(request: PendingFriendRequestDto, accept: boolean): void {
    if (this.respondingId) {
      return;
    }

    this.respondingId = request.friendshipId;

    this.friendsService.respondToRequest(request.friendshipId, accept).subscribe({
      next: () => {
        this.respondingId = null;
        // Optimistically drop it from the pending list rather than
        // refetching — the request is resolved either way.
        this.pendingRequests.update((requests) =>
          requests.filter((r) => r.friendshipId !== request.friendshipId),
        );

        if (accept) {
          this.loadFriends();
        }
      },
      error: (err) => {
        this.respondingId = null;
        this.errorMessage.set(err?.message ?? 'Could not respond to that request.');
      },
    });
  }

  private loadAll(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.loadFriends();

    this.friendsService.getPendingRequests().subscribe({
      next: (requests) => {
        this.pendingRequests.set(requests);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.message ?? 'Could not load friend requests.');
      },
    });
  }

  private loadFriends(): void {
    this.friendsService.getFriends().subscribe({
      next: (friends) => {
        // Seed onlineIds from the server-authoritative snapshot (correct as
        // of this request), then keep it live via friendOnline$/
        // friendOffline$ from here on. This used to ignore f.isOnline
        // entirely and always read from onlineIds, which starts empty —
        // so every friend showed offline until a live FriendOnline event
        // happened to arrive for them sometime after this page was
        // already open (LobbyComponent's friend list had the exact same
        // bug, already fixed there).
        this.onlineIds.clear();
        friends.filter((f) => f.isOnline).forEach((f) => this.onlineIds.add(f.userId));

        this.friends.set(friends.map((f) => ({ ...f, online: this.onlineIds.has(f.userId) })));
      },
      error: (err) => {
        this.errorMessage.set(err?.message ?? 'Could not load friends.');
      },
    });
  }

  private applyOnlineStatus(): void {
    this.friends.update((friends) =>
      friends.map((f) => ({ ...f, online: this.onlineIds.has(f.userId) })),
    );
  }
}
