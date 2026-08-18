import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';

export interface NotificationDto {
  notificationId: string;
  type: string;
  title: string;
  message: string;
  data: string | null;
  isRead: boolean;
  createdAtUtc: string;
}

@Injectable({ providedIn: 'root' })
export class NotificationsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/notifications`;

  getAll(unreadOnly = false): Observable<NotificationDto[]> {
    return this.http
      .get<ApiResponse<NotificationDto[]>>(this.baseUrl, {
        params: { unreadOnly: unreadOnly ? 'true' : 'false' },
      })
      .pipe(map((res) => res.data ?? []));
  }

  getUnreadCount(): Observable<number> {
    return this.http
      .get<ApiResponse<number>>(`${this.baseUrl}/unread-count`)
      .pipe(map((res) => res.data ?? 0));
  }

  markAsRead(notificationId: string): Observable<void> {
    return this.http
      .post<ApiResponse<void>>(`${this.baseUrl}/${notificationId}/read`, {})
      .pipe(map(() => void 0));
  }

  markAllAsRead(): Observable<void> {
    return this.http
      .post<ApiResponse<void>>(`${this.baseUrl}/read-all`, {})
      .pipe(map(() => void 0));
  }
}
