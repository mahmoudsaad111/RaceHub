import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { TrackDto } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class TracksService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/tracks`;

  getAll(): Observable<TrackDto[]> {
    return this.http
      .get<ApiResponse<TrackDto[]>>(this.baseUrl)
      .pipe(map((res) => res.data ?? []));
  }

  getById(id: string): Observable<TrackDto | null> {
    return this.http
      .get<ApiResponse<TrackDto>>(`${this.baseUrl}/${id}`)
      .pipe(map((res) => res.data ?? null));
  }
}
