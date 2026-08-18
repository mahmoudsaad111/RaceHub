import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import {
  FriendDto,
  PendingFriendRequestDto,
} from '../models/api.models';

export interface FriendsApiError {
  message: string;
  errorCode?: string;
}

@Injectable({ providedIn: 'root' })
export class FriendsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/friends`;

  getFriends(): Observable<FriendDto[]> {
    return this.http
      .get<ApiResponse<FriendDto[]>>(this.baseUrl)
      .pipe(map((res) => res.data ?? []), catchError(this.toError));
  }

  getPendingRequests(): Observable<PendingFriendRequestDto[]> {
    return this.http
      .get<ApiResponse<PendingFriendRequestDto[]>>(`${this.baseUrl}/requests`)
      .pipe(map((res) => res.data ?? []), catchError(this.toError));
  }

  sendRequest(addresseeEmail: string): Observable<void> {
    return this.http
      .post<ApiResponse<void>>(`${this.baseUrl}/requests`, { addresseeEmail })
      .pipe(map(() => void 0), catchError(this.toError));
  }

  /** Backend route is POST /api/friends/requests/{friendshipId}/respond — the "requests" segment is required, matching FriendsController's [HttpPost("requests/{friendshipId:guid}/respond")]. */
  respondToRequest(friendshipId: string, accept: boolean): Observable<void> {
    return this.http
      .post<ApiResponse<void>>(`${this.baseUrl}/requests/${friendshipId}/respond`, { accept })
      .pipe(map(() => void 0), catchError(this.toError));
  }

  private toError(err: any) {
    const body = err?.error as ApiResponse<unknown> | undefined;

    const normalized: FriendsApiError = {
      message: body?.message ?? 'Something went wrong. Please try again.',
      errorCode: body?.errorCode,
    };

    return throwError(() => normalized);
  }
}
