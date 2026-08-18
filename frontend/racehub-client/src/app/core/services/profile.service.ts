import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { ProfileDto } from '../models/profile.models';
import { PersonalBestDto } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class ProfileService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/users`;

  getMyProfile(): Observable<ProfileDto | null> {
    return this.http
      .get<ApiResponse<ProfileDto>>(`${this.baseUrl}/me/profile`)
      .pipe(map((res) => res.data ?? null));
  }

  /** Best finish time per track, from StatisticsWorker's RaceHistoryEntry read model — powers the "your PB" hint on the track picker. */
  getPersonalBests(): Observable<PersonalBestDto[]> {
    return this.http
      .get<ApiResponse<PersonalBestDto[]>>(`${this.baseUrl}/me/personal-bests`)
      .pipe(map((res) => res.data ?? []));
  }
}
