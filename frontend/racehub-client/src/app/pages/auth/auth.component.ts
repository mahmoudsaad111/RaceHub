import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, ElementRef, ViewChild, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AuthError, AuthService } from '../../core/services/auth.service';

// Minimal shape of the Google Identity Services global — no @types package
// is installed for it, and pulling one in for a single button isn't worth
// the dependency.
declare const google: any;

/**
 * Screen 2 — Login / Register. Both forms call the real backend through
 * AuthService; each shows a top-level error banner (from ApiResponse.message)
 * plus per-field errors when the backend's FluentValidation kicks in.
 */
@Component({
  selector: 'rh-auth',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './auth.component.html',
  styleUrl: './auth.component.scss',
})
export class AuthComponent implements AfterViewInit {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly authService = inject(AuthService);

  @ViewChild('googleLoginBtn') googleLoginBtn?: ElementRef<HTMLDivElement>;
  @ViewChild('googleRegisterBtn') googleRegisterBtn?: ElementRef<HTMLDivElement>;

  loginForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
    rememberMe: [false],
  });

  registerForm = this.fb.nonNullable.group({
    username: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', Validators.required],
  });

  showLoginPassword = false;
  showRegisterPassword = false;
  showConfirmPassword = false;

  loginLoading = false;
  registerLoading = false;
  googleLoading = false;

  loginError: string | null = null;
  registerError: string | null = null;
  googleError: string | null = null;

  private get returnUrl(): string {
    return this.route.snapshot.queryParamMap.get('returnUrl') ?? '/lobby';
  }

  ngAfterViewInit(): void {
    this.initGoogleSignIn();
  }

  onLogin(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.loginError = null;
    this.loginLoading = true;

    const { email, password } = this.loginForm.getRawValue();

    this.authService.login({ email, password }).subscribe({
      next: () => {
        this.loginLoading = false;
        this.router.navigateByUrl(this.returnUrl);
      },
      error: (err: AuthError) => {
        this.loginLoading = false;
        this.loginError = err.message;
        this.applyFieldErrors(this.loginForm, err);
      },
    });
  }

  onRegister(): void {
    if (this.registerForm.invalid) {
      this.registerForm.markAllAsTouched();
      return;
    }

    const { username, email, password, confirmPassword } = this.registerForm.getRawValue();

    if (password !== confirmPassword) {
      this.registerError = 'Passwords do not match.';
      return;
    }

    this.registerError = null;
    this.registerLoading = true;

    this.authService.register({ displayName: username, email, password }).subscribe({
      next: () => {
        this.registerLoading = false;
        this.router.navigateByUrl(this.returnUrl);
      },
      error: (err: AuthError) => {
        this.registerLoading = false;
        this.registerError = err.message;
        this.applyFieldErrors(this.registerForm, err);
      },
    });
  }

  /** Maps backend field-error keys (PascalCase) onto Angular form controls. */
  private applyFieldErrors(form: FormGroup, err: AuthError): void {
    if (!err.fieldErrors) return;

    for (const [field, messages] of Object.entries(err.fieldErrors)) {
      const control = form.get(field.charAt(0).toLowerCase() + field.slice(1));
      control?.setErrors({ server: messages[0] });
    }
  }

  private initGoogleSignIn(): void {
    if (typeof google === 'undefined' || !environment.googleClientId) {
      return;
    }

    google.accounts.id.initialize({
      client_id: environment.googleClientId,
      callback: (response: { credential: string }) => this.onGoogleCredential(response.credential),
    });

    const buttonOptions = { theme: 'outline', size: 'large', width: 300 };

    if (this.googleLoginBtn) {
      google.accounts.id.renderButton(this.googleLoginBtn.nativeElement, buttonOptions);
    }
    if (this.googleRegisterBtn) {
      google.accounts.id.renderButton(this.googleRegisterBtn.nativeElement, buttonOptions);
    }
  }

  private onGoogleCredential(idToken: string): void {
    this.googleError = null;
    this.googleLoading = true;

    this.authService.loginWithGoogle({ idToken }).subscribe({
      next: () => {
        this.googleLoading = false;
        this.router.navigateByUrl(this.returnUrl);
      },
      error: (err: AuthError) => {
        this.googleLoading = false;
        this.googleError = err.message;
      },
    });
  }
}
