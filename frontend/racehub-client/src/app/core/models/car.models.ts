/** Mirrors backend RaceHub.Application.DTOs.Cars.CarDto. */
export interface CarDto {
  id: string;
  name: string;
  topSpeed: number;
  acceleration: number;
  handling: number;
  braking: number;
  nitroCapacity: number;
  isActive: boolean;
  price: number;
  owned: boolean;
}
