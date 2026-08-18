import { Injectable, inject } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject, firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import {
  AchievementUnlockedDto,
  PlayerFinishedDto,
  PlayerLapDto,
  PlayerProgressDto,
  RaceBeginDto,
  RaceChatMessageDto,
  RaceCountdownDto,
  RaceDetailDto,
  RaceErrorDto,
  RaceFinishedDto,
  RaceInviteDeclinedDto,
  RaceInviteReceivedDto,
  RewardCreditedDto,
  RoomClosedDto,
  RoomDeletedDto,
} from '../models/race-api.models';

/**
 * Thin wrapper around a single SignalR HubConnection to /hubs/race.
 *
 * One shared connection for the whole app (root-provided, not
 * per-component) since a user only ever needs one socket regardless of how
 * many screens are open — the lobby wants friend-presence events, the room
 * screen wants room-lifecycle events, and the race screen wants in-race
 * telemetry, all over the same connection.
 *
 * Server events are exposed as RxJS Subjects rather than a big switch
 * statement, so each component only subscribes to the handful of events it
 * actually cares about.
 */
@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private readonly authService = inject(AuthService);

  private connection: signalR.HubConnection | null = null;
  private startPromise: Promise<void> | null = null;

  // The room group the caller most recently joined (if any) — re-applied
  // in onreconnected() below, since a SignalR reconnect gets a brand new
  // ConnectionId and silently drops all previous Groups.AddToGroupAsync
  // membership server-side. Without this, a client that reconnects (a
  // backend restart during dev, a brief wifi drop, anything that trips
  // withAutomaticReconnect()) stays subscribed to nothing: it looks
  // "connected" again but stops receiving PlayerJoined/PlayerLeft/
  // RaceStarted/etc. for whatever room it was in — which is exactly what
  // "other players in the room show as alone" turned out to be.
  private currentRaceGroupId: string | null = null;

  // Room lifecycle (see RaceHub.cs + RacesController broadcasts)
  readonly playerJoined$ = new Subject<RaceDetailDto>();
  readonly playerLeft$ = new Subject<RaceDetailDto>();
  readonly playerReady$ = new Subject<RaceDetailDto>();
  readonly roomClosed$ = new Subject<RoomClosedDto>();
  readonly roomDeleted$ = new Subject<RoomDeletedDto>();

  // Race start sequence
  readonly raceStarted$ = new Subject<RaceDetailDto>();
  readonly raceCountdown$ = new Subject<RaceCountdownDto>();
  readonly raceBegin$ = new Subject<RaceBeginDto>();

  // In-race telemetry
  readonly playerProgress$ = new Subject<PlayerProgressDto>();
  readonly playerLapCompleted$ = new Subject<PlayerLapDto>();
  readonly playerFinished$ = new Subject<PlayerFinishedDto>();
  readonly raceFinished$ = new Subject<RaceFinishedDto>();

  // Friend presence (lobby)
  readonly friendOnline$ = new Subject<{ userId: string; displayName: string }>();
  readonly friendOffline$ = new Subject<{ userId: string }>();

  // Server-side validation/business-rule failures on a hub *method* call
  // (as opposed to a REST call, which gets its errors from the HTTP response).
  readonly raceError$ = new Subject<RaceErrorDto>();

  // Race chat
  readonly raceChatMessage$ = new Subject<RaceChatMessageDto>();

  // Friend invites
  readonly raceInviteReceived$ = new Subject<RaceInviteReceivedDto>();
  readonly raceInviteDeclined$ = new Subject<RaceInviteDeclinedDto>();

  // Reward events (published by RewardWorker via RewardNotificationRelayService)
  readonly rewardCredited$ = new Subject<RewardCreditedDto>();

  // Achievement events (published by AchievementsWorker via
  // AchievementNotificationRelayService)
  readonly achievementUnlocked$ = new Subject<AchievementUnlockedDto>();

  /**
   * Starts the connection if it isn't already up. Safe to call from every
   * screen's ngOnInit — concurrent callers all await the same in-flight
   * start instead of racing to open multiple sockets.
   *
   * Correctness here depends entirely on connection/startPromise being
   * reset back to null the moment the connection actually dies (see the
   * onclose handler in start()) — otherwise a stale resolved startPromise
   * from the *original* connect would make every future call here return
   * instantly without checking whether that connection is still alive.
   * That was the actual bug behind "online friends show as offline" and
   * room-join calls hanging forever: withAutomaticReconnect() gives up
   * after its retry window (0s/2s/10s/30s by default) and drops to
   * Disconnected, but nothing was clearing startPromise, so every
   * subsequent ensureConnected()/invoke() silently no-op'd against a dead
   * socket instead of reconnecting.
   */
  async ensureConnected(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    if (!this.startPromise) {
      this.startPromise = this.start();
    }

    await this.startPromise;
  }

  async disconnect(): Promise<void> {
    this.startPromise = null;
    await this.connection?.stop();
    this.connection = null;
  }

  joinRaceGroup(raceId: string): Promise<void> {
    this.currentRaceGroupId = raceId;
    return this.invoke('JoinRaceGroup', raceId);
  }

  leaveRaceGroup(raceId: string): Promise<void> {
    if (this.currentRaceGroupId === raceId) {
      this.currentRaceGroupId = null;
    }
    return this.invoke('LeaveRaceGroup', raceId);
  }

  reportProgress(raceId: string, lap: number, checkpoint: number, progress: number): Promise<void> {
    return this.invoke('ReportProgress', raceId, lap, checkpoint, progress);
  }

  reportLapCompleted(raceId: string, lapNumber: number, lapTimeMs: number): Promise<void> {
    return this.invoke('ReportLapCompleted', raceId, lapNumber, lapTimeMs);
  }

  reportFinished(raceId: string, totalTimeMs: number): Promise<void> {
    return this.invoke('ReportFinished', raceId, totalTimeMs);
  }

  sendRaceMessage(raceId: string, content: string): Promise<void> {
    return this.invoke('SendRaceMessage', raceId, content);
  }

  /** Invites a friend into the caller's current room — they must already be an accepted friend and the room must still be joinable. */
  inviteFriendToRace(raceId: string, friendUserId: string): Promise<void> {
    return this.invoke('InviteFriendToRace', raceId, friendUserId);
  }

  /** Tells the host the invitee isn't coming, so their UI can drop the "pending" state instead of guessing from silence. */
  declineRaceInvite(raceId: string, hostUserId: string): Promise<void> {
    return this.invoke('DeclineRaceInvite', raceId, hostUserId);
  }

  private async start(): Promise<void> {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.apiUrl.replace(/\/api\/?$/, '')}/hubs/race`, {
        // Matches the JwtBearerEvents.OnMessageReceived check in
        // RaceHub.Infrastructure.DependencyInjection, which only reads the
        // access_token query param for paths under /hubs/race — SignalR's
        // negotiate/WebSocket handshake can't set an Authorization header,
        // so the token has to travel in the URL instead.
        //
        // This is called again on every automatic-reconnect attempt, not
        // just the initial connect — so it needs to hand back a *valid*
        // token each time, not just whatever's sitting in storage. A
        // reconnect attempt firing after the 15-minute access token expiry
        // (e.g. after a laptop sleep or wifi drop) would otherwise present
        // an expired token, get rejected by the JWT bearer handshake, and
        // permanently fail to reconnect — which is a second, independent
        // way a genuinely-online user could end up looking offline.
        accessTokenFactory: () => this.getValidAccessToken(),
      })
      .withAutomaticReconnect()
      .configureLogging(environment.production ? signalR.LogLevel.Warning : signalR.LogLevel.Information)
      .build();

    this.registerHandlers(connection);

    connection.onclose(() => {
      // Automatic reconnect exhausted its retries and gave up. Clear both
      // fields so the next ensureConnected() call (the next page
      // navigation, or the next hub method invocation) starts a brand new
      // connection instead of trusting a startPromise that resolved long
      // ago against a socket that no longer exists.
      this.connection = null;
      this.startPromise = null;
    });

    connection.onreconnected(() => {
      // New ConnectionId under the hood -> re-join whatever room group we
      // were in before the drop, or PlayerJoined/etc. broadcasts for that
      // room silently stop reaching this client from here on. Friend
      // presence doesn't need the same treatment: OnConnectedAsync on the
      // server fires fresh on every (re)connect and re-broadcasts
      // FriendOnline on its own.
      if (this.currentRaceGroupId) {
        void this.invoke('JoinRaceGroup', this.currentRaceGroupId);
      }
    });

    this.connection = connection;

    await connection.start();
  }

  /** Returns the current access token, refreshing it first if it's expired — SignalR needs a *currently valid* token on every (re)connect attempt, not just whatever's in storage. */
  private async getValidAccessToken(): Promise<string> {
    if (this.authService.isAccessTokenExpired()) {
      const refreshed = await firstValueFrom(this.authService.refresh());
      return refreshed?.accessToken ?? '';
    }

    return this.authService.getAccessToken() ?? '';
  }

  private registerHandlers(connection: signalR.HubConnection): void {
    connection.on('PlayerJoined', (dto: RaceDetailDto) => this.playerJoined$.next(dto));
    connection.on('PlayerLeft', (dto: RaceDetailDto) => this.playerLeft$.next(dto));
    connection.on('PlayerReady', (dto: RaceDetailDto) => this.playerReady$.next(dto));
    connection.on('RoomClosed', (dto: RoomClosedDto) => this.roomClosed$.next(dto));
    connection.on('RoomDeleted', (dto: RoomDeletedDto) => this.roomDeleted$.next(dto));

    connection.on('RaceStarted', (dto: RaceDetailDto) => this.raceStarted$.next(dto));
    connection.on('RaceCountdown', (dto: RaceCountdownDto) => this.raceCountdown$.next(dto));
    connection.on('RaceBegin', (dto: RaceBeginDto) => this.raceBegin$.next(dto));

    connection.on('PlayerProgress', (dto: PlayerProgressDto) => this.playerProgress$.next(dto));
    connection.on('PlayerLapCompleted', (dto: PlayerLapDto) => this.playerLapCompleted$.next(dto));
    connection.on('PlayerFinished', (dto: PlayerFinishedDto) => this.playerFinished$.next(dto));
    connection.on('RaceFinished', (dto: RaceFinishedDto) => this.raceFinished$.next(dto));

    connection.on('FriendOnline', (dto: { userId: string; displayName: string }) => this.friendOnline$.next(dto));
    connection.on('FriendOffline', (dto: { userId: string }) => this.friendOffline$.next(dto));

    connection.on('RaceError', (dto: RaceErrorDto) => this.raceError$.next(dto));

    connection.on('RaceChatMessage', (dto: RaceChatMessageDto) => this.raceChatMessage$.next(dto));

    connection.on('RaceInviteReceived', (dto: RaceInviteReceivedDto) => this.raceInviteReceived$.next(dto));
    connection.on('RaceInviteDeclined', (dto: RaceInviteDeclinedDto) => this.raceInviteDeclined$.next(dto));
    connection.on('RewardCredited', (dto: RewardCreditedDto) => this.rewardCredited$.next(dto));
    connection.on('AchievementUnlocked', (dto: AchievementUnlockedDto) => this.achievementUnlocked$.next(dto));
  }

  private async invoke(method: string, ...args: unknown[]): Promise<void> {
    await this.ensureConnected();
    await this.connection!.invoke(method, ...args);
  }
}
