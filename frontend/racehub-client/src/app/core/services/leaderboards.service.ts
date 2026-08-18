import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { LeaderboardEntryDto } from '../models/api.models';

export type LeaderboardScope = 'global' | 'weekly' | 'track';

@Injectable({ providedIn: 'root' })
export class LeaderboardsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/leaderboards`;

  /**
   * global: rating ladder from PlayerStatistics (RankingWorker's read
   * model). weekly: wins in the last 7 days. track: fastest times on one
   * track — both of those read RaceHistoryEntry (StatisticsWorker's read
   * model) instead of aggregating RaceResult live.
   */
  get(scope: LeaderboardScope, trackId?: string): Observable<LeaderboardEntryDto[]> {
    return this.http
      .get<ApiResponse<LeaderboardEntryDto[]>>(this.baseUrl, {
        params: trackId ? { scope, trackId } : { scope },
      })
      .pipe(map((res) => res.data ?? []));
  }
}
