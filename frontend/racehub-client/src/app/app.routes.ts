import { Routes } from '@angular/router';

import { AppShellComponent } from './layout/app-shell/app-shell.component';
import { LandingComponent } from './pages/landing/landing.component';
import { AuthComponent } from './pages/auth/auth.component';
import { LobbyComponent } from './pages/lobby/lobby.component';
import { RoomComponent } from './pages/room/room.component';
import { RaceComponent } from './pages/race/race.component';
import { ResultsComponent } from './pages/results/results.component';
import { LeaderboardComponent } from './pages/leaderboard/leaderboard.component';
import { GarageComponent } from './pages/garage/garage.component';
import { ShopComponent } from './pages/shop/shop.component';
import { ProfileComponent } from './pages/profile/profile.component';
import { FriendsComponent } from './pages/friends/friends.component';
import { SettingsComponent } from './pages/settings/settings.component';
import { authGuard } from './core/guards/auth.guard';
import { guestGuard } from './core/guards/guest.guard';

/**
 * Screens 1 & 2 (landing, auth) render full-bleed with no sidebar.
 * Screens 3, 5-10 (lobby, room, race, results, leaderboard, garage,
 * shop, profile) are nested under AppShellComponent, which renders
 * the persistent sidebar + topbar (mirrors `shellScreens` in the
 * original prototype's go() function), and are protected by authGuard —
 * an unauthenticated user is redirected to /auth.
 */
export const routes: Routes = [
  { path: '', component: LandingComponent, pathMatch: 'full' },
  { path: 'auth', component: AuthComponent, canActivate: [guestGuard] },
  {
    // Rendered full-page, deliberately outside AppShellComponent: the race
    // canvas needs the entire viewport to show the whole track without
    // being squeezed by the sidebar/topbar chrome (which is what was
    // cutting it off), and the in-race controls/HUD are meant to be the
    // only thing on screen while racing anyway.
    path: 'race',
    component: RaceComponent,
    canActivate: [authGuard],
  },
  {
    path: '',
    component: AppShellComponent,
    canActivate: [authGuard],
    children: [
      { path: 'lobby', component: LobbyComponent },
      { path: 'room/:id', component: RoomComponent },
      { path: 'results', component: ResultsComponent },
      { path: 'leaderboard', component: LeaderboardComponent },
      { path: 'garage', component: GarageComponent },
      { path: 'shop', component: ShopComponent },
      { path: 'profile', component: ProfileComponent },
      { path: 'friends', component: FriendsComponent },
      { path: 'settings', component: SettingsComponent },
    ],
  },
  { path: '**', redirectTo: '' },
];
