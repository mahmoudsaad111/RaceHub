import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { OpenRaceDto, RaceDetailDto } from '../models/race-api.models';

export interface RaceApiError {
  message: string;
  errorCode?: string;
}

/**
 * REST calls for room lifecycle (list/create/join/leave/ready/start).
 * These stay plain HTTP — the resulting state changes are what
 * RealtimeService then picks up via RaceHub's SignalR broadcasts, so every
 * connected client (not just the caller) sees the update.
 */
@Injectable({ providedIn: 'root' })
export class RacesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/races`;

  getOpenRaces(): Observable<OpenRaceDto[]> {
    return this.http
      .get<ApiResponse<OpenRaceDto[]>>(this.baseUrl)
      .pipe(map((res) => res.data ?? []), catchError(this.toError));
  }

  getById(raceId: string): Observable<RaceDetailDto> {
    return this.http
      .get<ApiResponse<RaceDetailDto>>(`${this.baseUrl}/${raceId}`)
      .pipe(map((res) => res.data as RaceDetailDto), catchError(this.toError));
  }

  create(trackId: string, carId: string, maxPlayers: number): Observable<RaceDetailDto> {
    return this.http
      .post<ApiResponse<RaceDetailDto>>(this.baseUrl, { trackId, carId, maxPlayers })
      .pipe(map((res) => res.data as RaceDetailDto), catchError(this.toError));
  }

  join(raceId: string, carId: string): Observable<RaceDetailDto> {
    return this.http
      .post<ApiResponse<RaceDetailDto>>(`${this.baseUrl}/${raceId}/join`, { carId })
      .pipe(map((res) => res.data as RaceDetailDto), catchError(this.toError));
  }

  acceptInvite(raceId: string): Observable<RaceDetailDto> {
    return this.http
      .post<ApiResponse<RaceDetailDto>>(`${this.baseUrl}/${raceId}/accept-invite`, {})
      .pipe(map((res) => res.data as RaceDetailDto), catchError(this.toError));
  }

  leave(raceId: string): Observable<void> {
    return this.http
      .post<ApiResponse<void>>(`${this.baseUrl}/${raceId}/leave`, {})
      .pipe(map(() => void 0), catchError(this.toError));
  }

  ready(raceId: string): Observable<RaceDetailDto> {
    return this.http
      .post<ApiResponse<RaceDetailDto>>(`${this.baseUrl}/${raceId}/ready`, {})
      .pipe(map((res) => res.data as RaceDetailDto), catchError(this.toError));
  }

  changeCar(raceId: string, carId: string): Observable<RaceDetailDto> {
    return this.http
      .put<ApiResponse<RaceDetailDto>>(`${this.baseUrl}/${raceId}/car`, { carId })
      .pipe(map((res) => res.data as RaceDetailDto), catchError(this.toError));
  }

  start(raceId: string): Observable<RaceDetailDto> {
    return this.http
      .post<ApiResponse<RaceDetailDto>>(`${this.baseUrl}/${raceId}/start`, {})
      .pipe(map((res) => res.data as RaceDetailDto), catchError(this.toError));
  }

  delete(raceId: string): Observable<void> {
    return this.http
      .delete<ApiResponse<void>>(`${this.baseUrl}/${raceId}`)
      .pipe(map(() => void 0), catchError(this.toError));
  }

  private toError(err: any) {
    const body = err?.error as ApiResponse<unknown> | undefined;

    const normalized: RaceApiError = {
      message: body?.message ?? 'Something went wrong. Please try again.',
      errorCode: body?.errorCode,
    };

    return throwError(() => normalized);
  }
}
