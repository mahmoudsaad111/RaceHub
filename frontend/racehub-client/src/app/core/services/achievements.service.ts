import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { AchievementDto } from '../models/api.models';

/**
 * Unlocks themselves happen in RaceHub.AchievementsWorker off the
 * race.finished event; this just reads the merged catalog (locked badges
 * included) that GET /api/achievements builds.
 */
@Injectable({ providedIn: 'root' })
export class AchievementsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/achievements`;

  getMine(): Observable<AchievementDto[]> {
    return this.http
      .get<ApiResponse<AchievementDto[]>>(this.baseUrl)
      .pipe(map((res) => res.data ?? []));
  }
}
