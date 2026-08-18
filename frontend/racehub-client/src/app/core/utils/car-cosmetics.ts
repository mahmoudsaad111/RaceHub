/**
 * Deterministic cosmetics (color + silhouette) per car, keyed by car id.
 *
 * The backend Car entity only models racing stats (topSpeed/acceleration/
 * handling/braking) — it has no color or shape field. GarageComponent used
 * to derive a color purely from a car's position in the catalog array, and
 * RaceComponent never derived anything from car identity at all: the
 * player was always drawn with one hardcoded body shape in one hardcoded
 * color ('#ffd23f'), and every opponent used a second hardcoded shape/
 * color, regardless of which car anyone actually picked. That's why
 * choosing "Phantom" still drew the same yellow arrow every time.
 *
 * This hashes the car's id (stable regardless of catalog ordering, unlike
 * array index) into a fixed set of profiles, so the same car renders with
 * the same look everywhere — Garage, room, and the race canvas.
 */

export interface CarShapeProfile {
  /** Half-length of the nose, from center to front tip. */
  noseLength: number;
  /** Half-width at the widest point (the rear). */
  bodyWidth: number;
  /** How far back the widest point sits, as a fraction of total length. */
  taper: 'sharp' | 'wedge' | 'blunt';
  spoiler: boolean;
}

export interface CarCosmetic {
  color: string;
  icon: string;
  shape: CarShapeProfile;
}

const CAR_PALETTE: CarCosmetic[] = [
  { icon: '🏎️', color: '#e0453f', shape: { noseLength: 24, bodyWidth: 13, taper: 'sharp', spoiler: true } },
  { icon: '⚡🚗', color: '#2f6fe0', shape: { noseLength: 30, bodyWidth: 11, taper: 'wedge', spoiler: false } },
  { icon: '🚙', color: '#e0b23f', shape: { noseLength: 20, bodyWidth: 16, taper: 'blunt', spoiler: false } },
  { icon: '🚓', color: '#2fa34e', shape: { noseLength: 26, bodyWidth: 14, taper: 'wedge', spoiler: true } },
  { icon: '🚕', color: '#8b4fe0', shape: { noseLength: 22, bodyWidth: 15, taper: 'blunt', spoiler: true } },
];

function hashCarId(carId: string): number {
  let hash = 0;
  for (let i = 0; i < carId.length; i++) {
    hash = (hash * 31 + carId.charCodeAt(i)) >>> 0;
  }
  return hash;
}

export function getCarCosmetic(carId: string): CarCosmetic {
  const index = hashCarId(carId) % CAR_PALETTE.length;
  return CAR_PALETTE[index];
}
