/**
 * Mirrors backend RaceHub.Application.Common.ApiResponse<T> — every API
 * endpoint responds with this envelope, success or failure.
 */
export interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data?: T;
  errorCode?: string;
  /** Field-level validation errors, keyed by property name (PascalCase, matches backend model). */
  errors?: Record<string, string[]>;
}
