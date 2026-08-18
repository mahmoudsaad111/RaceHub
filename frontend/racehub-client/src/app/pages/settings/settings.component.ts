import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'rh-settings',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss',
})
export class SettingsComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly currentUser = this.authService.currentUser;

  // Client-only preferences — there's no user-preferences endpoint on the
  // backend yet, so these don't persist across sessions/devices. Wiring
  // them up for real just needs a small Settings feature (a UserSettings
  // table + GET/PUT endpoint) mirroring how Profile/Cars are done.
  soundEnabled = true;
  raceNotifications = true;
  friendNotifications = true;

  logout(): void {
    this.authService.logout().subscribe(() => {
      this.router.navigateByUrl('/auth');
    });
  }
}
