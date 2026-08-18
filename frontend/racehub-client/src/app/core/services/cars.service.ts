import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { CarDto } from '../models/car.models';

@Injectable({ providedIn: 'root' })
export class CarsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/cars`;

  getAll(): Observable<CarDto[]> {
    return this.http
      .get<ApiResponse<CarDto[]>>(this.baseUrl)
      .pipe(map((res) => res.data ?? []));
  }

  getById(id: string): Observable<CarDto | null> {
    return this.http
      .get<ApiResponse<CarDto>>(`${this.baseUrl}/${id}`)
      .pipe(map((res) => res.data ?? null));
  }

  buy(id: string): Observable<void> {
    return this.http
      .post<ApiResponse<void>>(`${this.baseUrl}/${id}/buy`, {})
      .pipe(map(() => void 0));
  }
}
