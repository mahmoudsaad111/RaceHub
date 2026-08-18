import { Component, ElementRef, AfterViewInit, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';

import { RealtimeService } from '../../core/services/realtime.service';
import { ActiveRaceStore } from '../../core/services/active-race.store';
import { AuthService } from '../../core/services/auth.service';
import { TracksService } from '../../core/services/tracks.service';
import { CarsService } from '../../core/services/cars.service';
import { TrackCheckpointDto } from '../../core/models/api.models';
import { CarShapeProfile, LiveryPattern, getCarLivery, getCarShape, getCarSwatch } from '../../core/config/car-visuals';

interface OpponentState {
  userId: string;
  displayName: string;
  carName: string;
  lap: number;
  checkpoint: number;
  segmentProgress: number;
  finished: boolean;
  position: number | null;
  /**
   * False until this opponent's first real playerProgress/lapCompleted
   * telemetry arrives. Before that, checkpoint/segmentProgress are just
   * placeholder zeros with no real meaning, so rendering falls back to
   * their starting-grid slot instead — otherwise there's a jarring snap
   * the instant the race begins, from the nicely-lined-up grid to
   * whatever (checkpoint=0, progress=0) interpolates to (the checkpoint
   * *behind* the line, not the line itself).
   */
  hasLiveData: boolean;
}

/** A faded dark streak left on the road under hard cornering/braking; fades out over a few seconds. */
interface SkidMark {
  x: number;
  y: number;
  angle: number;
  alpha: number;
}

/** A single transient puff: tire smoke, dust, or a fence-impact spark. */
interface Particle {
  x: number;
  y: number;
  vx: number;
  vy: number;
  size: number;
  color: string;
  life: number;
  maxLife: number;
}

const MAX_SKID_MARKS = 500;
const MAX_PARTICLES = 260;

// Chase-camera field of view, in virtual world units shown horizontally.
// Zooms out toward MAX_VIEW_WIDTH as speed increases (a classic "sense of
// speed" technique — more forward visibility and a wider view at speed,
// tighter/more detailed at low speed) rather than a fixed zoom level.
// Both bumped up from their original 760/1150 — the resting view was
// tight enough that very little of the track's shape/curves were visible
// ahead of the car; this trades a little close-up car detail for a lot
// more track visibility at all speeds.
const BASE_VIEW_WIDTH = 1450;
const MAX_VIEW_WIDTH = 2000;
const CAMERA_POSITION_SMOOTHING = 9; // higher = camera catches up to the car faster
const CAMERA_ROTATION_SMOOTHING = 6;
const CAMERA_ZOOM_SMOOTHING = 3;

interface CarPhysicsProfile {
  maxSpeed: number; // px/s
  acceleration: number; // px/s^2
  braking: number; // px/s^2
  turnRate: number; // rad/s at full speed
  nitroCapacity: number; // 0-100, max nitro meter size
}

type TrackTheme = 'grass' | 'desert' | 'beach' | 'city' | 'forest';

interface ThemePalette {
  background: string;
  accent: string;
  road: string;
  roadEdge: string;
  fenceMain: string;
  fenceAlt: string;
  /** 0 = full daylight, 1 = full night. Drives the ambient tint overlay and how visible headlight cones are. */
  ambientDarkness: number;
  curbA: string;
  curbB: string;
}

const THEME_PALETTES: Record<TrackTheme, ThemePalette> = {
  grass: { background: '#173018', accent: '#204225', road: '#3a3f45', roadEdge: 'rgba(255,255,255,0.35)', fenceMain: '#e8e8e8', fenceAlt: '#d1343b', ambientDarkness: 0, curbA: '#d1343b', curbB: '#f5f5f5' },
  desert: { background: '#cba36b', accent: '#dbb877', road: '#8a6a44', roadEdge: 'rgba(255,255,255,0.28)', fenceMain: '#f2e6c9', fenceAlt: '#a8502c', ambientDarkness: 0, curbA: '#a8502c', curbB: '#f2e6c9' },
  beach: { background: '#e8d9a0', accent: '#3aa1c9', road: '#c9b385', roadEdge: 'rgba(255,255,255,0.45)', fenceMain: '#ffffff', fenceAlt: '#1f7fae', ambientDarkness: 0, curbA: '#1f7fae', curbB: '#ffffff' },
  city: { background: '#2b2f36', accent: '#383e47', road: '#454b54', roadEdge: 'rgba(255,220,80,0.4)', fenceMain: '#dcdcdc', fenceAlt: '#f2c94c', ambientDarkness: 0.55, curbA: '#f2c94c', curbB: '#2b2f36' },
  forest: { background: '#132b16', accent: '#1b3b1f', road: '#4a4235', roadEdge: 'rgba(200,255,200,0.28)', fenceMain: '#7a5a3a', fenceAlt: '#3f2c1a', ambientDarkness: 0.2, curbA: '#3f2c1a', curbB: '#c9b385' },
};

function themeFromTrackName(name: string): TrackTheme {
  const lower = name.toLowerCase();
  if (lower.includes('desert')) return 'desert';
  if (lower.includes('beach')) return 'beach';
  if (lower.includes('city')) return 'city';
  if (lower.includes('forest')) return 'forest';
  return 'grass';
}

// Car color/shape are both resolved through the shared car-visuals config
// (see that file for why: keeps Garage, the room, and this screen from
// ever silently disagreeing about what a given car looks like).



/** Cheap deterministic pseudo-random generator so scenery is stable across frames without storing state. */
function hash2(x: number, y: number): number {
  const s = Math.sin(x * 127.1 + y * 311.7) * 43758.5453;
  return s - Math.floor(s);
}

function clamp255(value: number): number {
  return Math.max(0, Math.min(255, value));
}

function hexToRgb(hex: string): [number, number, number] {
  const clean = hex.replace('#', '');
  const bigint = parseInt(clean, 16);
  return [(bigint >> 16) & 255, (bigint >> 8) & 255, bigint & 255];
}

function rgbToHex(r: number, g: number, b: number): string {
  return `#${[r, g, b].map((v) => clamp255(Math.round(v)).toString(16).padStart(2, '0')).join('')}`;
}

/** Blends a hex color toward white by `amount` (0-1) — used for the body gradient's highlight edge. */
function lighten(hex: string, amount: number): string {
  const [r, g, b] = hexToRgb(hex);
  return rgbToHex(r + (255 - r) * amount, g + (255 - g) * amount, b + (255 - b) * amount);
}

/** Blends a hex color toward black by `amount` (0-1) — used for the body gradient's shaded edge. */
function darken(hex: string, amount: number): string {
  const [r, g, b] = hexToRgb(hex);
  return rgbToHex(r * (1 - amount), g * (1 - amount), b * (1 - amount));
}

function hexToRgba(hex: string, alpha: number): string {
  const [r, g, b] = hexToRgb(hex);
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

const CANVAS_WIDTH = 3000;
const CANVAS_HEIGHT = 1700;
const FRICTION = 220; // px/s^2, natural deceleration with no input
const PROGRESS_REPORT_INTERVAL_MS = 120; // ~8x/sec, matches RaceHub.cs's documented 5-10x/sec

// Cars are drawn (and collide) at shape-size × this factor. Pure realism
// (~5% of screen width) collapses all detail into a blob, but 1.9× proved
// cartoonishly large and hid the road behind the car — 1.4× is the sweet
// spot: clearly visible bodywork detail without dominating the frame.
// Rendering and collision both use it so the visual never lies about the
// hitbox.
const CAR_RENDER_SCALE = 1.4;

const FENCE_MARGIN = 14; // px gap between the drivable road edge and the fence line
const BOUNCE_DAMPING = 0.4; // speed multiplier applied on fence impact

// Starting grid: rows staggered back from the line, cars side-by-side
// within a row instead of everyone stacked single-file along the track.
// carsPerRow is derived from the start line's actual road width so a
// narrow track naturally falls back to fewer cars per row (2 or even 1)
// while a wide one fits 3 abreast, capped at GRID_MAX_PER_ROW either way.
const GRID_ROW_SPACING = 115; // px between rows, back from the line (sized for CAR_RENDER_SCALE cars)
const GRID_COL_SPACING = 95; // px between cars within a row
const GRID_MAX_PER_ROW = 3;

// Car-to-car crash: checked as an axis-aligned box overlap in the
// player's own local frame (forward = local X, sideways = local Y) using
// each car's real length/width from car-visuals — a plain circle sized
// to the car's length was flagging cars as "crashed" while they were
// still visibly a car-width apart side by side, since a circle can't be
// narrower sideways than it is front-to-back. SHRINK requires genuine
// visual overlap (not just edge-touching) before it counts as a hit.
const CRASH_BOX_SHRINK = 0.82;
const CRASH_CHECK_MAX_DISTANCE = 150; // px, cheap early-out before the per-opponent shape lookup + rotation math
const CRASH_BOUNCE_DAMPING = 0.3;
const CRASH_PUSH_APART_SPEED = 40; // px/s impulse applied to the player, away from the opponent
const CRASH_EFFECT_COOLDOWN_MS = 350; // per-opponent, so overlapping doesn't spam sparks every frame
const CRASH_SHAKE_MS = 220;
const CRASH_SHAKE_MAGNITUDE = 9; // px, in screen space

const NITRO_DRAIN_PER_SEC = 42;
const NITRO_REGEN_PER_SEC = 14;
const NITRO_ACCEL_MULTIPLIER = 1.8;
const NITRO_MAX_SPEED_MULTIPLIER = 1.35;

interface GridSlot {
  x: number;
  y: number;
  angle: number;
}

/**
 * Assigns every participant a static starting-grid slot: rows staggered
 * back from the line, cars arranged side-by-side within each row instead
 * of stacked single-file along the track. `participantIds` must already
 * be in the same order on every client (callers sort it) so everyone
 * independently computes an identical grid without the server needing to
 * broadcast starting positions.
 */
function computeStartGrid(checkpoints: TrackCheckpointDto[], participantIds: string[]): Map<string, GridSlot> {
  const slots = new Map<string, GridSlot>();
  if (checkpoints.length < 2 || participantIds.length === 0) return slots;

  const start = checkpoints[0];
  const aim = checkpoints[1];

  const ax = Number(start.positionX);
  const ay = Number(start.positionY);
  const forwardX = Number(aim.positionX) - ax;
  const forwardY = Number(aim.positionY) - ay;
  const forwardLen = Math.hypot(forwardX, forwardY) || 1;
  const fwdX = forwardX / forwardLen;
  const fwdY = forwardY / forwardLen;
  // Perpendicular (lateral) direction, for spacing cars within a row.
  const latX = -fwdY;
  const latY = fwdX;

  const roadWidth = Number(start.width);
  const carsPerRow = Math.max(1, Math.min(GRID_MAX_PER_ROW, Math.floor(roadWidth / GRID_COL_SPACING)));

  participantIds.forEach((userId, i) => {
    const row = Math.floor(i / carsPerRow);
    const rowStartIndex = row * carsPerRow;
    const carsInThisRow = Math.min(carsPerRow, participantIds.length - rowStartIndex);
    const colIndex = i - rowStartIndex;
    const colOffset = colIndex - (carsInThisRow - 1) / 2;

    const backDistance = row * GRID_ROW_SPACING;
    const x = ax - fwdX * backDistance + latX * colOffset * GRID_COL_SPACING;
    const y = ay - fwdY * backDistance + latY * colOffset * GRID_COL_SPACING;
    const angle = Math.atan2(fwdY, fwdX);

    slots.set(userId, { x, y, angle });
  });

  return slots;
}

/** Fallback path if a track somehow has no seeded checkpoints. Matches Classic Track's real seeded geometry. */
function fallbackOvalCheckpoints(): TrackCheckpointDto[] {
  const count = 28;
  const checkpoints: TrackCheckpointDto[] = [];
  for (let i = 0; i < count; i++) {
    const angle = (i / count) * 2 * Math.PI - Math.PI / 2;
    checkpoints.push({
      sequence: i,
      positionX: 1500 + 1150 * Math.cos(angle),
      positionY: 850 + 550 * Math.sin(angle),
      width: 280,
    });
  }
  return checkpoints;
}

/** Maps a 0-100 car stat into a usable physics constant range. */
function statToProfile(topSpeed: number, acceleration: number, handling: number, braking: number, nitroCapacity: number): CarPhysicsProfile {
  return {
    maxSpeed: 380 + (topSpeed / 100) * 340, // 380-720 px/s
    acceleration: 480 + (acceleration / 100) * 420, // 480-900 px/s^2
    braking: 620 + (braking / 100) * 500, // 620-1120 px/s^2
    turnRate: 2.4 + (handling / 100) * 1.6, // 2.4-4.0 rad/s
    nitroCapacity: 60 + (nitroCapacity / 100) * 40, // 60-100 meter size
  };
}

@Component({
  selector: 'rh-race',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './race.component.html',
  styleUrl: './race.component.scss',
})
export class RaceComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('raceCanvas') private canvasRef!: ElementRef<HTMLCanvasElement>;

  private readonly realtime = inject(RealtimeService);
  private readonly activeRaceStore = inject(ActiveRaceStore);
  private readonly authService = inject(AuthService);
  private readonly tracksService = inject(TracksService);
  private readonly carsService = inject(CarsService);
  private readonly router = inject(Router);

  raceId: string | null = null;
  trackName = 'Track';
  totalLaps = 3;

  currentLap = 1;
  elapsedMs = 0;
  bestLapMs: number | null = null;
  nitroPercent = 100;

  position = 1;
  totalPlayers = 1;

  countdown: number | null = null;
  raceStarted = false;
  raceFinished = false;
  controlsEnabled = false;

  opponents: OpponentState[] = [];

  // Actual on-screen pixel resolution of the canvas element (device-pixel-ratio
  // aware for crispness), kept in sync with .track-canvas-wrap's real size via
  // a ResizeObserver in ngAfterViewInit. Everything is still *drawn* in the
  // fixed CANVAS_WIDTH x CANVAS_HEIGHT "virtual" coordinate space (which also
  // matches the checkpoint coordinates the backend seeds) — render() applies a
  // single stretch transform mapping virtual space onto whatever this actually
  // is, so the whole scene always fills the container edge-to-edge with no
  // blank bars, on any screen aspect ratio.
  private canvasPixelWidth = CANVAS_WIDTH;
  private canvasPixelHeight = CANVAS_HEIGHT;
  private resizeObserver: ResizeObserver | null = null;

  private checkpoints: TrackCheckpointDto[] = fallbackOvalCheckpoints();
  private avgRoadWidth = 150;
  // Precomputed once per checkpoint-load (not per-frame) so drawFences()
  // and applyFenceCollision() always agree on exactly where the fence is
  // — see computeFencePolylines() doc comment for why that consistency
  // matters for collision accuracy, not just draw performance.
  private innerFencePoints: { x: number; y: number }[] = [];
  private outerFencePoints: { x: number; y: number }[] = [];
  private startGrid = new Map<string, GridSlot>();
  private myUserId = '';
  private theme: ThemePalette = THEME_PALETTES.grass;
  private physics: CarPhysicsProfile = statToProfile(70, 70, 70, 70, 70);
  private playerCarColor = getCarSwatch('Speedster').color;
  private playerCarShape = getCarShape('Speedster');
  private playerCarName = 'Speedster';

  // Chase-camera state, smoothed independently from the raw physics state
  // each frame in updateCamera() so camera motion reads as cinematic
  // damping rather than being rigidly glued to every physics micro-jitter.
  // cameraAngle is the world-rotation applied so the car's current heading
  // always maps to "up" on screen; viewWidth is how many virtual world
  // units are currently visible horizontally (zooms out with speed).
  private cameraX = 0;
  private cameraY = 0;
  private cameraAngle = 0;
  private viewWidth = BASE_VIEW_WIDTH;
  private cameraInitialized = false;

  private skidMarks: SkidMark[] = [];
  private particles: Particle[] = [];
  private lastSkidAt = 0;

  // Car state, in canvas coordinate space.
  private carX = 0;
  private carY = 0;
  private carAngle = 0; // radians, 0 = pointing along +x
  private carSpeed = 0; // px/s, negative = reversing
  private nitroMeter = 100;
  private boosting = false;

  // Screen-space camera shake, used for the car-crash impact feel.
  private crashShakeUntilMs = 0;
  private crashShakeSeed = 0;
  private readonly lastCrashEffectAt = new Map<string, number>();

  private nextCheckpoint = 1;
  private lapStartMs = 0;
  private raceStartMs = 0;
  private isBraking = false;

  private readonly pressedKeys = new Set<string>();
  private animationFrameId: number | null = null;
  private lastFrameTime: number | null = null;
  private lastProgressReportAt = 0;

  private ctx: CanvasRenderingContext2D | null = null;
  private subscriptions = new Subscription();
  private keydownHandler = (e: KeyboardEvent) => this.onKeyDown(e);
  private keyupHandler = (e: KeyboardEvent) => this.onKeyUp(e);

  ngOnInit(): void {
    const race = this.activeRaceStore.currentRace();
    if (!race) {
      this.router.navigateByUrl('/lobby');
      return;
    }

    this.raceId = race.id;
    this.trackName = race.trackName;
    this.totalLaps = race.totalLaps;
    this.totalPlayers = race.players.length;
    this.theme = THEME_PALETTES[themeFromTrackName(race.trackName)];
    this.computeFencePolylines(); // seeded from fallbackOvalCheckpoints() until the real track loads below

    const currentUserId = this.authService.currentUser()?.userId;
    this.myUserId = currentUserId ?? '';

    this.opponents = race.players
      .filter((p) => p.userId !== currentUserId)
      .map((p) => ({
        userId: p.userId,
        displayName: p.displayName,
        carName: p.carName,
        lap: 1,
        checkpoint: 0,
        segmentProgress: 0,
        finished: p.status === 'Finished',
        position: null,
        hasLiveData: false,
      }));

    // Load real track geometry so the canvas draws the actual path this
    // race is using, not a placeholder.
    this.tracksService.getById(race.trackId).subscribe((track) => {
      if (track && track.checkpoints.length > 0) {
        this.checkpoints = [...track.checkpoints].sort((a, b) => a.sequence - b.sequence);
      }

      this.avgRoadWidth = this.checkpoints.reduce((sum, c) => sum + Number(c.width), 0) / this.checkpoints.length;
      this.computeFencePolylines();

      // Sorted so every client (self + every opponent) independently
      // computes the exact same grid without any server coordination.
      const participantIds = [this.myUserId, ...this.opponents.map((o) => o.userId)].sort();
      this.startGrid = computeStartGrid(this.checkpoints, participantIds);

      this.resetCarToStart();
    });

    // Load the player's own car stats so their handling matches their
    // garage pick rather than a flat default.
    const myPlayer = race.players.find((p) => p.userId === currentUserId);
    if (myPlayer) {
      this.playerCarColor = getCarSwatch(myPlayer.carName).color;
      this.playerCarShape = getCarShape(myPlayer.carName);
      this.playerCarName = myPlayer.carName;

      this.carsService.getById(myPlayer.carId).subscribe((car) => {
        if (car) {
          this.physics = statToProfile(car.topSpeed, car.acceleration, car.handling, car.braking, car.nitroCapacity);
          this.nitroMeter = this.physics.nitroCapacity;
          this.nitroPercent = 100;
        }
      });
    }

    this.subscriptions.add(
      this.realtime.raceCountdown$.subscribe((dto) => {
        if (dto.raceId === this.raceId) {
          this.countdown = dto.seconds;
        }
      }),
    );

    this.subscriptions.add(
      this.realtime.raceBegin$.subscribe((dto) => {
        if (dto.raceId === this.raceId) {
          this.startRace();
        }
      }),
    );

    this.subscriptions.add(
      this.realtime.playerProgress$.subscribe((dto) => {
        if (dto.raceId !== this.raceId) return;

        const opponent = this.opponents.find((o) => o.userId === dto.userId);
        if (opponent) {
          opponent.lap = dto.lap;
          opponent.checkpoint = dto.checkpoint;
          opponent.segmentProgress = dto.progress;
          opponent.hasLiveData = true;
        }

        this.recalculatePositions();
      }),
    );

    this.subscriptions.add(
      this.realtime.playerLapCompleted$.subscribe((dto) => {
        if (dto.raceId !== this.raceId) return;

        const opponent = this.opponents.find((o) => o.userId === dto.userId);
        if (opponent) {
          opponent.lap = dto.lapNumber + 1;
          opponent.checkpoint = 0;
          opponent.segmentProgress = 0;
          opponent.hasLiveData = true;
        }

        this.recalculatePositions();
      }),
    );

    this.subscriptions.add(
      this.realtime.playerFinished$.subscribe((dto) => {
        if (dto.raceId !== this.raceId) return;

        const opponent = this.opponents.find((o) => o.userId === dto.userId);
        if (opponent) {
          opponent.finished = true;
          opponent.position = dto.position;
        }

        this.recalculatePositions();
      }),
    );

    this.subscriptions.add(
      this.realtime.raceFinished$.subscribe((dto) => {
        if (dto.raceId !== this.raceId) return;

        this.raceFinished = true;
        this.controlsEnabled = false;
        this.stopLoop();
        this.activeRaceStore.setLastResults(dto);
        this.router.navigateByUrl('/results');
      }),
    );

    this.subscriptions.add(
      this.realtime.raceError$.subscribe((dto) => {
        console.error('Race error:', dto);
      }),
    );

    this.realtime.ensureConnected().then(() => {
      this.realtime.joinRaceGroup(this.raceId!);
    });
  }

  ngAfterViewInit(): void {
    this.ctx = this.canvasRef.nativeElement.getContext('2d');
    window.addEventListener('keydown', this.keydownHandler);
    window.addEventListener('keyup', this.keyupHandler);

    const wrap = this.canvasRef.nativeElement.parentElement;
    if (wrap) {
      this.resizeObserver = new ResizeObserver(() => this.resizeCanvasToContainer());
      this.resizeObserver.observe(wrap);
    }
    this.resizeCanvasToContainer();

    this.animationFrameId = requestAnimationFrame((t) => this.tick(t));
  }

  /** Matches the canvas's actual pixel buffer to its container's real on-screen size, so render()'s stretch-to-fill transform has an accurate target. */
  private resizeCanvasToContainer(): void {
    const wrap = this.canvasRef.nativeElement.parentElement;
    if (!wrap) return;

    const rect = wrap.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) return;

    const dpr = window.devicePixelRatio || 1;
    const canvas = this.canvasRef.nativeElement;

    canvas.width = Math.round(rect.width * dpr);
    canvas.height = Math.round(rect.height * dpr);
    canvas.style.width = `${rect.width}px`;
    canvas.style.height = `${rect.height}px`;

    this.canvasPixelWidth = canvas.width;
    this.canvasPixelHeight = canvas.height;
  }

  ngOnDestroy(): void {
    this.stopLoop();
    window.removeEventListener('keydown', this.keydownHandler);
    window.removeEventListener('keyup', this.keyupHandler);
    this.resizeObserver?.disconnect();
    this.subscriptions.unsubscribe();
    if (this.raceId) {
      void this.realtime.leaveRaceGroup(this.raceId);
    }
  }

  get elapsed(): string {
    return formatMs(this.elapsedMs);
  }

  get bestLap(): string {
    return this.bestLapMs == null ? '--:--.--' : formatMs(this.bestLapMs);
  }

  back(): void {
    this.router.navigateByUrl('/lobby');
  }

  private onKeyDown(e: KeyboardEvent): void {
    this.pressedKeys.add(e.key.toLowerCase());
  }

  private onKeyUp(e: KeyboardEvent): void {
    this.pressedKeys.delete(e.key.toLowerCase());
  }

  private isPressed(...keys: string[]): boolean {
    return keys.some((k) => this.pressedKeys.has(k));
  }

  private resetCarToStart(): void {
    const slot = this.startGrid.get(this.myUserId);

    if (slot) {
      this.carX = slot.x;
      this.carY = slot.y;
      this.carAngle = slot.angle;
    } else {
      // Fallback for the brief window before checkpoints/grid are loaded.
      const start = this.checkpoints[0];
      const aim = this.checkpoints[1] ?? this.checkpoints[0];

      this.carX = Number(start.positionX);
      this.carY = Number(start.positionY);
      this.carAngle = Math.atan2(Number(aim.positionY) - this.carY, Number(aim.positionX) - this.carX);
    }

    this.carSpeed = 0;
    this.nextCheckpoint = 1 % this.checkpoints.length;

    // Snap the camera straight to the start position/heading instead of
    // smoothing in from wherever it defaulted to (0,0) — otherwise the
    // very first frame shows a jarring pan across the whole map.
    this.cameraX = this.carX;
    this.cameraY = this.carY;
    this.cameraAngle = -Math.PI / 2 - this.carAngle;
    this.cameraInitialized = true;
  }

  /**
   * Precomputes the inner/outer fence boundary polylines once per
   * checkpoint-load, instead of approximating the boundary every frame via
   * nearest-centerline-segment distance (the old applyFenceCollision()
   * approach). Using the exact same offset points drawFences() draws means
   * collision and the visible fence line can never disagree — and, more
   * importantly, gives applyFenceCollision() real line geometry to run a
   * swept intersection test against, which is what actually stops a fast
   * car from tunneling through the fence on a sharp corner: a pure
   * "distance from current position to nearest boundary" check only
   * catches the car if it happens to REST near the fence, not if a single
   * frame's worth of movement carried it clean through a thin gap near a
   * tight bend.
   */
  private computeFencePolylines(): void {
    const len = this.checkpoints.length;
    if (len < 2) {
      this.innerFencePoints = [];
      this.outerFencePoints = [];
      return;
    }

    const inner: { x: number; y: number }[] = [];
    const outer: { x: number; y: number }[] = [];

    for (let i = 0; i < len; i++) {
      const cur = this.checkpoints[i];
      const prev = this.checkpoints[(i - 1 + len) % len];
      const next = this.checkpoints[(i + 1) % len];

      const dirX = Number(next.positionX) - Number(prev.positionX);
      const dirY = Number(next.positionY) - Number(prev.positionY);
      const dirLen = Math.hypot(dirX, dirY) || 1;
      const nx = -dirY / dirLen;
      const ny = dirX / dirLen;

      const halfWidth = Number(cur.width) / 2 + FENCE_MARGIN;
      const cx = Number(cur.positionX);
      const cy = Number(cur.positionY);

      inner.push({ x: cx - nx * halfWidth, y: cy - ny * halfWidth });
      outer.push({ x: cx + nx * halfWidth, y: cy + ny * halfWidth });
    }

    this.innerFencePoints = inner;
    this.outerFencePoints = outer;
  }

  /**
   * Resolves where to actually draw/collide-against an opponent: their
   * static starting-grid slot while the race hasn't started yet (so
   * everyone visibly lines up together instead of appearing spread out
   * along the track — a quirk of checkpoint 0 meaning "heading toward the
   * line", which at rest interpolates to the *previous* checkpoint, not
   * the line itself), or their live network-telemetry position
   * (checkpoint + segment progress) once the race is actually running.
   */
  private getOpponentRenderPosition(opponent: OpponentState): { x: number; y: number; angle: number } {
    if (!opponent.hasLiveData) {
      const slot = this.startGrid.get(opponent.userId);
      if (slot) return slot;
    }

    const len = this.checkpoints.length;
    const prevIndex = (opponent.checkpoint - 1 + len) % len;
    const prev = this.checkpoints[prevIndex];
    const next = this.checkpoints[opponent.checkpoint % len];

    const t = Math.max(0, Math.min(1, opponent.segmentProgress));
    const x = Number(prev.positionX) + (Number(next.positionX) - Number(prev.positionX)) * t;
    const y = Number(prev.positionY) + (Number(next.positionY) - Number(prev.positionY)) * t;
    const angle = Math.atan2(Number(next.positionY) - Number(prev.positionY), Number(next.positionX) - Number(prev.positionX));

    return { x, y, angle };
  }

  /**
   * Smoothly moves/rotates/zooms the chase camera toward the car each
   * frame. Exponential (frame-rate-independent) smoothing rather than a
   * fixed lerp fraction, so it behaves the same at 30fps or 144fps.
   * Position and rotation are smoothed at different rates deliberately —
   * rotation lags slightly more so quick steering corrections don't spin
   * the whole world dizzyingly, while position stays reasonably tight so
   * the car doesn't visually drift off-center.
   *
   * The position target is a LOOK-AHEAD point in front of the car, not
   * the car itself: the camera leads by ~90 world units at rest, growing
   * to ~250 at full speed. On a screen where the car's heading is "up",
   * that places the car in the lower third with the road you're about to
   * drive filling the upper two — centering the camera on the car itself
   * wasted the top half of the screen on tarmac you've already passed.
   */
  private updateCamera(dt: number): void {
    if (!this.cameraInitialized) {
      this.cameraX = this.carX;
      this.cameraY = this.carY;
      this.cameraAngle = -Math.PI / 2 - this.carAngle;
      this.cameraInitialized = true;
    }

    const targetAngle = -Math.PI / 2 - this.carAngle;

    // Shortest-path angle delta so the camera never spins the "long way"
    // around when crossing the -PI/PI wrap boundary.
    let angleDiff = targetAngle - this.cameraAngle;
    angleDiff = ((angleDiff + Math.PI) % (Math.PI * 2)) - Math.PI;

    const posT = 1 - Math.exp(-CAMERA_POSITION_SMOOTHING * dt);
    const rotT = 1 - Math.exp(-CAMERA_ROTATION_SMOOTHING * dt);
    const zoomT = 1 - Math.exp(-CAMERA_ZOOM_SMOOTHING * dt);

    const speedFraction = Math.min(1, Math.abs(this.carSpeed) / this.physics.maxSpeed);
    const lookAhead = 90 + speedFraction * 160;

    const targetX = this.carX + Math.cos(this.carAngle) * lookAhead;
    const targetY = this.carY + Math.sin(this.carAngle) * lookAhead;

    this.cameraX += (targetX - this.cameraX) * posT;
    this.cameraY += (targetY - this.cameraY) * posT;
    this.cameraAngle += angleDiff * rotT;

    const targetViewWidth = BASE_VIEW_WIDTH + speedFraction * (MAX_VIEW_WIDTH - BASE_VIEW_WIDTH);
    this.viewWidth += (targetViewWidth - this.viewWidth) * zoomT;
  }

  /** Spawns a fading skid mark under the car — called from updatePhysics() while cornering hard or braking at speed. */
  private spawnSkidMark(timestampMs: number): void {
    if (timestampMs - this.lastSkidAt < 40) return; // cap density so marks don't overlap into a solid smear
    this.lastSkidAt = timestampMs;

    this.skidMarks.push({ x: this.carX, y: this.carY, angle: this.carAngle, alpha: 0.5 });
    if (this.skidMarks.length > MAX_SKID_MARKS) {
      this.skidMarks.splice(0, this.skidMarks.length - MAX_SKID_MARKS);
    }
  }

  /** Spawns a burst of tire-smoke/dust particles at (x, y), tinted by `color`. */
  private spawnParticles(x: number, y: number, count: number, color: string, spread: number, baseSpeed: number): void {
    for (let i = 0; i < count; i++) {
      const angle = Math.random() * Math.PI * 2;
      const speed = baseSpeed * (0.4 + Math.random() * 0.6);
      this.particles.push({
        x: x + (Math.random() - 0.5) * spread,
        y: y + (Math.random() - 0.5) * spread,
        vx: Math.cos(angle) * speed,
        vy: Math.sin(angle) * speed,
        size: 4 + Math.random() * 6,
        color,
        life: 0,
        maxLife: 0.5 + Math.random() * 0.4,
      });
    }

    if (this.particles.length > MAX_PARTICLES) {
      this.particles.splice(0, this.particles.length - MAX_PARTICLES);
    }
  }

  private updateParticles(dt: number): void {
    for (const mark of this.skidMarks) {
      mark.alpha = Math.max(0, mark.alpha - dt * 0.06); // slow fade over several seconds
    }
    this.skidMarks = this.skidMarks.filter((m) => m.alpha > 0.01);

    for (const p of this.particles) {
      p.life += dt;
      p.x += p.vx * dt;
      p.y += p.vy * dt;
      p.vx *= 0.9;
      p.vy *= 0.9;
    }
    this.particles = this.particles.filter((p) => p.life < p.maxLife);
  }

  private startRace(): void {
    this.raceStarted = true;
    this.controlsEnabled = true;
    this.countdown = null;
    this.elapsedMs = 0;
    this.currentLap = 1;
    this.raceStartMs = performance.now();
    this.lapStartMs = this.raceStartMs;
    this.resetCarToStart();
  }

  private tick(timestampMs: number): void {
    if (this.lastFrameTime == null) {
      this.lastFrameTime = timestampMs;
    }

    const dt = Math.min(0.05, (timestampMs - this.lastFrameTime) / 1000); // clamp to avoid huge steps on tab switch
    this.lastFrameTime = timestampMs;

    if (this.raceStarted && !this.raceFinished) {
      this.elapsedMs = performance.now() - this.raceStartMs;
      this.updatePhysics(dt);
      this.checkCheckpointCrossing();
      this.checkCarCollisions(timestampMs);
      this.maybeReportProgress(timestampMs);
    }

    this.updateCamera(dt);
    this.updateParticles(dt);
    this.render();

    this.animationFrameId = requestAnimationFrame((t) => this.tick(t));
  }

  private updatePhysics(dt: number): void {
    if (!this.controlsEnabled) return;

    const { maxSpeed, acceleration, braking, turnRate } = this.physics;

    const throttle = this.isPressed('arrowup', 'w');
    const brake = this.isPressed('arrowdown', 's');
    const left = this.isPressed('arrowleft', 'a');
    const right = this.isPressed('arrowright', 'd');
    const wantsNitro = this.isPressed('shift');

    this.isBraking = brake;

    // Standalone boost: Shift alone gives a forward push even with no
    // throttle held, not just a multiplier on top of W/ArrowUp. Requiring
    // both keys together made nitro easy to miss entirely — holding Shift
    // by itself is the more discoverable, obviously-doing-something version.
    this.boosting = wantsNitro && this.nitroMeter > 0;

    if (this.boosting) {
      this.nitroMeter = Math.max(0, this.nitroMeter - NITRO_DRAIN_PER_SEC * dt);
    } else {
      this.nitroMeter = Math.min(this.physics.nitroCapacity, this.nitroMeter + NITRO_REGEN_PER_SEC * dt);
    }
    this.nitroPercent = Math.round((this.nitroMeter / this.physics.nitroCapacity) * 100);

    const effectiveAcceleration = this.boosting ? acceleration * NITRO_ACCEL_MULTIPLIER : acceleration;
    const effectiveMaxSpeed = this.boosting ? maxSpeed * NITRO_MAX_SPEED_MULTIPLIER : maxSpeed;

    if (throttle || this.boosting) {
      this.carSpeed += effectiveAcceleration * dt;
    } else if (brake) {
      this.carSpeed -= braking * dt;
    } else {
      // Engine braking / rolling friction pulls speed back toward zero.
      const decay = FRICTION * dt;
      if (this.carSpeed > 0) {
        this.carSpeed = Math.max(0, this.carSpeed - decay);
      } else if (this.carSpeed < 0) {
        this.carSpeed = Math.min(0, this.carSpeed + decay);
      }
    }

    this.carSpeed = Math.max(-maxSpeed * 0.5, Math.min(effectiveMaxSpeed, this.carSpeed));

    // Steering scales with how fast you're going — no pivoting in place,
    // matches how the physics profile is tuned (turnRate is a *rate*, not
    // a fixed increment).
    const speedFraction = Math.min(1, Math.abs(this.carSpeed) / maxSpeed);
    const turnDirection = (left ? -1 : 0) + (right ? 1 : 0);
    const reverseFactor = this.carSpeed < 0 ? -1 : 1;

    this.carAngle += turnDirection * turnRate * speedFraction * reverseFactor * dt;

    const prevX = this.carX;
    const prevY = this.carY;

    this.carX += Math.cos(this.carAngle) * this.carSpeed * dt;
    this.carY += Math.sin(this.carAngle) * this.carSpeed * dt;
    // hard from a decent speed — the two situations where tires actually
    // scrub in an arcade sense, even without full lateral-slip physics.
    const corneringHard = Math.abs(turnDirection) > 0 && speedFraction > 0.55;
    const brakingHard = brake && Math.abs(this.carSpeed) > maxSpeed * 0.35;
    if (corneringHard || brakingHard) {
      this.spawnSkidMark(performance.now());

      if (Math.random() < 0.5) {
        const rearX = this.carX - Math.cos(this.carAngle) * 20;
        const rearY = this.carY - Math.sin(this.carAngle) * 20;
        this.spawnParticles(rearX, rearY, 1, 'rgba(230,230,230,0.55)', 6, 18);
      }
    }

    // Soft arena bounds so the car can't drive off into infinity.
    const margin = 40;
    this.carX = Math.max(margin, Math.min(CANVAS_WIDTH - margin, this.carX));
    this.carY = Math.max(margin, Math.min(CANVAS_HEIGHT - margin, this.carY));

    this.applyFenceCollision(prevX, prevY);
  }

  /**
   * Keeps the car from crossing the track's fence, two ways:
   *
   * 1. Swept check (the actual corner-cutting fix): tests the car's full
   *    movement PATH this frame — from its pre-movement position to its
   *    post-movement position — against every segment of the real,
   *    precomputed fence polylines (see computeFencePolylines()). A fast
   *    car cutting a tight corner moves several dozen px in one frame;
   *    the old approach only ever checked the car's final resting spot
   *    against an approximated "nearest centerline segment" boundary,
   *    which could pick the wrong segment near a sharp bend and let the
   *    whole frame's movement land clean on the other side of the fence
   *    with nothing in between ever having been tested. Checking the path
   *    itself, against the same lines that get drawn, closes that gap.
   *
   * 2. Resting-boundary fallback (kept from the original approach): if
   *    the swept check finds no crossing this frame (the car didn't pass
   *    through the fence, just ended up sitting at/near it), fall back to
   *    "is the car's current position further from the centerline than
   *    the road's half-width" — this is what keeps a car that's slowly
   *    grinding along the fence pinned at the boundary instead of only
   *    reacting on the specific frame it first touches.
   *
   * Either path resolves the same way: push the car back to the contact
   * point and scrub most of its speed — a "hit the fence" bounce, not a
   * hard wall stop.
   */
  private applyFenceCollision(prevX: number, prevY: number): void {
    if (this.checkpoints.length < 2) return;

    const sweptHit = this.findFenceCrossing(prevX, prevY, this.carX, this.carY);

    if (sweptHit) {
      // Land just behind the crossing point, along the reverse of travel,
      // so the car doesn't end up sitting exactly ON the line (which the
      // resting-boundary check next frame could then read as still
      // outside, causing a jitter loop right at the fence).
      const travelX = this.carX - prevX;
      const travelY = this.carY - prevY;
      const travelLen = Math.hypot(travelX, travelY) || 1;

      this.carX = sweptHit.x - (travelX / travelLen) * 3;
      this.carY = sweptHit.y - (travelY / travelLen) * 3;

      this.onFenceImpact(sweptHit.x, sweptHit.y);
      return;
    }

    // Resting-boundary fallback: same nearest-centerline-segment distance
    // check as before, for the steady-state "leaning on the fence" case
    // the swept check above doesn't cover (no crossing occurred because
    // the car was already past the boundary before this frame started,
    // or is moving along the fence rather than into it).
    const len = this.checkpoints.length;
    let minDistance = Infinity;
    let closestX = this.carX;
    let closestY = this.carY;
    let halfWidth = 75;

    for (let i = 0; i < len; i++) {
      const a = this.checkpoints[i];
      const b = this.checkpoints[(i + 1) % len];

      const ax = Number(a.positionX);
      const ay = Number(a.positionY);
      const bx = Number(b.positionX);
      const by = Number(b.positionY);

      const abx = bx - ax;
      const aby = by - ay;
      const abLenSq = abx * abx + aby * aby;
      if (abLenSq === 0) continue;

      const t = Math.max(0, Math.min(1, ((this.carX - ax) * abx + (this.carY - ay) * aby) / abLenSq));
      const px = ax + abx * t;
      const py = ay + aby * t;
      const dist = Math.hypot(this.carX - px, this.carY - py);

      if (dist < minDistance) {
        minDistance = dist;
        closestX = px;
        closestY = py;
        halfWidth = (Number(a.width) + Number(b.width)) / 4 + FENCE_MARGIN; // avg of both, /2 for half-width, already /2 baked via /4
      }
    }

    if (minDistance > halfWidth) {
      const dx = this.carX - closestX;
      const dy = this.carY - closestY;
      const length = Math.hypot(dx, dy) || 1;

      this.carX = closestX + (dx / length) * halfWidth;
      this.carY = closestY + (dy / length) * halfWidth;

      this.onFenceImpact(closestX, closestY);
    }
  }

  /** Runs the car's movement segment against both fence polylines, returning the nearest crossing point (if any). */
  private findFenceCrossing(
    prevX: number, prevY: number, newX: number, newY: number,
  ): { x: number; y: number } | null {
    const start = { x: prevX, y: prevY };
    const end = { x: newX, y: newY };

    let hit: { x: number; y: number } | null = null;
    let hitDistance = Infinity;

    for (const polyline of [this.innerFencePoints, this.outerFencePoints]) {
      const len = polyline.length;
      if (len < 2) continue;

      for (let i = 0; i < len; i++) {
        const a = polyline[i];
        const b = polyline[(i + 1) % len];
        const point = segmentIntersection(start, end, a, b);

        if (point) {
          const dist = Math.hypot(point.x - start.x, point.y - start.y);
          if (dist < hitDistance) {
            hitDistance = dist;
            hit = point;
          }
        }
      }
    }

    return hit;
  }

  /** Shared impact reaction for both fence-collision paths above: speed scrub + an impact dust puff scaled to how hard the hit was. */
  private onFenceImpact(atX: number, atY: number): void {
    if (Math.abs(this.carSpeed) > 60) {
      const impactFraction = Math.min(1, Math.abs(this.carSpeed) / this.physics.maxSpeed);
      this.spawnParticles(atX, atY, Math.round(3 + impactFraction * 6), 'rgba(200,190,170,0.6)', 14, 90 * impactFraction);
    }

    this.carSpeed *= BOUNCE_DAMPING;
  }

  /**
   * Contact check between the player's car and every opponent's current
   * render position, using each car's actual length/width (from
   * car-visuals) as an axis-aligned box in the *player's* local frame
   * (forward = local X, sideways = local Y) — tighter side-to-side than
   * front-to-back, matching a real car's proportions, instead of a single
   * circle that couldn't tell "beside" from "touching." Only the player's
   * side is actually physically resolved (pushed apart + speed cut) since
   * opponents are network-telemetry-driven, not locally simulated on this
   * client — but every other player's own client independently runs this
   * exact same check against their own locally-simulated car, so from
   * each player's own screen a crash still looks and feels mutual.
   */
  private checkCarCollisions(timestampMs: number): void {
    const playerHalfLength = (this.playerCarShape.length / 2) * CRASH_BOX_SHRINK * CAR_RENDER_SCALE;
    const playerHalfWidth = (this.playerCarShape.width / 2) * CRASH_BOX_SHRINK * CAR_RENDER_SCALE;

    for (const opponent of this.opponents) {
      if (opponent.finished) continue;

      const { x, y } = this.getOpponentRenderPosition(opponent);
      const dx = this.carX - x;
      const dy = this.carY - y;
      const distance = Math.hypot(dx, dy);

      if (distance > CRASH_CHECK_MAX_DISTANCE) continue; // cheap early-out before the shape lookup below

      const opponentShape = getCarShape(opponent.carName);
      const combinedHalfLength = playerHalfLength + (opponentShape.length / 2) * CRASH_BOX_SHRINK * CAR_RENDER_SCALE;
      const combinedHalfWidth = playerHalfWidth + (opponentShape.width / 2) * CRASH_BOX_SHRINK * CAR_RENDER_SCALE;

      // Rotate the center-to-center delta into the player's local frame.
      const cos = Math.cos(this.carAngle);
      const sin = Math.sin(this.carAngle);
      const localX = dx * cos + dy * sin; // + = opponent is behind the player
      const localY = -dx * sin + dy * cos; // + = opponent is to the player's left

      if (Math.abs(localX) >= combinedHalfLength || Math.abs(localY) >= combinedHalfWidth) {
        continue; // outside the box on at least one axis — not actually touching
      }

      // Push out along whichever axis has the smaller penetration — the
      // more natural resolution when a hit is mostly-front/rear or
      // mostly-side rather than a dead-center overlap.
      const overlapX = combinedHalfLength - Math.abs(localX);
      const overlapY = combinedHalfWidth - Math.abs(localY);
      const length = distance || 1;
      const nx = dx / length;
      const ny = dy / length;
      const pushDistance = Math.min(overlapX, overlapY);

      this.carX += nx * pushDistance;
      this.carY += ny * pushDistance;
      this.carSpeed *= CRASH_BOUNCE_DAMPING;
      this.carX += nx * (CRASH_PUSH_APART_SPEED / 60);
      this.carY += ny * (CRASH_PUSH_APART_SPEED / 60);

      const lastEffect = this.lastCrashEffectAt.get(opponent.userId) ?? 0;
      if (timestampMs - lastEffect > CRASH_EFFECT_COOLDOWN_MS) {
        this.lastCrashEffectAt.set(opponent.userId, timestampMs);

        const impactX = (this.carX + x) / 2;
        const impactY = (this.carY + y) / 2;
        this.spawnParticles(impactX, impactY, 14, '#ffe066', 10, 150);
        this.spawnParticles(impactX, impactY, 8, '#ff5a3d', 8, 110);
        this.spawnParticles(impactX, impactY, 6, '#ffffff', 6, 90);

        this.crashShakeUntilMs = timestampMs + CRASH_SHAKE_MS;
        this.crashShakeSeed = Math.random() * 1000;
      }
    }
  }

  /**
   * Advances nextCheckpoint when the car passes the target checkpoint.
   *
   * Two detection paths, because a circle alone silently breaks laps on
   * sharp-cornered tracks: the circle (radius = checkpoint width / 2, plus
   * a little forgiveness) only catches cars that come reasonably close to
   * the checkpoint's center. On a tight corner apex a car holding the
   * outside line is up to width/2 + FENCE_MARGIN from the *centerline* —
   * already past the circle's reach before the corner even curls — so the
   * checkpoint was never registered and the lap never completed, no matter
   * how many times the finish line was crossed (the "10 laps, never
   * counted 4" bug).
   *
   * The fallback is projection-based: project the car onto the segment
   * LEADING INTO the target checkpoint. Once that projection passes the
   * checkpoint (t > 1) while the car is still within a road's width of the
   * segment line, the checkpoint counts — matching the intuitive rule "I
   * drove past it on the road", not "I happened to drive through one
   * specific disc".
   */
  private checkCheckpointCrossing(): void {
    const len = this.checkpoints.length;
    const target = this.checkpoints[this.nextCheckpoint];
    if (!target || len < 2) return;

    const targetX = Number(target.positionX);
    const targetY = Number(target.positionY);
    const distance = Math.hypot(targetX - this.carX, targetY - this.carY);

    if (distance <= Number(target.width) / 2 + 30) {
      this.advanceCheckpoint();
      return;
    }

    const prev = this.checkpoints[(this.nextCheckpoint - 1 + len) % len];
    const ax = Number(prev.positionX);
    const ay = Number(prev.positionY);
    const abx = targetX - ax;
    const aby = targetY - ay;
    const abLenSq = abx * abx + aby * aby;
    if (abLenSq === 0) return;

    const t = ((this.carX - ax) * abx + (this.carY - ay) * aby) / abLenSq;

    if (t > 1 && t < 2.5) {
      // Lateral distance from the (infinite) segment line at the car's
      // position — clamping t for the reference point keeps the math local
      // to the checkpoint end of the segment.
      const refX = ax + abx * Math.min(t, 1.25);
      const refY = ay + aby * Math.min(t, 1.25);
      const lateral = Math.hypot(this.carX - refX, this.carY - refY);

      if (lateral <= Number(target.width) * 0.9) {
        this.advanceCheckpoint();
      }
    }
  }

  private advanceCheckpoint(): void {
    const wasFinishLine = this.nextCheckpoint === 0;
    this.nextCheckpoint = (this.nextCheckpoint + 1) % this.checkpoints.length;

    if (wasFinishLine) {
      this.completeLap();
    }
  }

  private completeLap(): void {
    const lapTimeMs = performance.now() - this.lapStartMs;
    this.lapStartMs = performance.now();

    if (this.bestLapMs == null || lapTimeMs < this.bestLapMs) {
      this.bestLapMs = lapTimeMs;
    }

    if (this.currentLap >= this.totalLaps) {
      this.finishRace();
      return;
    }

    void this.realtime.reportLapCompleted(this.raceId!, this.currentLap, Math.round(lapTimeMs));
    this.currentLap++;
    this.recalculatePositions();
  }

  private finishRace(): void {
    this.raceFinished = true;
    this.controlsEnabled = false;
    void this.realtime.reportFinished(this.raceId!, Math.round(this.elapsedMs));
  }

  private maybeReportProgress(timestampMs: number): void {
    if (timestampMs - this.lastProgressReportAt < PROGRESS_REPORT_INTERVAL_MS) {
      return;
    }
    this.lastProgressReportAt = timestampMs;

    const prevIndex = (this.nextCheckpoint - 1 + this.checkpoints.length) % this.checkpoints.length;
    const prev = this.checkpoints[prevIndex];
    const next = this.checkpoints[this.nextCheckpoint];

    const segmentProgress = segmentFraction(this.carX, this.carY, prev, next);

    void this.realtime.reportProgress(this.raceId!, this.currentLap, this.nextCheckpoint, segmentProgress);
  }

  private recalculatePositions(): void {
    const mine = { userId: this.authService.currentUser()?.userId ?? '', lap: this.currentLap, checkpoint: this.nextCheckpoint, finished: this.raceFinished };

    const all = [
      mine,
      ...this.opponents.map((o) => ({ userId: o.userId, lap: o.lap, checkpoint: o.checkpoint, finished: o.finished })),
    ];

    const sorted = all.sort((a, b) => {
      if (a.finished !== b.finished) return a.finished ? -1 : 1;
      if (a.lap !== b.lap) return b.lap - a.lap;
      return b.checkpoint - a.checkpoint;
    });

    const myIndex = sorted.findIndex((p) => p.userId === mine.userId);
    this.position = myIndex >= 0 ? myIndex + 1 : 1;
  }

  private stopLoop(): void {
    if (this.animationFrameId != null) {
      cancelAnimationFrame(this.animationFrameId);
      this.animationFrameId = null;
    }
  }

  private render(): void {
    const ctx = this.ctx;
    if (!ctx) return;

    ctx.save();

    // Chase-camera transform: place the camera position at screen center,
    // rotate so the car's heading points "up", scale so `viewWidth`
    // virtual units fill the canvas width. Order matters — canvas
    // transforms compose in the order applied, against points drawn
    // afterward, so this reads as "world point -> centered on camera ->
    // rotated -> scaled -> placed at screen center." World-space drawing
    // below (scenery/track/fences/cars/particles) is otherwise completely
    // unchanged from the old whole-map view; only this transform changed,
    // which is what makes the camera followable without needing to touch
    // every individual draw call.
    const scale = this.canvasPixelWidth / this.viewWidth;

    // Crash screen shake: a small random jitter applied to the camera's
    // screen-space anchor point, decaying linearly over CRASH_SHAKE_MS.
    // Applied before rotate/scale so it reads as the whole world shaking
    // relative to the screen, not the car shaking within the world.
    let shakeX = 0;
    let shakeY = 0;
    const shakeRemaining = this.crashShakeUntilMs - performance.now();
    if (shakeRemaining > 0) {
      const magnitude = CRASH_SHAKE_MAGNITUDE * (shakeRemaining / CRASH_SHAKE_MS);
      shakeX = (hash2(this.crashShakeSeed, performance.now() * 0.03) - 0.5) * 2 * magnitude;
      shakeY = (hash2(performance.now() * 0.03, this.crashShakeSeed) - 0.5) * 2 * magnitude;
    }

    ctx.translate(this.canvasPixelWidth / 2 + shakeX, this.canvasPixelHeight / 2 + shakeY);
    ctx.rotate(this.cameraAngle);
    ctx.scale(scale, scale);
    ctx.translate(-this.cameraX, -this.cameraY);

    ctx.fillStyle = this.theme.background;
    ctx.fillRect(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);

    this.drawHorizon(ctx);
    this.drawScenery(ctx);
    this.drawTrack(ctx);
    this.drawCurbs(ctx);
    this.drawTrackside(ctx);
    this.drawSkidMarks(ctx);
    this.drawFences(ctx);
    this.drawHeadlightCones(ctx);
    this.drawOpponents(ctx);
    this.drawPlayerCar(ctx);
    this.drawParticles(ctx);

    ctx.restore();

    // Screen-space overlays, drawn after restoring out of the camera
    // transform so they're unaffected by its rotation/zoom: ambient
    // day/dusk tint and speed lines. (The "YOU" label lives in drawCar,
    // in world space above the car itself — it tracks the car exactly,
    // which the old fixed-screen-center copy stopped doing once the
    // camera gained its look-ahead lead.)
    this.drawAmbientTint(ctx);
    this.drawSpeedLines(ctx);
  }

  /** Full-screen tint matching the track's ambient darkness (see THEME_PALETTES) — subtle for daylight themes, a real dusk feel for city. */
  private drawAmbientTint(ctx: CanvasRenderingContext2D): void {
    if (this.theme.ambientDarkness <= 0) return;

    ctx.save();
    ctx.fillStyle = `rgba(10, 12, 25, ${this.theme.ambientDarkness * 0.4})`;
    ctx.fillRect(0, 0, this.canvasPixelWidth, this.canvasPixelHeight);
    ctx.restore();
  }


  /**
   * Radiating lines from screen edges toward center at speed — a classic
   * cheap "sense of speed" technique, screen-space so it reads as a
   * camera/motion effect rather than something physically in the world.
   * Fades in only once genuinely fast (60%+ of top speed) and scales with
   * how far over that threshold you are.
   */
  private drawSpeedLines(ctx: CanvasRenderingContext2D): void {
    const speedFraction = Math.min(1, Math.abs(this.carSpeed) / this.physics.maxSpeed);
    if (speedFraction < 0.6 || !this.raceStarted) return;

    const intensity = (speedFraction - 0.6) / 0.4; // 0-1 over the 60%-100% range
    const cx = this.canvasPixelWidth / 2;
    const cy = this.canvasPixelHeight / 2;
    const count = 14;

    ctx.save();
    ctx.strokeStyle = `rgba(255,255,255,${0.05 + intensity * 0.12})`;
    ctx.lineWidth = 2;

    for (let i = 0; i < count; i++) {
      const angle = (i / count) * Math.PI * 2 + this.lastFrameTime! * 0.0002;
      const innerR = Math.max(cx, cy) * 0.55;
      const outerR = Math.max(cx, cy) * (0.75 + intensity * 0.3);

      ctx.beginPath();
      ctx.moveTo(cx + Math.cos(angle) * innerR, cy + Math.sin(angle) * innerR);
      ctx.lineTo(cx + Math.cos(angle) * outerR, cy + Math.sin(angle) * outerR);
      ctx.stroke();
    }

    ctx.restore();
  }

  /** Faded dark streaks left behind by hard cornering/braking — drawn under the fences/cars, above the road surface. */
  private drawSkidMarks(ctx: CanvasRenderingContext2D): void {
    if (this.skidMarks.length === 0) return;

    ctx.save();
    for (const mark of this.skidMarks) {
      ctx.save();
      ctx.translate(mark.x, mark.y);
      ctx.rotate(mark.angle);
      ctx.fillStyle = `rgba(20,20,20,${mark.alpha})`;
      ctx.beginPath();
      ctx.ellipse(0, -6, 5, 2.5, 0, 0, Math.PI * 2);
      ctx.ellipse(0, 6, 5, 2.5, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
    }
    ctx.restore();
  }

  /** Tire-smoke/dust/impact puffs — drawn last so they sit above cars and everything else. */
  private drawParticles(ctx: CanvasRenderingContext2D): void {
    if (this.particles.length === 0) return;

    ctx.save();
    for (const p of this.particles) {
      const lifeFraction = p.life / p.maxLife;
      const alpha = 1 - lifeFraction;
      const size = p.size * (1 + lifeFraction * 1.5); // particles grow as they fade, like smoke dispersing

      ctx.globalAlpha = Math.max(0, alpha);
      ctx.fillStyle = p.color;
      ctx.beginPath();
      ctx.arc(p.x, p.y, size, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.restore();
  }

  /**
   * Soft directional glow in front of each car. Always at least faintly
   * visible (real headlights read even in daylight), much more prominent
   * on darker-ambience themes (see THEME_PALETTES.ambientDarkness) where
   * it's doing real illumination work rather than just a cosmetic dot.
   * Drawn before the cars themselves so the cone sits under/behind them.
   */
  private drawHeadlightCones(ctx: CanvasRenderingContext2D): void {
    const intensity = 0.12 + this.theme.ambientDarkness * 0.55;
    const drawCone = (x: number, y: number, angle: number, shape: CarShapeProfile) => {
      ctx.save();
      ctx.translate(x, y);
      ctx.rotate(angle);

      const coneLength = shape.length * CAR_RENDER_SCALE * 2.6;
      const gradient = ctx.createRadialGradient(shape.length / 2, 0, 2, shape.length / 2, 0, coneLength);
      gradient.addColorStop(0, `rgba(255,250,210,${intensity})`);
      gradient.addColorStop(1, 'rgba(255,250,210,0)');

      ctx.fillStyle = gradient;
      ctx.beginPath();
      ctx.moveTo(shape.length / 2, -shape.width * 0.35);
      ctx.lineTo(shape.length / 2 + coneLength, -shape.width * CAR_RENDER_SCALE * 1.6);
      ctx.lineTo(shape.length / 2 + coneLength, shape.width * CAR_RENDER_SCALE * 1.6);
      ctx.lineTo(shape.length / 2, shape.width * 0.35);
      ctx.closePath();
      ctx.fill();

      ctx.restore();
    };

    drawCone(this.carX, this.carY, this.carAngle, this.playerCarShape);

    for (const opponent of this.opponents) {
      if (this.checkpoints.length < 2) continue;
      const { x, y, angle } = this.getOpponentRenderPosition(opponent);
      drawCone(x, y, angle, getCarShape(opponent.carName));
    }
  }

  /**
   * Trackside furniture that fills the dead space beside the road:
   *   - Pit lane: a parallel service road on the inside of the start
   *     straight with marked pit boxes — real circuits have one, and it
   *     occupies the strip of land right where the infield void starts.
   *   - Grandstands: tiered crowd blocks on the OUTSIDE of selected
   *     checkpoints (skipped automatically where the stand would collide
   *     with another section of track on a tight loop, or fall off-canvas).
   *   - Tire barriers: stacked tire rings at sharp corner apexes, on the
   *     outside of the bend where runoff actually ends.
   *   - Start gantry: an overhead truss spanning the road at the finish
   *     line, with pylons, a beam, and a row of start lights.
   * Everything is derived deterministically from checkpoint geometry — no
   * per-frame state, and every client draws the identical scene.
   */
  private drawTrackside(ctx: CanvasRenderingContext2D): void {
    const len = this.checkpoints.length;
    if (len < 4) return;

    const centroidX = this.checkpoints.reduce((s, c) => s + Number(c.positionX), 0) / len;
    const centroidY = this.checkpoints.reduce((s, c) => s + Number(c.positionY), 0) / len;

    // Unit direction at checkpoint i (averaged in/out like the fences).
    const directionAt = (i: number) => {
      const prev = this.checkpoints[(i - 1 + len) % len];
      const next = this.checkpoints[(i + 1) % len];
      const dx = Number(next.positionX) - Number(prev.positionX);
      const dy = Number(next.positionY) - Number(prev.positionY);
      const d = Math.hypot(dx, dy) || 1;
      return { x: dx / d, y: dy / d };
    };

    // Unit normal pointing from centroid → checkpoint (i.e. "outward").
    const outwardAt = (i: number) => {
      const cp = this.checkpoints[i];
      const dx = Number(cp.positionX) - centroidX;
      const dy = Number(cp.positionY) - centroidY;
      const d = Math.hypot(dx, dy) || 1;
      return { x: dx / d, y: dy / d };
    };

    // ---- Pit lane: inside of the start straight ----
    {
      // Inside = toward the centroid, opposite of outward.
      const stallCount = Math.min(6, Math.max(3, Math.floor(len / 6)));
      const pitHalf = 26;

      const pitPoint = (i: number, extra: number) => {
        const cp = this.checkpoints[i];
        const out = outwardAt(i);
        const off = Number(cp.width) / 2 + 55 + extra;
        return {
          x: Number(cp.positionX) - out.x * off,
          y: Number(cp.positionY) - out.y * off,
        };
      };

      ctx.save();
      ctx.lineCap = 'round';
      ctx.lineJoin = 'round';

      // Entry/exit blend arcs + the lane itself as one stroked path from a
      // little before the line to a little after the last stall.
      ctx.strokeStyle = '#33373d';
      ctx.lineWidth = pitHalf * 2;
      ctx.beginPath();
      const startPt = pitPoint(0, -30);
      const endPt = pitPoint(stallCount, -30);
      ctx.moveTo(startPt.x, startPt.y);
      for (let i = 0; i <= stallCount; i++) {
        const p = pitPoint(i, 0);
        ctx.lineTo(p.x, p.y);
      }
      ctx.lineTo(endPt.x, endPt.y);
      ctx.stroke();

      // Pit-box markings: white angle brackets along the lane.
      ctx.strokeStyle = 'rgba(255,255,255,0.5)';
      ctx.lineWidth = 2.5;
      for (let i = 1; i < stallCount; i++) {
        const p = pitPoint(i, 0);
        const dir = directionAt(i);
        const nx = -dir.y;
        const ny = dir.x;
        ctx.beginPath();
        ctx.moveTo(p.x - dir.x * 16 + nx * pitHalf * 0.7, p.y - dir.y * 16 + ny * pitHalf * 0.7);
        ctx.lineTo(p.x - dir.x * 16, p.y - dir.y * 16);
        ctx.lineTo(p.x - dir.x * 16 - nx * pitHalf * 0.7, p.y - dir.y * 16 - ny * pitHalf * 0.7);
        ctx.stroke();
      }

      // Pit wall between the lane and the main straight.
      ctx.strokeStyle = 'rgba(230,230,230,0.55)';
      ctx.lineWidth = 4;
      ctx.beginPath();
      const wallStart = pitPoint(0, 0);
      const wallEnd = pitPoint(stallCount, 0);
      ctx.moveTo(wallStart.x, wallStart.y);
      for (let i = 1; i <= stallCount; i++) {
        const p = pitPoint(i, 0);
        ctx.lineTo(p.x, p.y);
      }
      ctx.lineTo(wallEnd.x, wallEnd.y);
      ctx.stroke();

      ctx.restore();
    }

    // ---- Grandstands on the outside of selected checkpoints ----
    {
      const step = Math.max(4, Math.floor(len / 6));
      for (let i = 0; i < len; i += step) {
        const cp = this.checkpoints[i];
        const out = outwardAt(i);
        const dir = directionAt(i);
        const halfWidth = Number(cp.width) / 2;
        const standDist = halfWidth + 70;
        const cxp = Number(cp.positionX) + out.x * standDist;
        const cyp = Number(cp.positionY) + out.y * standDist;

        // Skip stands that would sit on another section of road (tight
        // loops pass near themselves) or off the canvas.
        if (this.distanceToTrackCenterline(cxp, cyp) < halfWidth + 55) continue;
        if (cxp < 80 || cxp > CANVAS_WIDTH - 80 || cyp < 70 || cyp > CANVAS_HEIGHT - 70) continue;

        const angle = Math.atan2(dir.y, dir.x);
        const standW = 150;
        const standH = 44;

        ctx.save();
        ctx.translate(cxp, cyp);
        ctx.rotate(angle);

        // Tiered deck (three stacked strips, darker toward the back) with
        // a crowd of colored dots, plus a roof strip.
        for (let tier = 0; tier < 3; tier++) {
          const t = tier / 2;
          ctx.fillStyle = tier === 0 ? '#3a3f4a' : tier === 1 ? '#31353f' : '#292c34';
          ctx.fillRect(-standW / 2, -standH / 2 + tier * (standH / 3), standW, standH / 3 - 1);
          for (let c = 0; c < 22; c++) {
            const rx = (hash2(i * 31 + c, tier * 7.3) - 0.5) * standW * 0.92;
            const ry = -standH / 2 + tier * (standH / 3) + 4 + hash2(c, i + tier) * (standH / 3 - 8);
            ctx.fillStyle = `hsl(${Math.floor(hash2(c + i, tier * 3.7) * 360)}, 55%, 55%)`;
            ctx.beginPath();
            ctx.arc(rx, ry, 2.2, 0, Math.PI * 2);
            ctx.fill();
          }
        }
        ctx.fillStyle = '#1d2027';
        ctx.fillRect(-standW / 2, -standH / 2 - 6, standW, 5);

        ctx.restore();
      }
    }

    // ---- Tire barriers at sharp corner apexes ----
    {
      for (let i = 0; i < len; i++) {
        const dirIn = directionAt((i - 1 + len) % len);
        const dirOut = directionAt(i);
        const turn = Math.abs(Math.atan2(dirOut.x * dirIn.y - dirOut.y * dirIn.x, dirOut.x * dirIn.x + dirOut.y * dirIn.y));
        if (turn < 0.35) continue; // not a corner

        const cp = this.checkpoints[i];
        const out = outwardAt(i);
        const dir = dirOut;
        const nx = -dir.y;
        const ny = dir.x;
        const baseDist = Number(cp.width) / 2 + 32;

        // 5 tires fanned along the outside of the apex.
        for (let t = -2; t <= 2; t++) {
          const tx = Number(cp.positionX) + out.x * baseDist + nx * t * 17;
          const ty = Number(cp.positionY) + out.y * baseDist + ny * t * 17;
          if (this.distanceToTrackCenterline(tx, ty) < Number(cp.width) / 2 + 20) continue;

          ctx.fillStyle = '#14161a';
          ctx.beginPath();
          ctx.arc(tx, ty, 7, 0, Math.PI * 2);
          ctx.fill();
          ctx.strokeStyle = t % 2 === 0 ? '#e8e8e8' : '#d1343b';
          ctx.lineWidth = 2.4;
          ctx.stroke();
          ctx.fillStyle = 'rgba(255,255,255,0.12)';
          ctx.beginPath();
          ctx.arc(tx - 2, ty - 2, 2.5, 0, Math.PI * 2);
          ctx.fill();
        }
      }
    }

    // ---- Start gantry over the finish line ----
    {
      const start = this.checkpoints[0];
      const next = this.checkpoints[1] ?? this.checkpoints[0];
      const dirX = Number(next.positionX) - Number(start.positionX);
      const dirY = Number(next.positionY) - Number(start.positionY);
      const dirLen = Math.hypot(dirX, dirY) || 1;
      const ux = dirX / dirLen;
      const uy = dirY / dirLen;
      const nx = -uy;
      const ny = ux;
      const halfWidth = Number(start.width) / 2;
      const sx = Number(start.positionX);
      const sy = Number(start.positionY);

      ctx.save();
      ctx.translate(sx, sy);
      ctx.rotate(Math.atan2(uy, ux));

      // Pylons just outside the road edges.
      ctx.fillStyle = '#23262d';
      ctx.fillRect(-6, -halfWidth - 22, 12, 20);
      ctx.fillRect(-6, halfWidth + 2, 12, 20);
      ctx.fillStyle = 'rgba(255,255,255,0.12)';
      ctx.fillRect(-6, -halfWidth - 22, 12, 3);
      ctx.fillRect(-6, halfWidth + 2, 12, 3);

      // Beam spanning the road (with a soft drop shadow on the asphalt).
      ctx.fillStyle = 'rgba(0,0,0,0.3)';
      ctx.fillRect(2, -halfWidth - 4, 9, halfWidth * 2 + 8);
      ctx.fillStyle = '#2c3038';
      ctx.fillRect(-4, -halfWidth - 4, 9, halfWidth * 2 + 8);
      ctx.fillStyle = 'rgba(255,255,255,0.1)';
      ctx.fillRect(-4, -halfWidth - 4, 9, 2.5);

      // Five start lights on the beam.
      for (let i = 0; i < 5; i++) {
        const lx = 0.5;
        const ly = -halfWidth + 10 + (i / 4) * (halfWidth * 2 - 20);
        ctx.fillStyle = this.raceStarted ? '#2fd06a' : '#e8433d';
        ctx.beginPath();
        ctx.arc(lx, ly, 3.4, 0, Math.PI * 2);
        ctx.fill();
        ctx.fillStyle = 'rgba(0,0,0,0.4)';
        ctx.beginPath();
        ctx.arc(lx, ly, 3.4 + 1.6, Math.PI * 0.15, Math.PI * 0.85);
        ctx.fill();
      }

      ctx.restore();
    }
  }

  /** Ray-casting point-in-polygon test against the closed track loop (checkpoints as vertices). */
  private isInsideTrackLoop(px: number, py: number): boolean {
    let inside = false;
    const len = this.checkpoints.length;

    for (let i = 0, j = len - 1; i < len; j = i++) {
      const xi = Number(this.checkpoints[i].positionX);
      const yi = Number(this.checkpoints[i].positionY);
      const xj = Number(this.checkpoints[j].positionX);
      const yj = Number(this.checkpoints[j].positionY);

      const intersects = yi > py !== yj > py && px < ((xj - xi) * (py - yi)) / (yj - yi) + xi;
      if (intersects) inside = !inside;
    }

    return inside;
  }

  /** Shortest distance from (px, py) to the track centerline polyline — used to keep decorative scenery off the actual road. */
  private distanceToTrackCenterline(px: number, py: number): number {
    let minDistance = Infinity;
    const len = this.checkpoints.length;

    for (let i = 0; i < len; i++) {
      const a = this.checkpoints[i];
      const b = this.checkpoints[(i + 1) % len];

      const ax = Number(a.positionX);
      const ay = Number(a.positionY);
      const bx = Number(b.positionX);
      const by = Number(b.positionY);

      const abx = bx - ax;
      const aby = by - ay;
      const abLenSq = abx * abx + aby * aby;
      if (abLenSq === 0) continue;

      const t = Math.max(0, Math.min(1, ((px - ax) * abx + (py - ay) * aby) / abLenSq));
      const cx = ax + abx * t;
      const cy = ay + aby * t;
      const distance = Math.hypot(px - cx, py - cy);

      if (distance < minDistance) minDistance = distance;
    }

    return minDistance;
  }

  /**
   * Themed centerpiece placed at the track loop's centroid — a lagoon for
   * beach/desert tracks, a grove for forest, a grandstand + scoreboard
   * pylon for grass/city — so the (often large) space enclosed by the
   * track reads as landscaped grounds instead of empty canvas. Sized to
   * the actual clearance at that specific track's centroid rather than a
   * fixed size, so it never overlaps the road on a tighter/twistier loop.
   */
  private drawInfieldLandmark(ctx: CanvasRenderingContext2D): void {
    if (this.checkpoints.length < 3) return;

    const cx = this.checkpoints.reduce((sum, c) => sum + Number(c.positionX), 0) / this.checkpoints.length;
    const cy = this.checkpoints.reduce((sum, c) => sum + Number(c.positionY), 0) / this.checkpoints.length;

    if (!this.isInsideTrackLoop(cx, cy)) return; // a very tight/twisty loop might not cleanly enclose its own centroid

    const clearance = this.distanceToTrackCenterline(cx, cy) - this.avgRoadWidth / 2;
    if (clearance < 90) return; // not enough infield room for anything but the sparse scatter dots

    const size = Math.max(50, Math.min(230, clearance - 50));
    const theme = themeFromTrackName(this.trackName);

    ctx.save();
    ctx.translate(cx, cy);

    if (theme === 'beach' || theme === 'desert') {
      // Lake/oasis: sandy shore ring, depth-gradient water, wave arcs,
      // palms along the shore. Previously a single flat blue ellipse that
      // read as a blurry void at gameplay zoom.
      const sandW = size * 1.18;
      const sandH = size * 0.74;
      ctx.fillStyle = theme === 'beach' ? '#e6d191' : '#d9b87a';
      ctx.beginPath();
      ctx.ellipse(0, 0, sandW, sandH, 0, 0, Math.PI * 2);
      ctx.fill();

      ctx.fillStyle = 'rgba(0,0,0,0.15)';
      ctx.beginPath();
      ctx.ellipse(4, 5, size, size * 0.62, 0, 0, Math.PI * 2);
      ctx.fill();

      const water = ctx.createRadialGradient(-size * 0.2, -size * 0.15, size * 0.1, 0, 0, size);
      water.addColorStop(0, theme === 'beach' ? '#5fc2de' : '#4fa8b8');
      water.addColorStop(1, theme === 'beach' ? '#1f7fae' : '#2c6e7e');
      ctx.fillStyle = water;
      ctx.beginPath();
      ctx.ellipse(0, 0, size, size * 0.62, 0, 0, Math.PI * 2);
      ctx.fill();

      ctx.strokeStyle = 'rgba(255,255,255,0.4)';
      ctx.lineWidth = 2.5;
      for (let w = 0; w < 3; w++) {
        const wr = size * (0.3 + w * 0.22);
        ctx.beginPath();
        ctx.ellipse(0, 0, wr, wr * 0.62, 0, Math.PI * 0.15, Math.PI * 0.85);
        ctx.stroke();
      }

      // Palms dotted around the shore line.
      for (let p = 0; p < 6; p++) {
        const a = (p / 6) * Math.PI * 2 + 0.5;
        const px = Math.cos(a) * sandW * 0.92;
        const py = Math.sin(a) * sandH * 0.92;
        ctx.fillStyle = 'rgba(0,0,0,0.2)';
        ctx.beginPath();
        ctx.ellipse(px + 4, py + 3, 10, 4.5, 0, 0, Math.PI * 2);
        ctx.fill();
        ctx.strokeStyle = '#7a5a3a';
        ctx.lineWidth = 2.2;
        ctx.beginPath();
        ctx.moveTo(px, py);
        ctx.quadraticCurveTo(px + 4, py - 10, px + 2, py - 19);
        ctx.stroke();
        ctx.strokeStyle = '#1e6b3a';
        ctx.lineWidth = 2.6;
        for (let f = 0; f < 5; f++) {
          const fa = (f / 5) * Math.PI * 2;
          ctx.beginPath();
          ctx.moveTo(px + 2, py - 19);
          ctx.quadraticCurveTo(px + 2 + Math.cos(fa) * 10, py - 19 + Math.sin(fa) * 10 - 2, px + 2 + Math.cos(fa) * 15, py - 19 + Math.sin(fa) * 15 + 3);
          ctx.stroke();
        }
      }
    } else if (theme === 'forest') {
      for (let i = 0; i < 26; i++) {
        const a = hash2(i, 3.1) * Math.PI * 2;
        const r = hash2(i, 7.7) * size * 0.8;
        ctx.fillStyle = '#1f4a24';
        ctx.beginPath();
        ctx.arc(Math.cos(a) * r, Math.sin(a) * r, 14 + hash2(i, 9.3) * 10, 0, Math.PI * 2);
        ctx.fill();
      }
    } else {
      // Grandstand: tiered seating block with a crowd, facing the track, plus a scoreboard pylon.
      const standW = Math.min(size * 1.6, 260);
      const standH = Math.min(size * 0.7, 110);

      ctx.fillStyle = '#3a3f4a';
      ctx.fillRect(-standW / 2, -standH / 2, standW, standH);
      ctx.fillStyle = '#2a2e37';
      for (let row = 0; row < 4; row++) {
        ctx.fillRect(-standW / 2, -standH / 2 + row * (standH / 4), standW, 3);
      }

      for (let i = 0; i < 60; i++) {
        const rx = (hash2(i, 1.3) - 0.5) * standW * 0.92;
        const ry = (hash2(i, 5.9) - 0.5) * standH * 0.8;
        ctx.fillStyle = `hsl(${Math.floor(hash2(i, 8.4) * 360)}, 60%, 55%)`;
        ctx.beginPath();
        ctx.arc(rx, ry, 3, 0, Math.PI * 2);
        ctx.fill();
      }

      ctx.fillStyle = '#1c1e24';
      ctx.fillRect(-6, -standH / 2 - 40, 12, 40);
      ctx.fillStyle = '#0f1115';
      ctx.fillRect(-30, -standH / 2 - 60, 60, 24);
      ctx.strokeStyle = '#2fd06a';
      ctx.lineWidth = 2;
      ctx.strokeRect(-30, -standH / 2 - 60, 60, 24);
    }

    ctx.restore();
  }

  /**
   * Atmospheric backdrop: a broad ambient glow centered on the arena, plus
   * a jagged themed silhouette (hills / dunes / sea haze / skyline /
   * treeline) framing all four canvas edges — so the exterior reads as
   * "the world continues beyond here" instead of stopping at a hard,
   * empty canvas edge. Drawn once per frame, behind everything else
   * (scenery/track/cars), same as the old flat background fill it
   * replaces the plainness of.
   */
  private drawHorizon(ctx: CanvasRenderingContext2D): void {
    const theme = themeFromTrackName(this.trackName);

    // Vignette — darker toward the canvas corners instead of the old
    // centered accent-tinted glow. On beach especially, a big soft blue
    // radial blob in the middle read as a blurry "water void" next to the
    // road (see drawInfieldLandmark for the actual lake). A vignette adds
    // depth without fabricating empty terrain.
    const vignette = ctx.createRadialGradient(
      CANVAS_WIDTH / 2, CANVAS_HEIGHT / 2, Math.min(CANVAS_WIDTH, CANVAS_HEIGHT) * 0.38,
      CANVAS_WIDTH / 2, CANVAS_HEIGHT / 2, Math.max(CANVAS_WIDTH, CANVAS_HEIGHT) * 0.72,
    );
    vignette.addColorStop(0, 'rgba(0,0,0,0)');
    vignette.addColorStop(1, 'rgba(0,0,0,0.22)');
    ctx.save();
    ctx.fillStyle = vignette;
    ctx.fillRect(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);
    ctx.restore();

    const silhouetteColor: Record<TrackTheme, string> = {
      grass: '#0f2312',
      desert: '#7a5a35',
      beach: '#1c5f7a',
      city: '#14161b',
      forest: '#0a1c0c',
    };

    ctx.save();
    ctx.fillStyle = silhouetteColor[theme];

    const depth = 150;
    const teeth = 14;

    // Draws one jagged "distant skyline" edge: a strip of pseudo-random
    // peaks/valleys running along (dx, dy) from (x0, y0) for `length`,
    // extending inward by up to `depth` along the (nx, ny) normal.
    // seedOffset keeps each of the four edges' jaggedness independent so
    // they don't all mirror the same pattern.
    const jaggedEdge = (x0: number, y0: number, dx: number, dy: number, length: number, nx: number, ny: number, seedOffset: number) => {
      ctx.beginPath();
      ctx.moveTo(x0, y0);
      for (let i = 0; i <= teeth; i++) {
        const t = i / teeth;
        const wobble = depth * (0.35 + hash2(i + seedOffset, seedOffset * 3.1) * 0.65);
        ctx.lineTo(x0 + dx * length * t + nx * wobble, y0 + dy * length * t + ny * wobble);
      }
      ctx.lineTo(x0 + dx * length, y0 + dy * length);
      ctx.closePath();
      ctx.fill();
    };

    jaggedEdge(0, 0, 1, 0, CANVAS_WIDTH, 0, 1, 1); // top
    jaggedEdge(0, CANVAS_HEIGHT, 1, 0, CANVAS_WIDTH, 0, -1, 2); // bottom
    jaggedEdge(0, 0, 0, 1, CANVAS_HEIGHT, 1, 0, 3); // left
    jaggedEdge(CANVAS_WIDTH, 0, 0, 1, CANVAS_HEIGHT, -1, 0, 4); // right

    ctx.restore();
  }

  /** Themed mid-size scenery props scattered across the arena, behind the track. Denser inside the track loop than outside, never on the road. Each prop draws its own offset ground shadow — at gameplay zoom that shadow is what makes scenery read as objects standing in the world instead of painted on it. */
  private drawScenery(ctx: CanvasRenderingContext2D): void {
    const cell = 62;
    const exclusionRadius = this.avgRoadWidth / 2 + 55;
    const hasRealTrack = this.checkpoints.length >= 3;
    const theme = themeFromTrackName(this.trackName);

    /** Soft ellipse shadow under a prop, offset down-right like the cars'. */
    const propShadow = (px: number, py: number, rx: number, ry: number) => {
      ctx.fillStyle = 'rgba(0,0,0,0.22)';
      ctx.beginPath();
      ctx.ellipse(px + rx * 0.25, py + ry * 0.35, rx, ry, 0, 0, Math.PI * 2);
      ctx.fill();
    };

    /** Palm tree: curved trunk + 5 fronds. */
    const palm = (px: number, py: number, s: number) => {
      propShadow(px, py + 4, s * 0.9, s * 0.4);
      ctx.strokeStyle = '#7a5a3a';
      ctx.lineWidth = 2.4;
      ctx.beginPath();
      ctx.moveTo(px, py);
      ctx.quadraticCurveTo(px + s * 0.22, py - s * 0.55, px + s * 0.1, py - s);
      ctx.stroke();
      const cx = px + s * 0.1;
      const cy = py - s;
      ctx.strokeStyle = '#1e6b3a';
      ctx.lineWidth = 3;
      for (let f = 0; f < 5; f++) {
        const a = (f / 5) * Math.PI * 2 + 0.4;
        ctx.beginPath();
        ctx.moveTo(cx, cy);
        ctx.quadraticCurveTo(cx + Math.cos(a) * s * 0.55, cy + Math.sin(a) * s * 0.55 - 3, cx + Math.cos(a) * s * 0.8, cy + Math.sin(a) * s * 0.8 + 3);
        ctx.stroke();
      }
    };

    /** Round tree: shadow + dark canopy + lighter core. */
    const tree = (px: number, py: number, s: number, dark: string, light: string) => {
      propShadow(px, py + 3, s * 0.8, s * 0.35);
      ctx.fillStyle = dark;
      ctx.beginPath();
      ctx.arc(px, py, s, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = light;
      ctx.beginPath();
      ctx.arc(px - s * 0.25, py - s * 0.25, s * 0.55, 0, Math.PI * 2);
      ctx.fill();
    };

    /** Beach umbrella: pole + striped canopy. */
    const umbrella = (px: number, py: number, s: number, hue: number) => {
      propShadow(px, py + 2, s * 0.9, s * 0.3);
      ctx.strokeStyle = '#8a8f99';
      ctx.lineWidth = 1.6;
      ctx.beginPath();
      ctx.moveTo(px, py);
      ctx.lineTo(px, py - s);
      ctx.stroke();
      for (let seg = 0; seg < 4; seg++) {
        ctx.fillStyle = seg % 2 === 0 ? `hsl(${hue}, 70%, 55%)` : '#f5f2ea';
        ctx.beginPath();
        ctx.moveTo(px, py - s);
        ctx.arc(px, py - s, s, Math.PI + (seg / 4) * Math.PI, Math.PI + ((seg + 1) / 4) * Math.PI);
        ctx.closePath();
        ctx.fill();
      }
    };

    /** Cactus: trunk + two arms. */
    const cactus = (px: number, py: number, s: number) => {
      propShadow(px, py + 2, s * 0.5, s * 0.2);
      ctx.fillStyle = '#3e7d3a';
      ctx.beginPath();
      ctx.roundRect(px - s * 0.16, py - s, s * 0.32, s, s * 0.16);
      ctx.fill();
      ctx.beginPath();
      ctx.roundRect(px - s * 0.55, py - s * 0.75, s * 0.2, s * 0.45, s * 0.1);
      ctx.fill();
      ctx.beginPath();
      ctx.roundRect(px + s * 0.35, py - s * 0.62, s * 0.2, s * 0.38, s * 0.1);
      ctx.fill();
    };

    /** Rock: gray polygon with a lit face. */
    const rock = (px: number, py: number, s: number) => {
      propShadow(px, py + 2, s * 0.9, s * 0.35);
      ctx.fillStyle = '#8d8778';
      ctx.beginPath();
      ctx.moveTo(px - s, py + s * 0.4);
      ctx.lineTo(px - s * 0.4, py - s * 0.6);
      ctx.lineTo(px + s * 0.5, py - s * 0.5);
      ctx.lineTo(px + s, py + s * 0.4);
      ctx.closePath();
      ctx.fill();
      ctx.fillStyle = 'rgba(255,255,255,0.18)';
      ctx.beginPath();
      ctx.moveTo(px - s * 0.4, py - s * 0.6);
      ctx.lineTo(px + s * 0.5, py - s * 0.5);
      ctx.lineTo(px + s * 0.1, py + s * 0.1);
      ctx.closePath();
      ctx.fill();
    };

    /** City block: dark footprint + window grid. */
    const building = (px: number, py: number, s: number) => {
      propShadow(px, py + 3, s * 1.1, s * 0.5);
      ctx.fillStyle = '#20242c';
      ctx.fillRect(px - s, py - s * 0.7, s * 2, s * 1.4);
      ctx.fillStyle = 'rgba(255,220,120,0.75)';
      for (let row = 0; row < 3; row++) {
        for (let col = 0; col < 4; col++) {
          if (hash2(px + col, py + row) < 0.4) continue; // some windows dark
          ctx.fillRect(px - s * 0.8 + col * s * 0.45, py - s * 0.5 + row * s * 0.4, s * 0.22, s * 0.2);
        }
      }
    };

    for (let gx = 0; gx < CANVAS_WIDTH; gx += cell) {
      for (let gy = 0; gy < CANVAS_HEIGHT; gy += cell) {
        const r = hash2(gx, gy);
        const px = gx + r * cell;
        const py = gy + hash2(gy, gx) * cell;

        if (hasRealTrack && this.distanceToTrackCenterline(px, py) < exclusionRadius) {
          continue; // never overlap the actual road
        }

        const inInfield = hasRealTrack && this.isInsideTrackLoop(px, py);

        // Denser and larger inside the loop than outside, so the infield
        // reads as landscaped grounds rather than empty canvas.
        const threshold = inInfield ? 0.5 : 0.3;
        if (r > threshold) continue;
        const sizeBoost = inInfield ? 1.35 : 1;
        const kind = hash2(gy, gx * 3);

        switch (theme) {
          case 'forest':
            tree(px, py, (13 + r * 9) * sizeBoost, '#1c4220', '#2f6b33');
            break;
          case 'desert':
            if (kind < 0.5) cactus(px, py, 20 * sizeBoost);
            else rock(px, py, 11 * sizeBoost);
            break;
          case 'beach':
            if (kind < 0.45) palm(px, py, 26 * sizeBoost);
            else if (kind < 0.75) umbrella(px, py, 12 * sizeBoost, Math.floor(hash2(px, py) * 360));
            else rock(px, py, 9 * sizeBoost);
            break;
          case 'city':
            if (inInfield || kind < 0.4) building(px, py, 16 * sizeBoost);
            else tree(px, py, 11 * sizeBoost, '#26422a', '#3c6b40');
            break;
          default:
            tree(px, py, (12 + r * 8) * sizeBoost, '#234d27', '#3a7a3e');
        }
      }
    }

    this.drawInfieldLandmark(ctx);
  }

  private drawTrack(ctx: CanvasRenderingContext2D): void {
    if (this.checkpoints.length < 2) return;

    // Road width follows this track's actual seeded checkpoint width
    // (averaged, cached in avgRoadWidth) instead of a fixed 110px for
    // every track.
    const avgWidth = this.avgRoadWidth;

    const traceCenterline = () => {
      ctx.beginPath();
      ctx.moveTo(Number(this.checkpoints[0].positionX), Number(this.checkpoints[0].positionY));
      for (let i = 1; i <= this.checkpoints.length; i++) {
        const cp = this.checkpoints[i % this.checkpoints.length];
        ctx.lineTo(Number(cp.positionX), Number(cp.positionY));
      }
    };

    ctx.save();
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';

    // Asphalt base.
    ctx.strokeStyle = this.theme.road;
    ctx.lineWidth = avgWidth;
    traceCenterline();
    ctx.stroke();

    // Racing groove — a darker worn strip where cars actually put their
    // tires lap after lap, offset off-center slightly like a real racing
    // line through the middle of the road.
    ctx.strokeStyle = 'rgba(0,0,0,0.14)';
    ctx.lineWidth = avgWidth * 0.52;
    traceCenterline();
    ctx.stroke();

    // Asphalt patchiness — short translucent dark dashes scattered along
    // the centerline so the surface reads as laid tarmac, not vector-perfect
    // ribbon. Deterministic per-segment so it doesn't shimmer frame to frame.
    ctx.strokeStyle = 'rgba(0,0,0,0.10)';
    ctx.lineWidth = avgWidth * 0.9;
    ctx.setLineDash([26, 60]);
    ctx.lineDashOffset = 18;
    traceCenterline();
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.lineDashOffset = 0;

    // Solid white edge lines, inset from the curbs — real circuits paint
    // these just inside the kerbs; computed as offset polylines using the
    // same averaged-direction approach as the fences.
    for (const side of [-1, 1]) {
      const edge: { x: number; y: number }[] = [];
      const len = this.checkpoints.length;
      for (let i = 0; i < len; i++) {
        const cur = this.checkpoints[i];
        const prev = this.checkpoints[(i - 1 + len) % len];
        const next = this.checkpoints[(i + 1) % len];

        const dirX = Number(next.positionX) - Number(prev.positionX);
        const dirY = Number(next.positionY) - Number(prev.positionY);
        const dirLen = Math.hypot(dirX, dirY) || 1;
        const nx = (-dirY / dirLen) * side;
        const ny = (dirX / dirLen) * side;

        const offset = Number(cur.width) / 2 - 7;
        edge.push({ x: Number(cur.positionX) + nx * offset, y: Number(cur.positionY) + ny * offset });
      }

      ctx.strokeStyle = 'rgba(255,255,255,0.65)';
      ctx.lineWidth = 3.5;
      ctx.beginPath();
      ctx.moveTo(edge[0].x, edge[0].y);
      for (let i = 1; i <= edge.length; i++) {
        const p = edge[i % edge.length];
        ctx.lineTo(p.x, p.y);
      }
      ctx.stroke();
    }

    // Dashed centerline for a "road" feel.
    ctx.strokeStyle = this.theme.roadEdge;
    ctx.lineWidth = 4;
    ctx.setLineDash([24, 22]);
    traceCenterline();
    ctx.stroke();
    ctx.setLineDash([]);

    // Start/finish — a two-row checkered grid across the track at
    // checkpoint 0, oriented along the track direction, instead of a
    // plain white stripe.
    const start = this.checkpoints[0];
    const next = this.checkpoints[1] ?? this.checkpoints[0];
    const dirX = Number(next.positionX) - Number(start.positionX);
    const dirY = Number(next.positionY) - Number(start.positionY);
    const dirLen = Math.hypot(dirX, dirY) || 1;
    const ux = dirX / dirLen;
    const uy = dirY / dirLen;
    const perpX = -uy;
    const perpY = ux;

    const halfWidth = Number(start.width) / 2 - 6;
    const cell = 11;
    const cols = Math.max(4, Math.floor((halfWidth * 2) / cell));

    ctx.save();
    ctx.translate(Number(start.positionX), Number(start.positionY));
    for (let row = 0; row < 2; row++) {
      for (let col = 0; col < cols; col++) {
        const isBlack = (row + col) % 2 === 0;
        ctx.fillStyle = isBlack ? '#15161a' : '#f5f6f8';
        const lateral = -halfWidth + col * cell;
        const along = (row - 1) * cell + cell / 2;
        ctx.beginPath();
        ctx.rect(
          perpX * lateral + ux * along - cell / 2,
          perpY * lateral + uy * along - cell / 2,
          cell * 1.02, cell * 1.02, // tiny overlap so seams don't show
        );
        ctx.fill();
      }
    }
    ctx.restore();

    ctx.restore();
  }

  /**
   * Continuous solid guardrails tracing both edges of the track, instead
   * of the old scattered circular posts — this is what "fences shown as
   * solid pieces, not points" needed: two closed-loop lines (one per edge)
   * offset from the centerline by each checkpoint's own road half-width,
   * stroked solid, with a dashed hazard-stripe overlay in the accent
   * color for a guardrail look.
   */
  private drawFences(ctx: CanvasRenderingContext2D): void {
    if (this.checkpoints.length < 2) return;

    const len = this.checkpoints.length;
    const inner: { x: number; y: number }[] = [];
    const outer: { x: number; y: number }[] = [];

    for (let i = 0; i < len; i++) {
      const cur = this.checkpoints[i];
      const prev = this.checkpoints[(i - 1 + len) % len];
      const next = this.checkpoints[(i + 1) % len];

      // Average of incoming/outgoing direction so the fence line doesn't kink sharply at each checkpoint.
      const dirX = Number(next.positionX) - Number(prev.positionX);
      const dirY = Number(next.positionY) - Number(prev.positionY);
      const dirLen = Math.hypot(dirX, dirY) || 1;
      const nx = -dirY / dirLen;
      const ny = dirX / dirLen;

      const halfWidth = Number(cur.width) / 2 + FENCE_MARGIN;
      const cx = Number(cur.positionX);
      const cy = Number(cur.positionY);

      inner.push({ x: cx - nx * halfWidth, y: cy - ny * halfWidth });
      outer.push({ x: cx + nx * halfWidth, y: cy + ny * halfWidth });
    }

    ctx.save();
    ctx.lineJoin = 'round';
    ctx.lineCap = 'round';

    for (const edge of [inner, outer]) {
      ctx.strokeStyle = this.theme.fenceMain;
      ctx.lineWidth = 9;
      ctx.beginPath();
      ctx.moveTo(edge[0].x, edge[0].y);
      for (let i = 1; i <= edge.length; i++) {
        const p = edge[i % edge.length];
        ctx.lineTo(p.x, p.y);
      }
      ctx.stroke();

      // Hazard-stripe overlay in the accent color, same stroke path.
      ctx.strokeStyle = this.theme.fenceAlt;
      ctx.lineWidth = 9;
      ctx.setLineDash([22, 22]);
      ctx.stroke();
      ctx.setLineDash([]);
    }

    ctx.restore();
  }

  /**
   * Alternating-color curb/kerb stripes hugging both edges of the
   * drivable road, inside the fences — the classic racing-circuit visual
   * cue this was missing entirely. curbA/curbB were already defined per
   * theme (see THEME_PALETTES) but never actually drawn anywhere until
   * now. Stripe length is measured in real arc-length along the track
   * using a running distance accumulator carried across the whole loop,
   * rather than resetting at each checkpoint (checkpoint spacing varies a
   * lot), so stripes stay a consistent visual size the entire way around
   * instead of stretching or compressing per segment. Each checkpoint-to-
   * checkpoint segment is subdivided into several sub-steps so the
   * striping reads smoothly even where checkpoints are sparse, instead of
   * being limited to one color-per-full-segment.
   */
  private drawCurbs(ctx: CanvasRenderingContext2D): void {
    if (this.checkpoints.length < 2) return;

    const STRIPE_LENGTH = 34;
    const CURB_THICKNESS = 15;
    const SUBDIVISIONS = 5;

    const len = this.checkpoints.length;

    ctx.save();
    ctx.lineCap = 'butt';

    for (const side of [-1, 1] as const) {
      let distanceAccum = 0;
      let prevPoint: { x: number; y: number } | null = null;

      for (let i = 0; i <= len; i++) {
        const cur = this.checkpoints[i % len];
        const prevCp = this.checkpoints[(i - 1 + len) % len];
        const nextCp = this.checkpoints[(i + 1) % len];

        // Averaged in/out direction, same approach as drawFences, so the
        // curb line doesn't kink sharply at each checkpoint.
        const dirX = Number(nextCp.positionX) - Number(prevCp.positionX);
        const dirY = Number(nextCp.positionY) - Number(prevCp.positionY);
        const dirLen = Math.hypot(dirX, dirY) || 1;
        const nx = (-dirY / dirLen) * side;
        const ny = (dirX / dirLen) * side;

        const halfWidth = Number(cur.width) / 2;
        const cx = Number(cur.positionX) + nx * halfWidth;
        const cy = Number(cur.positionY) + ny * halfWidth;

        if (prevPoint) {
          const segDist = Math.hypot(cx - prevPoint.x, cy - prevPoint.y);
          const stepDist = segDist / SUBDIVISIONS;

          for (let s = 0; s < SUBDIVISIONS; s++) {
            const t0 = s / SUBDIVISIONS;
            const t1 = (s + 1) / SUBDIVISIONS;

            const colorIndex = Math.floor(distanceAccum / STRIPE_LENGTH) % 2;
            ctx.strokeStyle = colorIndex === 0 ? this.theme.curbA : this.theme.curbB;
            ctx.lineWidth = CURB_THICKNESS;
            ctx.beginPath();
            ctx.moveTo(prevPoint.x + (cx - prevPoint.x) * t0, prevPoint.y + (cy - prevPoint.y) * t0);
            ctx.lineTo(prevPoint.x + (cx - prevPoint.x) * t1, prevPoint.y + (cy - prevPoint.y) * t1);
            ctx.stroke();

            distanceAccum += stepDist;
          }
        }

        prevPoint = { x: cx, y: cy };
      }
    }

    ctx.restore();
  }

  private drawPlayerCar(ctx: CanvasRenderingContext2D): void {
    const { accentColor, livery } = getCarLivery(this.playerCarName);
    this.drawCar(
      ctx, this.carX, this.carY, this.carAngle,
      this.playerCarColor, this.playerCarShape, this.playerCarName, accentColor, livery,
      true, this.boosting, this.isBraking,
    );
  }

  private drawOpponents(ctx: CanvasRenderingContext2D): void {
    for (const opponent of this.opponents) {
      if (this.checkpoints.length < 2) continue;

      const { x, y, angle } = this.getOpponentRenderPosition(opponent);

      const { accentColor, livery } = getCarLivery(opponent.carName);
      this.drawCar(
        ctx, x, y, angle,
        getCarSwatch(opponent.carName).color, getCarShape(opponent.carName), opponent.carName, accentColor, livery,
        false, false, false,
      );
    }
  }

  /**
   * Draws a car with layered pseudo-3D depth: a grounded drop shadow, a
   * stacked extrusion under the body (dark offset silhouettes that read as
   * the car's side/height), recessed wheel wells with detailed 3D wheels,
   * a beveled body (dark outline + inset top-light stroke), a raised cabin
   * block (own extrusion, glass gradient, roof panel, A-pillars), a
   * per-car livery decal, a door badge, lens headlights, and brake-reactive
   * taillights.
   *
   * Depth model (top-down view): "height" is faked by offsetting darker
   * copies of a shape toward +y in car-local space before drawing the real
   * shape on top — the same trick CSS drop-shadow depth uses. The shadow
   * ellipse is drawn in world space *before* the car's rotate() so it stays
   * grounded regardless of heading (canvas shadowBlur would otherwise spin
   * the shadow with the car, reading as the light source orbiting).
   */
  private drawCar(
    ctx: CanvasRenderingContext2D,
    x: number,
    y: number,
    angle: number,
    color: string,
    shape: CarShapeProfile,
    carName: string,
    accentColor: string,
    livery: LiveryPattern,
    isPlayer: boolean,
    boosting: boolean,
    braking: boolean,
  ): void {
    const { length: bodyLength, width: bodyWidth, frontRadius, rearRadius, spoiler, hoodScoop } = shape;

    // Grounded shadow — two nested soft ellipses (core + wide penumbra),
    // fixed world orientation, offset slightly "down" of the car.
    ctx.save();
    ctx.translate(x + 3 * CAR_RENDER_SCALE, y + bodyWidth * CAR_RENDER_SCALE * 0.2);
    ctx.fillStyle = 'rgba(0,0,0,0.18)';
    ctx.beginPath();
    ctx.ellipse(0, 0, bodyLength * CAR_RENDER_SCALE * 0.52, bodyWidth * CAR_RENDER_SCALE * 0.44, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = 'rgba(0,0,0,0.28)';
    ctx.beginPath();
    ctx.ellipse(0, 0, bodyLength * CAR_RENDER_SCALE * 0.46, bodyWidth * CAR_RENDER_SCALE * 0.38, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();

    ctx.save();
    ctx.translate(x, y);
    ctx.rotate(angle);
    // Uniform render scale — every local-space element below (body,
    // wheels, cabin, lights, liveries) inherits it.
    ctx.scale(CAR_RENDER_SCALE, CAR_RENDER_SCALE);

    // Nitro flame — two layered tongues (outer orange, inner white-hot)
    // with randomized length so it flickers.
    if (boosting) {
      const flameLen = 22 + Math.random() * 14;
      ctx.fillStyle = 'rgba(255,138,61,0.85)';
      ctx.beginPath();
      ctx.moveTo(-bodyLength / 2 + 4, -9);
      ctx.quadraticCurveTo(-bodyLength / 2 - flameLen * 0.6, -5, -bodyLength / 2 - flameLen, 0);
      ctx.quadraticCurveTo(-bodyLength / 2 - flameLen * 0.6, 5, -bodyLength / 2 + 4, 9);
      ctx.closePath();
      ctx.fill();
      ctx.fillStyle = 'rgba(255,244,200,0.9)';
      ctx.beginPath();
      ctx.moveTo(-bodyLength / 2 + 4, -4);
      ctx.quadraticCurveTo(-bodyLength / 2 - flameLen * 0.4, 0, -bodyLength / 2 + 4, 4);
      ctx.closePath();
      ctx.fill();
    }

    // Real-car body silhouette: a smooth closed path through symmetric
    // "stations" [x, halfWidthFraction] — nose tip → nose flare → front
    // fender bulge → door waist → rear haunch → tail. frontRadius and
    // rearRadius from the car's shape profile drive how sharp the nose and
    // tail are, so a wedge-nosed hypercar and a rounded hatchback get
    // genuinely different silhouettes instead of the same rounded
    // rectangle. Every 3D layer below (extrusion, fill, bevel, sheen, AO)
    // reuses this exact geometry via the offset parameter.
    const bodyPath = (offset = 0): Path2D => {
      const hl = bodyLength / 2;
      const hw = bodyWidth / 2;
      const noseFrac = Math.min(0.85, Math.max(0.42, 0.42 + frontRadius / 40));
      const tailFrac = Math.min(0.9, Math.max(0.55, 0.55 + rearRadius / 40));

      const stations: Array<[number, number]> = [
        [hl, 0],                                              // nose tip
        [hl - bodyLength * 0.05, noseFrac * 0.72],            // nose shoulder
        [hl - bodyLength * 0.17, noseFrac],                   // nose flare
        [bodyLength * 0.18, 1],                               // front fender peak
        [0, 0.9],                                             // door waist
        [-bodyLength * 0.2, 0.98],                            // rear haunch rise
        [-hl + rearRadius * 0.9, tailFrac],                   // rear shoulder
        [-hl, tailFrac * 0.5],                                // tail edge
        [-hl, 0],                                             // tail center
      ];

      // Mirror the +y-side stations down the other side (skipping the
      // shared nose-tip/tail-center points) to form one closed symmetric
      // outline, then smooth it with midpoint quadratic curves.
      const outline: Array<[number, number]> = [
        ...stations,
        ...stations.slice(1, -1).reverse().map(([sx, f]) => [sx, -f] as [number, number]),
      ];

      const pts = outline.map(([sx, f]) => [sx, f * hw + offset] as [number, number]);
      const mid = (a: [number, number], b: [number, number]): [number, number] =>
        [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2];

      const p = new Path2D();
      const start = mid(pts[pts.length - 1], pts[0]);
      p.moveTo(start[0], start[1]);
      for (let i = 0; i < pts.length; i++) {
        const cur = pts[i];
        const nxt = pts[(i + 1) % pts.length];
        const m = mid(cur, nxt);
        p.quadraticCurveTo(cur[0], cur[1], m[0], m[1]);
      }
      p.closePath();
      return p;
    };

    // Extrusion stack — progressively darker copies offset toward +y.
    // Gives the top-down silhouette visible thickness/body height while
    // staying fine-grained enough to read as shading, not a hard shadow.
    ctx.fillStyle = darken(color, 0.66);
    ctx.fill(bodyPath(4));
    ctx.fillStyle = darken(color, 0.56);
    ctx.fill(bodyPath(2.5));
    ctx.fillStyle = darken(color, 0.44);
    ctx.fill(bodyPath(1.5));
    ctx.fillStyle = darken(color, 0.32);
    ctx.fill(bodyPath(0.8));

    // Rear wing — plank on raised endplates + struts, drawn before the
    // body/wheels so it tucks under them where they overlap.
    if (spoiler) {
      ctx.fillStyle = '#0d0d0d';
      ctx.fillRect(-bodyLength / 2 - 6, -bodyWidth / 2 - 4, 3, 4); // endplate L
      ctx.fillRect(-bodyLength / 2 - 6, bodyWidth / 2, 3, 4); // endplate R
      ctx.fillStyle = '#161616';
      ctx.fillRect(-bodyLength / 2 - 3, -bodyWidth / 2 - 3, 2, 4);
      ctx.fillRect(-bodyLength / 2 - 3, bodyWidth / 2 - 1, 2, 4);
      ctx.fillStyle = '#1f2126';
      ctx.fillRect(-bodyLength / 2 - 7, -bodyWidth / 2 - 3, 5, bodyWidth + 6); // wing plank
      ctx.fillStyle = 'rgba(255,255,255,0.14)';
      ctx.fillRect(-bodyLength / 2 - 7, -bodyWidth / 2 - 3, 5, 2); // top edge catch-light
    }

    // Wheel wells — darker sockets slightly larger than the tires so the
    // wheels read as sitting *in* the body, not taped on top of it.
    const wheelWidth = bodyLength * 0.19;
    const wheelHeight = bodyWidth * 0.26;
    const rearInset = bodyLength * 0.19;
    const frontInset = bodyLength * 0.36;
    const wheelPositions: Array<[number, number]> = [
      [-bodyLength / 2 + rearInset, -bodyWidth / 2 - wheelHeight * 0.3],
      [-bodyLength / 2 + rearInset, bodyWidth / 2 - wheelHeight * 0.7],
      [bodyLength / 2 - frontInset, -bodyWidth / 2 - wheelHeight * 0.3],
      [bodyLength / 2 - frontInset, bodyWidth / 2 - wheelHeight * 0.7],
    ];
    for (const [wx, wy] of wheelPositions) {
      ctx.fillStyle = 'rgba(0,0,0,0.5)';
      ctx.beginPath();
      ctx.roundRect(wx - 1.5, wy - 1.5, wheelWidth + 3, wheelHeight + 3, 3);
      ctx.fill();
    }

    // Wheels — radial-gradient tire (darker at edge) + machined hub with a
    // rim ring, so each wheel reads as a 3D cylinder rather than a dot.
    for (const [wx, wy] of wheelPositions) {
      const cxw = wx + wheelWidth / 2;
      const cyw = wy + wheelHeight / 2;

      const tire = ctx.createRadialGradient(cxw, cyw, 1, cxw, cyw, wheelHeight * 0.75);
      tire.addColorStop(0, '#2c2f35');
      tire.addColorStop(0.75, '#15161a');
      tire.addColorStop(1, '#050506');
      ctx.fillStyle = tire;
      ctx.beginPath();
      ctx.roundRect(wx, wy, wheelWidth, wheelHeight, 2);
      ctx.fill();

      const hubRadius = Math.min(wheelWidth, wheelHeight) * 0.32;
      const hub = ctx.createRadialGradient(cxw - hubRadius * 0.3, cyw - hubRadius * 0.3, 0.5, cxw, cyw, hubRadius);
      hub.addColorStop(0, '#c8ccd4');
      hub.addColorStop(0.6, '#7d838e');
      hub.addColorStop(1, '#4a4e57');
      ctx.fillStyle = hub;
      ctx.beginPath();
      ctx.arc(cxw, cyw, hubRadius, 0, Math.PI * 2);
      ctx.fill();
      ctx.strokeStyle = 'rgba(220,225,235,0.55)';
      ctx.lineWidth = 0.8;
      ctx.stroke();
    }

    // Body — multi-stop gradient along the width axis (bright roof-line →
    // base → dark rocker panel) instead of a flat fill.
    const bodyGradient = ctx.createLinearGradient(0, -bodyWidth / 2, 0, bodyWidth / 2);
    bodyGradient.addColorStop(0, lighten(color, 0.42));
    bodyGradient.addColorStop(0.28, lighten(color, 0.12));
    bodyGradient.addColorStop(0.55, color);
    bodyGradient.addColorStop(1, darken(color, 0.35));

    ctx.fillStyle = bodyGradient;
    ctx.strokeStyle = isPlayer ? '#ffffff' : 'rgba(10,10,14,0.75)';
    ctx.lineWidth = isPlayer ? 3.5 : 2.5;
    ctx.fill(bodyPath());
    ctx.stroke(bodyPath());

    // Bevel — an inset stroke along the top edge only (clip to the upper
    // half in local space) so light reads as coming from above the car.
    ctx.save();
    ctx.clip(bodyPath());
    ctx.beginPath();
    ctx.rect(-bodyLength, -bodyWidth, bodyLength * 2, bodyWidth);
    ctx.clip();
    ctx.strokeStyle = 'rgba(255,255,255,0.35)';
    ctx.lineWidth = 1.6;
    ctx.stroke(bodyPath(-1.2));
    ctx.restore();

    const hl = bodyLength / 2;
    const hw = bodyWidth / 2;
    const noseFrac = Math.min(0.85, Math.max(0.42, 0.42 + frontRadius / 40));
    const tailFrac = Math.min(0.9, Math.max(0.55, 0.55 + rearRadius / 40));

    // Front splitter — a dark aero lip protruding past the nose, and rear
    // diffuser — dark vertical fins under the tail. Both drawn on top of
    // the body but under the lights/glass so they read as bolt-on aero.
    ctx.fillStyle = 'rgba(12,13,16,0.9)';
    ctx.beginPath();
    ctx.roundRect(hl - 3, -hw * noseFrac * 0.62, 4.5, hw * noseFrac * 1.24, 2);
    ctx.fill();
    for (const fin of [-0.28, 0, 0.28]) {
      ctx.fillStyle = 'rgba(12,13,16,0.85)';
      ctx.beginPath();
      ctx.roundRect(-hl - 1, fin * hw * tailFrac * 1.6 - 1, 4, 3.2, 1);
      ctx.fill();
    }

    // Diagonal glossy sheen, clipped to the body outline so it reads as a
    // paint highlight rather than a shape of its own.
    ctx.save();
    ctx.clip(bodyPath());
    const sheen = ctx.createLinearGradient(-bodyLength * 0.1, -bodyWidth / 2, bodyLength * 0.35, bodyWidth / 2);
    sheen.addColorStop(0, 'rgba(255,255,255,0)');
    sheen.addColorStop(0.5, 'rgba(255,255,255,0.22)');
    sheen.addColorStop(1, 'rgba(255,255,255,0)');
    ctx.fillStyle = sheen;
    ctx.fillRect(-bodyLength / 2, -bodyWidth / 2, bodyLength, bodyWidth);
    ctx.restore();

    // Ambient occlusion — soft dark pooling on the body under the cabin,
    // grounding the raised block that's about to be drawn on top of it.
    const cabinFrontX = bodyLength * 0.34;
    const cabinRearX = -bodyLength * 0.12;
    const ao = ctx.createRadialGradient((cabinFrontX + cabinRearX) / 2, 0, 2, (cabinFrontX + cabinRearX) / 2, 0, bodyWidth * 0.62);
    ao.addColorStop(0, 'rgba(0,0,0,0.28)');
    ao.addColorStop(1, 'rgba(0,0,0,0)');
    ctx.save();
    ctx.clip(bodyPath());
    ctx.fillStyle = ao;
    ctx.fillRect(-bodyLength / 2, -bodyWidth / 2, bodyLength, bodyWidth);
    ctx.restore();

    // Livery decal, themed per car (see CAR_VISUALS in car-visuals.ts).
    this.drawLivery(ctx, livery, accentColor, bodyLength, bodyWidth, rearRadius, frontRadius);

    // Cabin — a raised greenhouse: extruded glass block, roof panel in the
    // body color, and A-pillar strokes at the windshield edge. The whole
    // block sits slightly toward +y like the body extrusion so the light
    // direction stays consistent across every layer.
    const cabinHalfWidth = bodyWidth / 2 - 5;
    const cabinPath = (offset = 0) => {
      const p = new Path2D();
      p.moveTo(cabinFrontX, -cabinHalfWidth * 0.7 + offset);
      p.lineTo(cabinFrontX - 4, cabinHalfWidth * 0.7 + offset);
      p.lineTo(cabinRearX, cabinHalfWidth + offset);
      p.lineTo(cabinRearX - 3, -cabinHalfWidth + offset);
      p.closePath();
      return p;
    };

    ctx.fillStyle = 'rgba(15,18,24,0.85)';
    ctx.fill(cabinPath(2));

    const glassGradient = ctx.createLinearGradient(0, -bodyWidth / 2, 0, bodyWidth / 2);
    glassGradient.addColorStop(0, 'rgba(255,255,255,0.9)');
    glassGradient.addColorStop(0.5, 'rgba(175,205,230,0.72)');
    glassGradient.addColorStop(1, 'rgba(95,130,165,0.6)');
    ctx.fillStyle = glassGradient;
    ctx.fill(cabinPath());

    // Roof panel — opaque body-colored inset with its own mini-gradient,
    // leaving a glass border visible around it on all sides.
    const roofInset = Math.min(4.5, bodyWidth * 0.14);
    const roofGradient = ctx.createLinearGradient(0, -bodyWidth / 2, 0, bodyWidth / 2);
    roofGradient.addColorStop(0, lighten(color, 0.5));
    roofGradient.addColorStop(1, lighten(color, 0.1));
    ctx.fillStyle = roofGradient;
    ctx.beginPath();
    ctx.moveTo(cabinFrontX - roofInset - 2, -cabinHalfWidth * 0.7 + roofInset);
    ctx.lineTo(cabinFrontX - roofInset - 5, cabinHalfWidth * 0.7 - roofInset);
    ctx.lineTo(cabinRearX + roofInset, cabinHalfWidth - roofInset);
    ctx.lineTo(cabinRearX - 3 + roofInset, -cabinHalfWidth + roofInset);
    ctx.closePath();
    ctx.fill();

    // A-pillars — thin dark strokes fanning from the roof front edge,
    // reading as the frame between windshield and side glass.
    ctx.strokeStyle = 'rgba(20,24,30,0.8)';
    ctx.lineWidth = 1.2;
    ctx.beginPath();
    ctx.moveTo(cabinFrontX - roofInset - 2, -cabinHalfWidth * 0.7 + roofInset);
    ctx.lineTo(cabinFrontX, -cabinHalfWidth * 0.7);
    ctx.moveTo(cabinFrontX - roofInset - 5, cabinHalfWidth * 0.7 - roofInset);
    ctx.lineTo(cabinFrontX - 4, cabinHalfWidth * 0.7);
    ctx.stroke();

    // Side mirrors — small stalked heads poking out just past the door
    // waist at the windshield's base, the signature "real car" tell in
    // top-down view.
    for (const side of [-1, 1]) {
      const mx = cabinFrontX - 3;
      const my = side * (cabinHalfWidth + 2.5);
      ctx.strokeStyle = darken(color, 0.4);
      ctx.lineWidth = 1.6;
      ctx.beginPath();
      ctx.moveTo(mx, side * cabinHalfWidth * 0.8);
      ctx.lineTo(mx - 1.5, my);
      ctx.stroke();
      ctx.fillStyle = color;
      ctx.beginPath();
      ctx.ellipse(mx - 1.5, my, 2.6, 1.8, side * 0.3, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = 'rgba(255,255,255,0.5)';
      ctx.beginPath();
      ctx.ellipse(mx - 1.5, my, 1.1, 0.7, side * 0.3, 0, Math.PI * 2);
      ctx.fill();
    }

    if (hoodScoop) {
      ctx.fillStyle = 'rgba(0,0,0,0.5)';
      ctx.beginPath();
      ctx.roundRect(bodyLength * 0.12, -bodyWidth * 0.14 + 1.5, bodyLength * 0.2, bodyWidth * 0.28, 3);
      ctx.fill();
      ctx.fillStyle = 'rgba(0,0,0,0.35)';
      ctx.beginPath();
      ctx.roundRect(bodyLength * 0.12, -bodyWidth * 0.14, bodyLength * 0.2, bodyWidth * 0.28, 3);
      ctx.fill();
    }

    // Door badge — the car's initial in its accent color, sitting behind
    // the cabin where it won't collide with the windshield or livery.
    const badgeX = -bodyLength * 0.3;
    const badgeRadius = bodyWidth * 0.22;
    ctx.beginPath();
    ctx.arc(badgeX, 0, badgeRadius, 0, Math.PI * 2);
    ctx.fillStyle = 'rgba(255,255,255,0.85)';
    ctx.fill();
    ctx.strokeStyle = accentColor;
    ctx.lineWidth = 2;
    ctx.stroke();
    ctx.fillStyle = darken(accentColor, 0.35);
    ctx.font = `bold ${Math.round(badgeRadius * 1.15)}px sans-serif`;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(carName.charAt(0).toUpperCase() || '?', badgeX, 1);

    // Headlights — lens ellipses with a soft glow bloom, sitting on the
    // nose shoulders where the fenders start.
    ctx.save();
    ctx.fillStyle = '#fff6cf';
    ctx.shadowColor = 'rgba(255,244,190,0.8)';
    ctx.shadowBlur = 5;
    for (const side of [-1, 1]) {
      ctx.beginPath();
      ctx.ellipse(hl - 4.5, side * hw * noseFrac * 0.66, 3.5, 2.2, side * 0.25, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.restore();

    // Taillights — glow red under braking, dim red otherwise. The one piece
    // of car-shape state that's functional rather than purely decorative:
    // the player's own brake input drives it directly (isBraking, set in
    // updatePhysics); opponents don't expose raw input over the wire, so
    // theirs stay dim.
    ctx.fillStyle = braking ? '#ff2f2f' : 'rgba(200,50,50,0.55)';
    if (braking) {
      ctx.shadowColor = '#ff2f2f';
      ctx.shadowBlur = 9;
    }
    for (const side of [-1, 1]) {
      ctx.beginPath();
      ctx.ellipse(-hl + 3.5, side * hw * tailFrac * 0.62, 3, 2, 0, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.shadowBlur = 0;

    ctx.restore();

    if (isPlayer) {
      ctx.save();
      ctx.fillStyle = 'rgba(255,255,255,0.9)';
      ctx.font = 'bold 13px sans-serif';
      ctx.textAlign = 'center';
      ctx.shadowColor = 'rgba(0,0,0,0.8)';
      ctx.shadowBlur = 4;
      ctx.fillText('YOU', x, y - 34 * CAR_RENDER_SCALE);
      ctx.restore();
    }
  }

  /**
   * Renders one of five decal patterns in local car space (+x = front),
   * themed to each car's name so liveries read as distinct designs rather
   * than one shared stripe recolored per car:
   *  - stripes (Speedster): classic double racing stripe down the center.
   *  - bolt (Lightning): a jagged lightning-bolt decal across the flank.
   *  - fade (Phantom): a soft pearlescent fade from the tail, ghostly.
   *  - chevron (Shadow): three sharp forward-pointing chevrons.
   *  - flames (Thunder): a flame lick from the nose along the side.
   */
  private drawLivery(
    ctx: CanvasRenderingContext2D,
    livery: LiveryPattern,
    accentColor: string,
    bodyLength: number,
    bodyWidth: number,
    rearRadius: number,
    frontRadius: number,
  ): void {
    switch (livery) {
      case 'stripes': {
        ctx.strokeStyle = hexToRgba(accentColor, 0.85);
        ctx.lineWidth = bodyWidth * 0.1;
        ctx.beginPath();
        ctx.moveTo(-bodyLength / 2 + rearRadius * 0.6, -bodyWidth * 0.12);
        ctx.lineTo(bodyLength / 2 - frontRadius * 0.6, -bodyWidth * 0.12);
        ctx.moveTo(-bodyLength / 2 + rearRadius * 0.6, bodyWidth * 0.12);
        ctx.lineTo(bodyLength / 2 - frontRadius * 0.6, bodyWidth * 0.12);
        ctx.stroke();
        break;
      }
      case 'bolt': {
        const hl = bodyLength / 2 * 0.6;
        const hw = bodyWidth / 2;
        ctx.fillStyle = accentColor;
        ctx.beginPath();
        ctx.moveTo(0.35 * hl, -0.9 * hw);
        ctx.lineTo(0.05 * hl, -0.15 * hw);
        ctx.lineTo(0.3 * hl, -0.15 * hw);
        ctx.lineTo(-0.25 * hl, 0.9 * hw);
        ctx.lineTo(0.0 * hl, 0.05 * hw);
        ctx.lineTo(-0.3 * hl, 0.05 * hw);
        ctx.closePath();
        ctx.fill();
        ctx.strokeStyle = 'rgba(0,0,0,0.25)';
        ctx.lineWidth = 1;
        ctx.stroke();
        break;
      }
      case 'fade': {
        const fade = ctx.createLinearGradient(-bodyLength / 2, 0, bodyLength * 0.1, 0);
        fade.addColorStop(0, hexToRgba(accentColor, 0.8));
        fade.addColorStop(1, hexToRgba(accentColor, 0));
        ctx.fillStyle = fade;
        ctx.beginPath();
        ctx.roundRect(
          -bodyLength / 2 + 2, -bodyWidth / 2 + 3,
          bodyLength * 0.55, bodyWidth - 6,
          [rearRadius * 0.8, 3, 3, rearRadius * 0.8],
        );
        ctx.fill();
        break;
      }
      case 'chevron': {
        ctx.strokeStyle = hexToRgba(accentColor, 0.9);
        ctx.lineWidth = bodyWidth * 0.09;
        ctx.lineCap = 'butt';
        for (const offset of [-0.22, 0, 0.22]) {
          const cx = bodyLength * 0.15 + offset * bodyLength * 0.3;
          ctx.beginPath();
          ctx.moveTo(cx, -bodyWidth * 0.4);
          ctx.lineTo(cx + bodyLength * 0.17, 0);
          ctx.lineTo(cx, bodyWidth * 0.4);
          ctx.stroke();
        }
        break;
      }
      case 'flames': {
        ctx.fillStyle = accentColor;
        ctx.beginPath();
        ctx.moveTo(bodyLength * 0.48, -bodyWidth * 0.05);
        ctx.quadraticCurveTo(bodyLength * 0.1, -bodyWidth * 0.35, -bodyLength * 0.15, -bodyWidth * 0.12);
        ctx.quadraticCurveTo(bodyLength * 0.05, -bodyWidth * 0.05, -bodyLength * 0.05, bodyWidth * 0.02);
        ctx.quadraticCurveTo(bodyLength * 0.15, bodyWidth * 0.3, bodyLength * 0.45, bodyWidth * 0.08);
        ctx.quadraticCurveTo(bodyLength * 0.3, 0, bodyLength * 0.48, -bodyWidth * 0.05);
        ctx.closePath();
        ctx.fill();
        break;
      }
    }
  }
}

/** Standard segment-segment intersection test — returns the crossing point if segments p1-p2 and p3-p4 actually intersect, else null. */
function segmentIntersection(
  p1: { x: number; y: number }, p2: { x: number; y: number },
  p3: { x: number; y: number }, p4: { x: number; y: number },
): { x: number; y: number } | null {
  const d1x = p2.x - p1.x;
  const d1y = p2.y - p1.y;
  const d2x = p4.x - p3.x;
  const d2y = p4.y - p3.y;
  const denom = d1x * d2y - d1y * d2x;
  if (Math.abs(denom) < 1e-9) return null; // parallel or degenerate

  const t = ((p3.x - p1.x) * d2y - (p3.y - p1.y) * d2x) / denom;
  const u = ((p3.x - p1.x) * d1y - (p3.y - p1.y) * d1x) / denom;

  if (t < 0 || t > 1 || u < 0 || u > 1) return null;

  return { x: p1.x + t * d1x, y: p1.y + t * d1y };
}

function segmentFraction(px: number, py: number, prev: TrackCheckpointDto, next: TrackCheckpointDto): number {
  const ax = Number(prev.positionX);
  const ay = Number(prev.positionY);
  const bx = Number(next.positionX);
  const by = Number(next.positionY);

  const abx = bx - ax;
  const aby = by - ay;
  const abLenSq = abx * abx + aby * aby;

  if (abLenSq === 0) return 0;

  const apx = px - ax;
  const apy = py - ay;

  const t = (apx * abx + apy * aby) / abLenSq;
  return Math.max(0, Math.min(1, t));
}

function formatMs(ms: number): string {
  const totalSeconds = ms / 1000;
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = Math.floor(totalSeconds % 60);
  const centiseconds = Math.floor((totalSeconds - Math.floor(totalSeconds)) * 100);
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}.${String(centiseconds).padStart(2, '0')}`;
}
