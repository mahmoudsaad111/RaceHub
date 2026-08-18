export interface CarSwatch {
  icon: string;
  color: string;
}

export interface CarShapeProfile {
  length: number;
  width: number;
  /** Corner rounding: [rear, front] — mirrored to both sides. Small = sharp/angular, large = rounded. */
  frontRadius: number;
  rearRadius: number;
  spoiler: boolean;
  hoodScoop: boolean;
}

/**
 * Matches the five decal renderers in RaceComponent.drawLivery() — that
 * switch statement is the source of truth for what each pattern actually
 * looks like; this is just the name. Adding a 6th pattern means adding a
 * case there too, not just a new value here.
 */
export type LiveryPattern = 'stripes' | 'bolt' | 'fade' | 'chevron' | 'flames';

export interface CarVisuals extends CarSwatch {
  shape: CarShapeProfile;
  /** Decal/badge accent color — deliberately distinct from the body color so the livery reads against it. */
  accentColor: string;
  livery: LiveryPattern;
}

/**
 * The backend Car entity only models racing stats (topSpeed, acceleration,
 * handling, braking, nitroCapacity) — it has no cosmetic fields at all, so
 * color/shape/livery are all derived client-side, keyed by car *name* so
 * Garage, the room, and the race canvas all resolve through this one map
 * and can never silently disagree about what a given car looks like.
 *
 * The seven legacy cars (Speedster…Zenith) are original archetypes with the
 * character each is going for noted per car below. The branded catalog
 * (2026 expansion) maps each real marque to its signature paint + body
 * style — hot hatches are short and rounded, supercars long and angular.
 * Shape deltas are large and deliberate (length/width/radius differences
 * of 15-25+ units, not a handful of px) since small deltas were
 * essentially invisible at the scale the race canvas renders cars at.
 */
const CAR_VISUALS: Record<string, CarVisuals> = {
  // Balanced all-rounder — the "default" silhouette everything else is a
  // deliberate departure from. Classic white double racing stripe.
  Speedster: {
    icon: '🏎️', color: '#e8524c', accentColor: '#f5f5f5', livery: 'stripes',
    shape: { length: 58, width: 30, frontRadius: 14, rearRadius: 14, spoiler: false, hoodScoop: false },
  },
  // Sleek sports coupe: long, narrow, sharp pointed nose, rounded tail.
  // Gold lightning-bolt decal against the blue body.
  Lightning: {
    icon: '⚡🚗', color: '#2f6fe0', accentColor: '#ffd23f', livery: 'bolt',
    shape: { length: 76, width: 22, frontRadius: 3, rearRadius: 20, spoiler: false, hoodScoop: false },
  },
  // GT car: compact and wide, heavily rounded, rear wing. Pearly fade
  // toward the tail for a ghostly look matching the name.
  Phantom: {
    icon: '🚙', color: '#d1394f', accentColor: '#f1e6ff', livery: 'fade',
    shape: { length: 52, width: 34, frontRadius: 17, rearRadius: 17, spoiler: true, hoodScoop: false },
  },
  // Aggressive angular tuner: long, narrow, sharp edges both ends, wing +
  // hood scoop. Sharp black chevrons for a tactical look.
  Shadow: {
    icon: '🚓', color: '#2fa34e', accentColor: '#1a1a1a', livery: 'chevron',
    shape: { length: 64, width: 26, frontRadius: 4, rearRadius: 4, spoiler: true, hoodScoop: true },
  },
  // Muscle car: short, very wide, blunt rounded nose, hood scoop, no wing.
  // Orange flame lick down the flank, the classic muscle-car decal.
  Thunder: {
    icon: '🚕', color: '#8b4fe0', accentColor: '#ff8a3d', livery: 'flames',
    shape: { length: 54, width: 38, frontRadius: 18, rearRadius: 8, spoiler: false, hoodScoop: true },
  },
  // Italian-supercar-style archetype: the most extreme silhouette in the
  // catalog — very low and wide with a near-diamond nose (frontRadius
  // close to 0) and a flat-cut tail, plus wing + scoop. Bold acid-green
  // body with black chevrons for maximum aggression.
  Vortex: {
    icon: '🚗', color: '#8fd13f', accentColor: '#111111', livery: 'chevron',
    shape: { length: 66, width: 40, frontRadius: 1, rearRadius: 3, spoiler: true, hoodScoop: true },
  },
  // British-supercar-style archetype: long, very low-slung, and gently
  // rounded rather than angular — reads as sleek/aerodynamic instead of
  // aggressive. No wing or scoop; the shape itself is the styling.
  // Signature-orange body with a subtle cream fade, evoking that school of
  // racing livery without reproducing any specific brand's exact color or
  // badge.
  Zenith: {
    icon: '🧡🏎️', color: '#ff8700', accentColor: '#fff3e0', livery: 'fade',
    shape: { length: 80, width: 24, frontRadius: 10, rearRadius: 16, spoiler: false, hoodScoop: false },
  },

  // ---- Branded catalog (2026 expansion) ------------------------------
  // Body colors follow each marque's signature paint where one exists
  // (papaya orange for McLaren, verde mantis for Lamborghini, rosso for
  // Ferrari, GT R's green magno, Chiron's two-tone blue...). Shapes map to
  // body style: hatches are short+wide+rounded, supercars/hypercars are
  // long+wide+angular with wings, GTs sit between.

  // — Entry tier: hot hatches. Short, tall-cabin, rounded both ends.
  'Mini JCW GP': {
    icon: '🔴🚙', color: '#c8102e', accentColor: '#f2f2f2', livery: 'stripes',
    shape: { length: 46, width: 32, frontRadius: 18, rearRadius: 18, spoiler: true, hoodScoop: false },
  },
  'VW Golf GTI Clubsport': {
    icon: '⚪🚗', color: '#f2f2f2', accentColor: '#d90429', livery: 'stripes',
    shape: { length: 50, width: 33, frontRadius: 16, rearRadius: 17, spoiler: false, hoodScoop: false },
  },
  'Toyota GR86': {
    icon: '🚗', color: '#1b2a4a', accentColor: '#e10600', livery: 'bolt',
    shape: { length: 56, width: 28, frontRadius: 10, rearRadius: 14, spoiler: true, hoodScoop: false },
  },
  'Honda Civic Type R': {
    icon: '🏁🚗', color: '#f4f4f4', accentColor: '#e10600', livery: 'chevron',
    shape: { length: 52, width: 32, frontRadius: 14, rearRadius: 18, spoiler: true, hoodScoop: true },
  },
  'Hyundai i30 N Performance': {
    icon: '🔵🚙', color: '#004b87', accentColor: '#e01f26', livery: 'fade',
    shape: { length: 50, width: 33, frontRadius: 15, rearRadius: 17, spoiler: false, hoodScoop: false },
  },

  // — Sport tier: driver's coupes and sports sedans. Mid-length, mid-width.
  'BMW M2': {
    icon: '🔵🚗', color: '#0066b1', accentColor: '#f2f2f2', livery: 'stripes',
    shape: { length: 58, width: 32, frontRadius: 12, rearRadius: 14, spoiler: false, hoodScoop: true },
  },
  'Nissan Z Performance': {
    icon: '🟡🚗', color: '#f5b301', accentColor: '#1a1a1a', livery: 'bolt',
    shape: { length: 60, width: 29, frontRadius: 8, rearRadius: 14, spoiler: true, hoodScoop: false },
  },
  'Toyota GR Supra': {
    icon: '🔴🏎️', color: '#d5001c', accentColor: '#111111', livery: 'flames',
    shape: { length: 64, width: 30, frontRadius: 6, rearRadius: 15, spoiler: true, hoodScoop: false },
  },
  'Audi RS3 Sportback': {
    icon: '🔴🚙', color: '#bb0a30', accentColor: '#c8c8c8', livery: 'fade',
    shape: { length: 50, width: 34, frontRadius: 15, rearRadius: 16, spoiler: false, hoodScoop: false },
  },
  'Lexus RC F Track Edition': {
    icon: '⚪🚗', color: '#f7f7f7', accentColor: '#c3002f', livery: 'chevron',
    shape: { length: 62, width: 31, frontRadius: 10, rearRadius: 14, spoiler: true, hoodScoop: true },
  },
  'Mercedes-AMG C63 S': {
    icon: '⭐🚗', color: '#9b9b9b', accentColor: '#1a1a1a', livery: 'stripes',
    shape: { length: 64, width: 31, frontRadius: 13, rearRadius: 15, spoiler: false, hoodScoop: false },
  },
  'BMW M4 Competition': {
    icon: '🟦🚗', color: '#004a99', accentColor: '#ffd23f', livery: 'bolt',
    shape: { length: 66, width: 32, frontRadius: 9, rearRadius: 14, spoiler: true, hoodScoop: true },
  },

  // — Performance tier: track weapons. Longer, lower, wings standard.
  'Chevrolet Corvette C8 Stingray': {
    icon: '🟨🏎️', color: '#f7c800', accentColor: '#111111', livery: 'flames',
    shape: { length: 70, width: 34, frontRadius: 3, rearRadius: 8, spoiler: true, hoodScoop: false },
  },
  'Porsche Cayman GT4 RS': {
    icon: '⚪🏎️', color: '#f4f4f4', accentColor: '#d5001c', livery: 'stripes',
    shape: { length: 62, width: 33, frontRadius: 8, rearRadius: 12, spoiler: true, hoodScoop: true },
  },
  'Nissan GT-R Nismo': {
    icon: '⚫🚗', color: '#3a3a3a', accentColor: '#e10600', livery: 'chevron',
    shape: { length: 66, width: 35, frontRadius: 6, rearRadius: 10, spoiler: true, hoodScoop: true },
  },
  'Aston Martin Vantage': {
    icon: '🟢🏎️', color: '#00594c', accentColor: '#f2f2f2', livery: 'fade',
    shape: { length: 66, width: 30, frontRadius: 9, rearRadius: 13, spoiler: false, hoodScoop: false },
  },
  'Mercedes-AMG GT R': {
    icon: '🟩🏎️', color: '#0f7b3c', accentColor: '#f2f2f2', livery: 'stripes',
    shape: { length: 70, width: 34, frontRadius: 4, rearRadius: 9, spoiler: true, hoodScoop: false },
  },

  // — Supercar tier: very low, wide, angular — near-diamond noses.
  'Audi R8 V10 Performance': {
    icon: '🔴🏎️', color: '#c3002f', accentColor: '#c8c8c8', livery: 'chevron',
    shape: { length: 68, width: 35, frontRadius: 4, rearRadius: 8, spoiler: true, hoodScoop: false },
  },
  'McLaren 570S': {
    icon: '🟠🏎️', color: '#ff8000', accentColor: '#111111', livery: 'bolt',
    shape: { length: 72, width: 33, frontRadius: 5, rearRadius: 10, spoiler: true, hoodScoop: false },
  },
  'Porsche 911 GT3 RS': {
    icon: '⚪🏎️', color: '#ececec', accentColor: '#d5001c', livery: 'stripes',
    shape: { length: 66, width: 34, frontRadius: 7, rearRadius: 12, spoiler: true, hoodScoop: false },
  },
  'Ferrari Roma': {
    icon: '🔴🐎', color: '#d40000', accentColor: '#f5e6c8', livery: 'fade',
    shape: { length: 70, width: 33, frontRadius: 6, rearRadius: 13, spoiler: false, hoodScoop: false },
  },
  'Lamborghini Huracan EVO': {
    icon: '🟢🏎️', color: '#7dbb2e', accentColor: '#111111', livery: 'chevron',
    shape: { length: 68, width: 36, frontRadius: 2, rearRadius: 6, spoiler: false, hoodScoop: true },
  },

  // — Hypercar tier: the most extreme silhouettes in the game.
  'McLaren 765LT': {
    icon: '🧡🏎️', color: '#ff6a00', accentColor: '#111111', livery: 'flames',
    shape: { length: 76, width: 34, frontRadius: 3, rearRadius: 7, spoiler: true, hoodScoop: true },
  },
  'Lamborghini Aventador SVJ': {
    icon: '🟧🏎️', color: '#e8641b', accentColor: '#111111', livery: 'bolt',
    shape: { length: 74, width: 38, frontRadius: 1, rearRadius: 4, spoiler: true, hoodScoop: true },
  },
  'Porsche 918 Spyder': {
    icon: '⚪🏎️', color: '#c8c8c8', accentColor: '#0d5c9c', livery: 'fade',
    shape: { length: 72, width: 35, frontRadius: 5, rearRadius: 9, spoiler: true, hoodScoop: false },
  },
  'Ferrari SF90 Stradale': {
    icon: '🔴⚡', color: '#cc0000', accentColor: '#111111', livery: 'bolt',
    shape: { length: 72, width: 36, frontRadius: 2, rearRadius: 7, spoiler: true, hoodScoop: false },
  },
  'Bugatti Chiron': {
    icon: '🔵🏎️', color: '#0d2c6b', accentColor: '#c8c8c8', livery: 'stripes',
    shape: { length: 76, width: 38, frontRadius: 3, rearRadius: 8, spoiler: false, hoodScoop: false },
  },
  'Koenigsegg Jesko': {
    icon: '⚪⚡', color: '#e9e9e9', accentColor: '#2b2b2b', livery: 'bolt',
    shape: { length: 78, width: 38, frontRadius: 1, rearRadius: 5, spoiler: true, hoodScoop: true },
  },
};

const DEFAULT_SHAPE: CarShapeProfile = CAR_VISUALS['Speedster'].shape;

const FALLBACK_VISUALS: CarVisuals[] = Object.values(CAR_VISUALS);

function hashString(value: string): number {
  let hash = 0;
  for (let i = 0; i < value.length; i++) {
    hash = (hash * 31 + value.charCodeAt(i)) | 0;
  }
  return Math.abs(hash);
}

function resolve(carName: string | null | undefined): CarVisuals {
  if (carName && CAR_VISUALS[carName]) {
    return CAR_VISUALS[carName];
  }

  const hash = hashString(carName ?? '');
  return FALLBACK_VISUALS[hash % FALLBACK_VISUALS.length];
}

/** Deterministic icon/color for a car by name. Falls back to a stable (name-hashed, not random) swatch for any car not in the fixed catalog above. */
export function getCarSwatch(carName: string | null | undefined): CarSwatch {
  return resolve(carName);
}

/** Deterministic body silhouette for a car by name — same fallback behavior as getCarSwatch. */
export function getCarShape(carName: string | null | undefined): CarShapeProfile {
  if (!carName) return DEFAULT_SHAPE;
  return resolve(carName).shape;
}

/** Deterministic livery (accent color + decal pattern) for a car by name — same fallback behavior as getCarSwatch. */
export function getCarLivery(carName: string | null | undefined): { accentColor: string; livery: LiveryPattern } {
  const visuals = resolve(carName);
  return { accentColor: visuals.accentColor, livery: visuals.livery };
}
