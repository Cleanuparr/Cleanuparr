# Cleanuparr Frontend Rewrite Plan

> **Status**: Phases 1-4 complete. Phase 5 (Polish & Parity) in progress.
> **Last Updated**: 2026-02-08

## Table of Contents
- [1. Goals & Principles](#1-goals--principles)
- [2. Technology Stack](#2-technology-stack)
- [3. Project Setup & Directory Structure](#3-project-setup--directory-structure)
- [4. Design System & Theming](#4-design-system--theming)
- [5. Component Architecture](#5-component-architecture)
- [6. State Management](#6-state-management)
- [7. Routing & Navigation](#7-routing--navigation)
- [8. API & Real-Time Communication](#8-api--real-time-communication)
- [9. Authentication Preparation](#9-authentication-preparation)
- [10. Performance & Accessibility](#10-performance--accessibility)
- [11. Testing Strategy](#11-testing-strategy)
- [12. Migration & Deployment](#12-migration--deployment)
- [13. Implementation Phases](#13-implementation-phases)
- [14. Decisions Log](#14-decisions-log)

---

## 1. Goals & Principles

### Why Rewrite?
- Modernize the UI with a glassmorphism-inspired design language
- Build a proper design system with fully modular, reusable components
- Adopt the latest Angular patterns (zoneless, signals, standalone-first)
- Prepare the architecture for future features (authentication, etc.)
- Improve developer experience with cleaner structure and less boilerplate
- Support both light and dark themes with a purple-centric brand identity

### Core Principles
1. **New project, clean slate** - Created in `code/frontend-v2/`, the current `code/frontend/` stays untouched
2. **Modularity above all** - Every UI element is a self-contained component with its own HTML, SCSS, and TS
3. **SCSS variables everywhere** - All colors, spacing, radii, and effects defined as SCSS variables and CSS custom properties; never hardcode values
4. **Don't reinvent the wheel** - Use Spartan UI's headless primitives for behavior/accessibility; own the styling layer
5. **Pragmatic comments** - Comment only when the "why" isn't obvious; no decorative or redundant comments
6. **Progressive enhancement** - Glassmorphism effects degrade gracefully; users can disable animations
7. **Auth-ready architecture** - Route guards, auth service interface, and layout structure ready for plug-in auth
8. **Dual theme support** - Dark (default) and light themes, both using Cleanuparr's purple brand colors
9. **No monoliths** - Split services by domain/feature (mirroring backend controllers). No single god-service that handles all API calls. Each feature area gets its own API service.
10. **Responsive, not mobile-first** - Desktop is the primary target, but the app should look good on mobile. Single 768px breakpoint for the desktop/mobile split. Sidebar becomes an overlay drawer on mobile with backdrop.

---

## 2. Technology Stack

### Core Framework
| Package | Version | Notes |
|---------|---------|-------|
| **Angular** | **21.x** (latest) | Zoneless by default, Signal Forms, standalone components |
| **TypeScript** | 5.8+ | Strict mode enabled |
| **RxJS** | 7.8+ | For async streams; signals for synchronous state |

### State Management
| Package | Version | Notes |
|---------|---------|-------|
| **@ngrx/signals** | Latest compatible | Signal stores for feature state |
| **Angular Signals** | Built-in | Local component state, computed values |

### Real-Time
| Package | Version | Notes |
|---------|---------|-------|
| **@microsoft/signalr** | 8.x | Same backend hub contract |

### UI Components: Spartan UI
| Package | Purpose |
|---------|---------|
| **@spartan-ng/brain/*** | Headless, accessible UI primitives (behavior + a11y) |
| **@spartan-ng/helm/*** | Styled component wrappers (we'll customize these heavily for glassmorphism) |

> **Why Spartan UI?** It's the Angular equivalent of shadcn/ui - the most popular headless UI approach in the React world, now ported to Angular. Key reasons:
> 1. **Headless primitives** (`brain`) handle all the hard parts: focus management, keyboard navigation, ARIA attributes, overlay positioning, animations - without imposing any visual style
> 2. **Copy-paste styled components** (`helm`) give us full ownership of the styling layer - we copy them into our project and customize freely for glassmorphism
> 3. **Built on Angular CDK** - battle-tested overlay, portal, and a11y foundations
> 4. **Signal-native, zoneless-ready** - designed for modern Angular from the ground up
> 5. **TailwindCSS v4 integration** - uses CSS custom properties for theming, which aligns perfectly with our variable-driven approach
> 6. **50+ components** including Dialog, Select, Table, Tabs, Accordion, Toast, Tooltip, etc. - covers everything we need
> 7. **We don't reinvent the wheel** - writing accessible dropdowns, modals, and focus traps from scratch is error-prone and time-consuming; Spartan handles all of that while we focus purely on the glassmorphism design

### Styling
| Tool | Purpose |
|------|---------|
| **TailwindCSS v4** | Utility classes + Spartan integration |
| **SCSS** | Component-scoped styles, glass mixins, design tokens |
| **CSS Custom Properties** | Theme switching (light/dark), runtime token overrides |

> **Note on TailwindCSS**: Required by Spartan UI's helm components. We use it for utility classes in templates (layout, spacing, responsive) but define our glassmorphism design system in SCSS variables/mixins. Component `.scss` files handle glass effects and custom styling; Tailwind handles layout utilities in templates.

### Icons: Tabler Icons
| Package | Notes |
|---------|-------|
| **angular-tabler-icons** | Angular component wrappers for Tabler SVGs |
| **@tabler/icons** | 5,900+ free MIT-licensed SVG icons |

> **Why Tabler Icons?** Largest free icon set (5,900+), designed specifically for dashboards and data-heavy interfaces on a consistent 24x24 grid with 2px stroke. MIT licensed. Has dedicated Angular packages. Perfect for an admin/management tool like Cleanuparr.

### Typography: Inter
| Font | Usage |
|------|-------|
| **Inter** | All UI text (headings, body, labels) |
| **JetBrains Mono** | Monospace (logs, code, technical data) |

> **Why Inter?** Designed specifically for screen readability with a tall x-height, open letterforms, and excellent weight range. Industry standard for UI design. Performs exceptionally well at small sizes in dense dashboard layouts.

### Testing
| Package | Notes |
|---------|-------|
| **Vitest** | Default in Angular 21, replaces Jasmine/Karma |
| **Angular Testing Library** | For component tests |

### Build & Tooling
| Tool | Notes |
|------|-------|
| **Angular CLI 21** | Project scaffolding and builds |
| **ESLint** | With Angular ESLint plugin |
| **Prettier** | Code formatting |

---

## 3. Project Setup & Directory Structure

### Location
```
code/frontend-v2/          <-- NEW project
code/frontend/             <-- UNTOUCHED, serves as reference only
```

### Directory Structure
```
code/frontend-v2/
├── src/
│   ├── app/
│   │   ├── core/                           # Singleton services, guards, interceptors
│   │   │   ├── auth/                       # Auth service interface & guards (placeholder)
│   │   │   │   ├── auth.service.ts
│   │   │   │   ├── auth.guard.ts
│   │   │   │   └── auth.interceptor.ts
│   │   │   ├── api/                        # API services, split by domain (mirrors backend controllers)
│   │   │   │   ├── general-config.api.ts   # General configuration endpoints
│   │   │   │   ├── queue-cleaner.api.ts    # Queue cleaner config endpoints
│   │   │   │   ├── malware-blocker.api.ts  # Malware blocker config endpoints
│   │   │   │   ├── download-cleaner.api.ts # Download cleaner config endpoints
│   │   │   │   ├── blacklist-sync.api.ts   # Blacklist sync config endpoints
│   │   │   │   ├── arr.api.ts              # Arr instance CRUD + config (sonarr/radarr/etc.)
│   │   │   │   ├── download-client.api.ts  # Download client CRUD + config
│   │   │   │   ├── notification.api.ts     # Notification provider CRUD + config
│   │   │   │   ├── jobs.api.ts             # Job invocation endpoints
│   │   │   │   ├── events.api.ts           # Events endpoints
│   │   │   │   └── system.api.ts           # System/health endpoints
│   │   │   ├── realtime/                   # SignalR services
│   │   │   │   ├── hub.service.ts          # Base hub connection
│   │   │   │   └── app-hub.service.ts      # App-specific hub events
│   │   │   ├── services/
│   │   │   │   ├── theme.service.ts        # Theme (light/dark) + reduced motion
│   │   │   │   ├── toast.service.ts
│   │   │   │   └── breakpoint.service.ts
│   │   │   └── interceptors/
│   │   │       ├── error.interceptor.ts
│   │   │       └── base-url.interceptor.ts
│   │   │
│   │   ├── ui/                             # Custom-styled Spartan helm components
│   │   │   ├── button/
│   │   │   │   ├── button.component.ts
│   │   │   │   ├── button.component.html
│   │   │   │   └── button.component.scss
│   │   │   ├── input/
│   │   │   │   ├── input.component.ts
│   │   │   │   ├── input.component.html
│   │   │   │   └── input.component.scss
│   │   │   ├── dropdown/
│   │   │   │   ├── dropdown.component.ts
│   │   │   │   ├── dropdown.component.html
│   │   │   │   └── dropdown.component.scss
│   │   │   ├── checkbox/
│   │   │   ├── toggle/
│   │   │   ├── card/                       # Glass card component
│   │   │   ├── modal/                      # Glass modal/dialog
│   │   │   ├── toast/                      # Toast notification
│   │   │   ├── table/                      # Data table
│   │   │   ├── tabs/
│   │   │   ├── accordion/
│   │   │   ├── tooltip/
│   │   │   ├── badge/
│   │   │   ├── spinner/
│   │   │   ├── skeleton/                   # Skeleton loader
│   │   │   ├── icon/                       # Tabler icon wrapper
│   │   │   ├── empty-state/
│   │   │   ├── confirm-dialog/
│   │   │   ├── sidebar/                    # Slide-out panel
│   │   │   ├── menu/
│   │   │   ├── tag/
│   │   │   ├── progress-bar/
│   │   │   ├── text-area/
│   │   │   ├── number-input/
│   │   │   └── index.ts                   # Barrel export for all UI components
│   │   │
│   │   ├── layout/
│   │   │   ├── shell/                      # App shell (sidebar + content)
│   │   │   │   ├── shell.component.ts
│   │   │   │   ├── shell.component.html
│   │   │   │   └── shell.component.scss
│   │   │   ├── nav-sidebar/
│   │   │   │   ├── nav-sidebar.component.ts
│   │   │   │   ├── nav-sidebar.component.html
│   │   │   │   └── nav-sidebar.component.scss
│   │   │   ├── toolbar/
│   │   │   ├── page-header/
│   │   │   ├── content-section/
│   │   │   └── auth-layout/               # Minimal layout for login page
│   │   │       ├── auth-layout.component.ts
│   │   │       ├── auth-layout.component.html
│   │   │       └── auth-layout.component.scss
│   │   │
│   │   ├── features/                       # Feature pages (lazy-loaded)
│   │   │   ├── dashboard/
│   │   │   │   ├── dashboard.component.ts
│   │   │   │   ├── dashboard.component.html
│   │   │   │   ├── dashboard.component.scss
│   │   │   │   ├── dashboard.store.ts
│   │   │   │   └── components/
│   │   │   │       ├── stats-cards/        # System health / quick stats
│   │   │   │       ├── activity-feed/      # Unified recent activity stream
│   │   │   │       ├── jobs-overview/      # Job status grid
│   │   │   │       └── quick-actions/      # Common action shortcuts
│   │   │   │
│   │   │   ├── logs/
│   │   │   │   ├── logs.component.ts
│   │   │   │   ├── logs.component.html
│   │   │   │   ├── logs.component.scss
│   │   │   │   └── logs.store.ts
│   │   │   │
│   │   │   ├── events/
│   │   │   │   ├── events.component.ts
│   │   │   │   ├── events.component.html
│   │   │   │   ├── events.component.scss
│   │   │   │   └── events.store.ts
│   │   │   │
│   │   │   ├── settings/
│   │   │   │   ├── general/
│   │   │   │   │   ├── general-settings.component.ts
│   │   │   │   │   ├── general-settings.component.html
│   │   │   │   │   ├── general-settings.component.scss
│   │   │   │   │   └── general-settings.store.ts
│   │   │   │   ├── queue-cleaner/
│   │   │   │   ├── malware-blocker/
│   │   │   │   ├── download-cleaner/
│   │   │   │   ├── blacklist-sync/
│   │   │   │   ├── arr/                    # Single unified arr settings component
│   │   │   │   │   ├── arr-settings.component.ts
│   │   │   │   │   ├── arr-settings.component.html
│   │   │   │   │   ├── arr-settings.component.scss
│   │   │   │   │   ├── arr-settings.store.ts
│   │   │   │   │   └── components/         # Arr-specific sub-components
│   │   │   │   │       ├── instance-form/
│   │   │   │   │       ├── instance-list/
│   │   │   │   │       └── rules-config/
│   │   │   │   ├── download-clients/
│   │   │   │   └── notifications/
│   │   │   │
│   │   │   └── auth/                       # Future: authentication pages
│   │   │       ├── login/
│   │   │       │   ├── login.component.ts
│   │   │       │   ├── login.component.html
│   │   │       │   └── login.component.scss
│   │   │       └── auth.routes.ts
│   │   │
│   │   ├── shared/                         # Shared non-visual utilities
│   │   │   ├── models/
│   │   │   │   ├── config.models.ts
│   │   │   │   ├── arr.models.ts
│   │   │   │   ├── download-client.models.ts
│   │   │   │   ├── notification.models.ts
│   │   │   │   ├── event.models.ts
│   │   │   │   └── job.models.ts
│   │   │   ├── pipes/
│   │   │   │   ├── relative-time.pipe.ts
│   │   │   │   └── byte-size.pipe.ts
│   │   │   ├── directives/
│   │   │   │   ├── numeric-input.directive.ts
│   │   │   │   └── click-outside.directive.ts
│   │   │   ├── validators/
│   │   │   │   └── url.validator.ts
│   │   │   ├── animations/
│   │   │   │   ├── fade.animation.ts
│   │   │   │   ├── slide.animation.ts
│   │   │   │   └── glass.animation.ts
│   │   │   └── utils/
│   │   │       ├── form.utils.ts
│   │   │       └── array.utils.ts
│   │   │
│   │   ├── app.component.ts
│   │   ├── app.config.ts
│   │   └── app.routes.ts
│   │
│   ├── styles/                             # Global styles
│   │   ├── _variables.scss                 # All SCSS variables (colors, spacing, radii, etc.)
│   │   ├── _tokens.scss                    # CSS custom properties generated from variables
│   │   ├── _typography.scss                # Font faces, text styles
│   │   ├── _glass.scss                     # Glassmorphism mixin library
│   │   ├── _animations.scss                # Keyframe animations & transitions
│   │   ├── _reset.scss                     # CSS reset / normalize
│   │   ├── _scrollbar.scss                 # Custom scrollbar styling
│   │   ├── _themes.scss                    # Light & dark theme token definitions
│   │   └── styles.scss                     # Entry point: imports all above
│   │
│   ├── assets/
│   │   ├── fonts/                          # Inter & JetBrains Mono (self-hosted)
│   │   └── images/
│   │
│   ├── environments/
│   │   ├── environment.ts
│   │   └── environment.prod.ts
│   │
│   ├── index.html
│   └── main.ts
│
├── angular.json
├── tsconfig.json
├── tsconfig.app.json
├── tsconfig.spec.json
├── tailwind.config.ts                      # TailwindCSS v4 config
├── eslint.config.js
├── .prettierrc
├── package.json
└── vitest.config.ts
```

### Key Structural Decisions

1. **`ui/` = Spartan helm + glassmorphism styling**: Each component in `ui/` wraps a Spartan brain primitive with our custom glassmorphism styles. The brain handles behavior and accessibility; our SCSS handles the look.

2. **SCSS variables cascade**: `_variables.scss` defines all SCSS variables. `_tokens.scss` maps them to CSS custom properties. `_themes.scss` overrides CSS custom properties for light/dark. Component `.scss` files use these CSS custom properties directly - they never hardcode a color, spacing, or radius value.

3. **Feature folders are self-contained**: Each feature has its own store, sub-components, and styles. No cross-feature imports.

4. **Barrel exports**: `ui/index.ts` exports all UI components for clean imports: `import { ButtonComponent, CardComponent } from '@app/ui'`

---

## 4. Design System & Theming

### SCSS Variable Architecture

The variable system has three layers:

```
Layer 1: _variables.scss    →  SCSS variables (compile-time, the single source of truth)
Layer 2: _tokens.scss       →  CSS custom properties derived from SCSS variables (runtime)
Layer 3: _themes.scss       →  Theme-specific overrides of CSS custom properties
```

#### Layer 1: SCSS Variables (`_variables.scss`)
```scss
// Brand colors (Cleanuparr purple palette)
$brand-50:  #f3e8ff;
$brand-100: #e9d5ff;
$brand-200: #d8b4fe;
$brand-300: #c084fc;
$brand-400: #a855f7;
$brand-500: #7E57C2;    // Primary brand color
$brand-600: #6D28D9;
$brand-700: #5B21B6;
$brand-800: #4C1D95;
$brand-900: #3B0764;
$brand-950: #1e0038;

// Semantic colors
$color-success: #22c55e;
$color-warning: #f59e0b;
$color-error:   #ef4444;
$color-info:    #3b82f6;

// Spacing scale
$space-1:  0.25rem;   // 4px
$space-2:  0.5rem;    // 8px
$space-3:  0.75rem;   // 12px
$space-4:  1rem;      // 16px
$space-5:  1.25rem;   // 20px
$space-6:  1.5rem;    // 24px
$space-8:  2rem;      // 32px
$space-10: 2.5rem;    // 40px
$space-12: 3rem;      // 48px

// Border radius
$radius-sm:   0.375rem;  // 6px
$radius-md:   0.5rem;    // 8px
$radius-lg:   0.75rem;   // 12px
$radius-xl:   1rem;      // 16px
$radius-2xl:  1.5rem;    // 24px
$radius-full: 9999px;

// Glass effect values
$glass-blur:   12px;
$glass-blur-sm: 8px;
$glass-blur-lg: 20px;

// Transitions
$transition-fast:   150ms ease;
$transition-normal: 250ms ease;
$transition-slow:   400ms ease;

// Typography
$font-family:  'Inter', system-ui, -apple-system, sans-serif;
$font-mono:    'JetBrains Mono', 'Fira Code', monospace;
$font-size-xs:   0.75rem;
$font-size-sm:   0.875rem;
$font-size-base: 1rem;
$font-size-lg:   1.125rem;
$font-size-xl:   1.25rem;
$font-size-2xl:  1.5rem;
$font-size-3xl:  1.875rem;

// Sidebar
$sidebar-width:           260px;
$sidebar-collapsed-width: 64px;
$toolbar-height:          56px;
```

#### Layer 2: CSS Custom Properties (`_tokens.scss`)
```scss
@use 'variables' as *;

:root {
  // Brand
  --brand-50:  #{$brand-50};
  --brand-100: #{$brand-100};
  // ... all brand colors mapped

  // Spacing (mapped from SCSS vars)
  --space-1:  #{$space-1};
  --space-2:  #{$space-2};
  // ... all spacing mapped

  // Radius
  --radius-sm:  #{$radius-sm};
  --radius-md:  #{$radius-md};
  // ... all radii mapped

  // Typography
  --font-family: #{$font-family};
  --font-mono:   #{$font-mono};

  // Transitions
  --transition-fast:   #{$transition-fast};
  --transition-normal: #{$transition-normal};
  --transition-slow:   #{$transition-slow};

  // Glass
  --glass-blur:    #{$glass-blur};
  --glass-blur-sm: #{$glass-blur-sm};
  --glass-blur-lg: #{$glass-blur-lg};
}
```

#### Layer 3: Theme Definitions (`_themes.scss`)
```scss
// Dark theme (default)
:root,
[data-theme="dark"] {
  // Surfaces
  --surface-ground:   #0c0614;
  --surface-section:  #140b22;
  --surface-card:     rgba(20, 11, 34, 0.75);
  --surface-overlay:  rgba(12, 6, 20, 0.90);
  --surface-elevated: rgba(30, 18, 50, 0.65);

  // Glass
  --glass-bg:           rgba(255, 255, 255, 0.04);
  --glass-bg-hover:     rgba(255, 255, 255, 0.07);
  --glass-bg-active:    rgba(255, 255, 255, 0.10);
  --glass-border:       rgba(255, 255, 255, 0.08);
  --glass-border-hover: rgba(255, 255, 255, 0.15);
  --glass-shadow:       0 8px 32px rgba(0, 0, 0, 0.4);

  // Text
  --text-primary:   rgba(255, 255, 255, 0.92);
  --text-secondary: rgba(255, 255, 255, 0.60);
  --text-tertiary:  rgba(255, 255, 255, 0.40);
  --text-disabled:  rgba(255, 255, 255, 0.25);

  // Primary (purple, matching brand)
  --color-primary:       var(--brand-500);
  --color-primary-hover: var(--brand-400);
  --color-primary-text:  #ffffff;
  --color-primary-subtle: rgba(126, 87, 194, 0.15);

  // Sidebar
  --sidebar-bg: linear-gradient(180deg, #1a0e2e 0%, #0c0614 100%);
  --sidebar-item-hover: rgba(126, 87, 194, 0.12);
  --sidebar-item-active: rgba(126, 87, 194, 0.20);

  // Scrollbar
  --scrollbar-track: transparent;
  --scrollbar-thumb: rgba(255, 255, 255, 0.12);
  --scrollbar-thumb-hover: rgba(255, 255, 255, 0.20);

  // Input
  --input-bg:       rgba(255, 255, 255, 0.04);
  --input-border:   rgba(255, 255, 255, 0.10);
  --input-focus:    var(--brand-500);
  --input-placeholder: rgba(255, 255, 255, 0.30);
}

// Light theme
[data-theme="light"] {
  // Surfaces
  --surface-ground:   #f5f0fa;
  --surface-section:  #ede5f7;
  --surface-card:     rgba(255, 255, 255, 0.70);
  --surface-overlay:  rgba(245, 240, 250, 0.92);
  --surface-elevated: rgba(255, 255, 255, 0.80);

  // Glass
  --glass-bg:           rgba(255, 255, 255, 0.50);
  --glass-bg-hover:     rgba(255, 255, 255, 0.65);
  --glass-bg-active:    rgba(255, 255, 255, 0.75);
  --glass-border:       rgba(126, 87, 194, 0.12);
  --glass-border-hover: rgba(126, 87, 194, 0.22);
  --glass-shadow:       0 8px 32px rgba(126, 87, 194, 0.08);

  // Text
  --text-primary:   rgba(12, 6, 20, 0.90);
  --text-secondary: rgba(12, 6, 20, 0.60);
  --text-tertiary:  rgba(12, 6, 20, 0.40);
  --text-disabled:  rgba(12, 6, 20, 0.25);

  // Primary
  --color-primary:       var(--brand-600);
  --color-primary-hover: var(--brand-700);
  --color-primary-text:  #ffffff;
  --color-primary-subtle: rgba(126, 87, 194, 0.10);

  // Sidebar (deep purple even in light mode for brand identity)
  --sidebar-bg: linear-gradient(180deg, #2d1a4e 0%, #1a0e2e 100%);
  --sidebar-item-hover: rgba(255, 255, 255, 0.10);
  --sidebar-item-active: rgba(255, 255, 255, 0.18);

  // Scrollbar
  --scrollbar-track: transparent;
  --scrollbar-thumb: rgba(12, 6, 20, 0.15);
  --scrollbar-thumb-hover: rgba(12, 6, 20, 0.25);

  // Input
  --input-bg:       rgba(255, 255, 255, 0.65);
  --input-border:   rgba(126, 87, 194, 0.18);
  --input-focus:    var(--brand-600);
  --input-placeholder: rgba(12, 6, 20, 0.35);
}
```

> **Design note on light theme**: The sidebar stays dark purple in both themes - this keeps the brand identity strong and is a common pattern (Discord, Slack, etc. keep their sidebar dark regardless of theme). The content area switches between dark and light.

### Glass Mixin Library (`_glass.scss`)
```scss
@mixin glass($level: 'base') {
  @if $level == 'subtle' {
    background: var(--glass-bg);
    border: 1px solid var(--glass-border);
  } @else if $level == 'base' {
    background: var(--glass-bg);
    backdrop-filter: blur(var(--glass-blur));
    -webkit-backdrop-filter: blur(var(--glass-blur));
    border: 1px solid var(--glass-border);
    border-radius: var(--radius-lg);
    box-shadow: var(--glass-shadow);
  } @else if $level == 'elevated' {
    background: var(--surface-elevated);
    backdrop-filter: blur(var(--glass-blur-lg));
    -webkit-backdrop-filter: blur(var(--glass-blur-lg));
    border: 1px solid var(--glass-border);
    border-radius: var(--radius-lg);
    box-shadow: var(--glass-shadow), 0 0 60px rgba(126, 87, 194, 0.05);
  }
}

@mixin glass-hover {
  transition: all var(--transition-normal);
  &:hover {
    background: var(--glass-bg-hover);
    border-color: var(--glass-border-hover);
  }
}

@mixin glass-focus {
  &:focus-visible {
    outline: 2px solid var(--color-primary);
    outline-offset: 2px;
  }
}
```

### Reduced Motion / Performance Mode
```scss
:root[data-reduce-effects="true"],
@media (prefers-reduced-motion: reduce) {
  --glass-blur: 0px;
  --glass-blur-sm: 0px;
  --glass-blur-lg: 0px;
  --glass-shadow: none;
  --transition-fast: 0ms;
  --transition-normal: 0ms;
  --transition-slow: 0ms;

  * {
    backdrop-filter: none !important;
    -webkit-backdrop-filter: none !important;
  }
}
```

### Theme Service
```typescript
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private _theme = signal<'dark' | 'light'>('dark');
  private _reduceEffects = signal(false);

  readonly theme = this._theme.asReadonly();
  readonly reduceEffects = this._reduceEffects.asReadonly();

  constructor() {
    // Restore from localStorage
    const saved = localStorage.getItem('cleanuparr-theme');
    if (saved === 'light' || saved === 'dark') this._theme.set(saved);

    const reducedMotion = localStorage.getItem('cleanuparr-reduce-effects');
    if (reducedMotion === 'true') this._reduceEffects.set(true);

    // Also respect OS preference
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      this._reduceEffects.set(true);
    }

    // Apply to DOM
    effect(() => {
      document.documentElement.setAttribute('data-theme', this._theme());
    });
    effect(() => {
      document.documentElement.setAttribute(
        'data-reduce-effects',
        String(this._reduceEffects())
      );
    });
  }

  toggleTheme(): void { /* ... */ }
  setReduceEffects(value: boolean): void { /* ... */ }
}
```

### Iconography
- **Tabler Icons** via `@ng-icons/core` + `@ng-icons/tabler-icons` packages
- Wrapped in an `<app-icon>` component that provides consistent sizing, color inheritance, and accessibility
- Icons use `currentColor` so they follow the text color of their parent
- **Arr application icons** (Sonarr, Radarr, Lidarr, Readarr, Whisparr) use the actual SVG logos from the old frontend, stored in `public/icons/ext/`. Both light (sidebar) and colored (hover) variants are available.

### Responsive Design
- **Single breakpoint: 768px** - Simpler to maintain, covers the desktop/mobile split well
- Desktop (>768px): Fixed sidebar, full layout
- Mobile (<=768px): Sidebar becomes an overlay drawer with backdrop, hamburger toggle in toolbar, auto-close on route change
- Dashboard grid uses `minmax(min(350px, 100%), 1fr)` for natural responsive behavior without media queries
- Content padding reduces from `space-6` to `space-4` on mobile

---

## 5. Component Architecture

### How Spartan UI Components Work

Each UI component follows this pattern:

```
Spartan brain (npm)  →  Handles behavior, a11y, keyboard, focus
        ↓
Our helm wrapper     →  Wraps brain with our glassmorphism styling
(in ui/ directory)       Each has .ts, .html, .scss
        ↓
Feature pages        →  Import and use our styled components
```

**Example: Dropdown**
```
@spartan-ng/brain/select     →  Headless select behavior (keyboard, ARIA, overlay)
ui/dropdown/                 →  Our glassmorphism-styled wrapper
features/settings/general/   →  Uses <app-dropdown> in templates
```

### Component Rules
1. **Standalone** - Every component is `standalone: true`
2. **Self-contained styles** - Each `.scss` uses CSS custom properties (from `_tokens.scss`) and glass mixins (from `_glass.scss`)
3. **Inputs via signals** - Use `input()` and `model()` signal APIs
4. **Outputs via output()** - Use the `output()` function
5. **OnPush change detection** - All components use `ChangeDetectionStrategy.OnPush`
6. **Brain for behavior** - Complex interaction patterns come from Spartan brain primitives
7. **SCSS variables for everything** - Never hardcode a color, spacing, radius, or timing value

### Example: Glass Card Component
```typescript
@Component({
  selector: 'app-card',
  standalone: true,
  templateUrl: './card.component.html',
  styleUrl: './card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CardComponent {
  header = input<string>();
  elevated = input(false);
  interactive = input(false);
}
```
```html
<div class="card" [class.elevated]="elevated()" [class.interactive]="interactive()">
  @if (header()) {
    <div class="card-header">{{ header() }}</div>
  }
  <div class="card-body">
    <ng-content />
  </div>
</div>
```
```scss
@use 'styles/glass' as *;

.card {
  @include glass('base');
  padding: var(--space-6);

  &.elevated {
    @include glass('elevated');
  }

  &.interactive {
    @include glass-hover;
    cursor: pointer;
  }
}

.card-header {
  font-size: var(--font-size-lg);
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: var(--space-4);
}

.card-body {
  color: var(--text-secondary);
}
```

### Example: Dropdown (wrapping Spartan brain)
```typescript
@Component({
  selector: 'app-dropdown',
  standalone: true,
  imports: [BrnSelectDirective, /* ... other brain directives */],
  templateUrl: './dropdown.component.html',
  styleUrl: './dropdown.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DropdownComponent<T> {
  options = input.required<T[]>();
  value = model<T>();
  label = input<string>();
  placeholder = input('Select...');
  displayWith = input<(item: T) => string>(String);
}
```

### UI Component Inventory

#### Form Controls
| Component | Spartan Brain | Description |
|-----------|--------------|-------------|
| `button` | `brn-button` | Primary, secondary, ghost, icon-only, destructive |
| `input` | - | Text input with label, validation states |
| `number-input` | - | Numeric-only input |
| `text-area` | - | Multi-line text input |
| `dropdown` | `brn-select` | Select with search, glass overlay |
| `checkbox` | `brn-checkbox` | Checkbox with label |
| `toggle` | `brn-switch` | Toggle switch |
| `tag` | - | Tag/chip with optional remove |

#### Data Display
| Component | Spartan Brain | Description |
|-----------|--------------|-------------|
| `table` | `brn-table` | Data table with sorting, virtual scroll |
| `card` | - | Glass card container |
| `badge` | `brn-badge` | Status badge / counter |
| `empty-state` | - | Illustrated empty state |
| `skeleton` | - | Loading skeleton |
| `progress-bar` | `brn-progress` | Determinate/indeterminate progress |

#### Feedback
| Component | Spartan Brain | Description |
|-----------|--------------|-------------|
| `toast` | `brn-toast` | Toast notification system |
| `spinner` | - | Loading spinner |
| `confirm-dialog` | `brn-alert-dialog` | Confirmation modal |
| `modal` | `brn-dialog` | General-purpose glass modal |
| `tooltip` | `brn-tooltip` | Hover tooltip |

#### Navigation
| Component | Spartan Brain | Description |
|-----------|--------------|-------------|
| `tabs` | `brn-tabs` | Tab navigation |
| `accordion` | `brn-accordion` | Collapsible sections |
| `menu` | `brn-menu` | Dropdown action menu |
| `sidebar` | `brn-sheet` | Slide-out panel |

---

## 6. State Management

### Strategy: Signals + NgRx Signal Stores

```
Local UI state      →  Angular Signals (signal(), computed(), effect())
Feature state       →  @ngrx/signals (signalStore)
Async data streams  →  RxJS Observables (converted to signals at boundaries)
Real-time data      →  SignalR → Observables → Signals
```

This is the same proven pattern from the current frontend, which works well. We keep it.

### Signal Store Pattern (per feature)
```typescript
// features/settings/general/general-settings.store.ts

type GeneralSettingsState = {
  config: GeneralConfig | null;
  loading: boolean;
  saving: boolean;
  error: string | null;
};

export const GeneralSettingsStore = signalStore(
  { providedIn: 'root' },
  withState<GeneralSettingsState>({
    config: null,
    loading: false,
    saving: false,
    error: null,
  }),
  withComputed((store) => ({
    isReady: computed(() => !store.loading() && store.config() !== null),
    hasError: computed(() => store.error() !== null),
  })),
  withMethods((store, api = inject(ConfigurationApi)) => ({
    load: rxMethod<void>(
      pipe(
        tap(() => patchState(store, { loading: true, error: null })),
        switchMap(() => api.getGeneralConfig()),
        tap((config) => patchState(store, { config, loading: false })),
        catchError((err) => {
          patchState(store, { loading: false, error: err.message });
          return EMPTY;
        })
      )
    ),
    save: rxMethod<GeneralConfig>(/* ... */),
    updateLocally(patch: Partial<GeneralConfig>) {
      const current = store.config();
      if (current) {
        patchState(store, { config: { ...current, ...patch } });
      }
    },
  })),
  withHooks({
    onInit(store) {
      store.load();
    },
  })
);
```

### Real-Time State
```typescript
// core/realtime/app-hub.service.ts
@Injectable({ providedIn: 'root' })
export class AppHubService {
  private _logs = signal<LogEntry[]>([]);
  private _events = signal<AppEvent[]>([]);
  private _jobs = signal<JobStatus[]>([]);
  private _connectionStatus = signal<ConnectionStatus>('disconnected');

  readonly logs = this._logs.asReadonly();
  readonly events = this._events.asReadonly();
  readonly jobs = this._jobs.asReadonly();
  readonly connectionStatus = this._connectionStatus.asReadonly();
}
```

---

## 7. Routing & Navigation

### Route Structure
```typescript
export const routes: Routes = [
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard.component')
          .then(m => m.DashboardComponent),
      },
      {
        path: 'logs',
        loadComponent: () => import('./features/logs/logs.component')
          .then(m => m.LogsComponent),
      },
      {
        path: 'events',
        loadComponent: () => import('./features/events/events.component')
          .then(m => m.EventsComponent),
      },
      {
        path: 'settings',
        children: [
          {
            path: 'general',
            loadComponent: () => import('./features/settings/general/general-settings.component')
              .then(m => m.GeneralSettingsComponent),
            canDeactivate: [pendingChangesGuard],
          },
          {
            path: 'queue-cleaner',
            loadComponent: () => import('./features/settings/queue-cleaner/queue-cleaner.component')
              .then(m => m.QueueCleanerComponent),
            canDeactivate: [pendingChangesGuard],
          },
          {
            path: 'malware-blocker',
            loadComponent: () => import('./features/settings/malware-blocker/malware-blocker.component')
              .then(m => m.MalwareBlockerComponent),
            canDeactivate: [pendingChangesGuard],
          },
          {
            path: 'download-cleaner',
            loadComponent: () => import('./features/settings/download-cleaner/download-cleaner.component')
              .then(m => m.DownloadCleanerComponent),
            canDeactivate: [pendingChangesGuard],
          },
          {
            path: 'blacklist-sync',
            loadComponent: () => import('./features/settings/blacklist-sync/blacklist-sync.component')
              .then(m => m.BlacklistSyncComponent),
            canDeactivate: [pendingChangesGuard],
          },
          {
            path: 'arr/:type',   // sonarr | radarr | lidarr | readarr | whisparr
            loadComponent: () => import('./features/settings/arr/arr-settings.component')
              .then(m => m.ArrSettingsComponent),
            canDeactivate: [pendingChangesGuard],
          },
          {
            path: 'download-clients',
            loadComponent: () => import('./features/settings/download-clients/download-clients.component')
              .then(m => m.DownloadClientsComponent),
            canDeactivate: [pendingChangesGuard],
          },
          {
            path: 'notifications',
            loadComponent: () => import('./features/settings/notifications/notifications.component')
              .then(m => m.NotificationsComponent),
            canDeactivate: [pendingChangesGuard],
          },
        ],
      },
    ],
  },
  {
    path: 'auth',
    component: AuthLayoutComponent,
    children: [
      {
        path: 'login',
        loadComponent: () => import('./features/auth/login/login.component')
          .then(m => m.LoginComponent),
      },
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];
```

### Sidebar Navigation (nested, collapsible settings)
```
Dashboard
Logs
Events
─────────────
Settings
  ├── General
  ├── Queue Cleaner
  ├── Malware Blocker
  ├── Download Cleaner
  ├── Blacklist Sync
  ├── Arr Applications
  │     ├── Sonarr
  │     ├── Radarr
  │     ├── Lidarr
  │     ├── Readarr
  │     └── Whisparr
  ├── Download Clients
  └── Notifications
```

Settings are nested under `/settings/*` to keep the sidebar organized. The "Settings" section in the sidebar is collapsible.

### Dynamic Arr Route
A single `ArrSettingsComponent` reads the `:type` parameter and adapts for Sonarr/Radarr/Lidarr/Readarr/Whisparr. Shared sub-components (`instance-form`, `instance-list`, `rules-config`) are reused across all arr types.

---

## 8. API & Real-Time Communication

### API Layer - Split by Domain

**No monolithic API services.** Each domain gets its own API service, mirroring the backend controller structure. This keeps services focused, testable, and easy to find.

| API Service | Backend Controller | Responsibility |
|------------|-------------------|----------------|
| `general-config.api.ts` | `ConfigurationController` | General app configuration |
| `queue-cleaner.api.ts` | `QueueCleanerController` | Queue cleaner settings |
| `malware-blocker.api.ts` | `MalwareBlockerController` | Malware blocker settings |
| `download-cleaner.api.ts` | `DownloadCleanerController` | Download cleaner settings |
| `blacklist-sync.api.ts` | `BlacklistSyncController` | Blacklist sync settings |
| `arr.api.ts` | `SonarrController`, `RadarrController`, etc. | All arr instance CRUD + config |
| `download-client.api.ts` | `DownloadClientController` | Download client CRUD + config |
| `notification.api.ts` | `NotificationController` | Notification provider CRUD + config |
| `jobs.api.ts` | `JobsController` | Job invocation |
| `events.api.ts` | `EventsController` | Events/manual events |
| `system.api.ts` | `SystemController` | Health, version, status |

```typescript
// Example: general-config.api.ts (focused, single-responsibility)
@Injectable({ providedIn: 'root' })
export class GeneralConfigApi {
  private http = inject(HttpClient);
  private basePath = inject(APP_BASE_PATH);

  get(): Observable<GeneralConfig> {
    return this.http.get<GeneralConfig>(`${this.basePath}/api/configuration/general`);
  }

  save(config: GeneralConfig): Observable<void> {
    return this.http.post<void>(`${this.basePath}/api/configuration/general`, config);
  }
}

// Example: arr.api.ts (handles all arr types via parameter)
@Injectable({ providedIn: 'root' })
export class ArrApi {
  private http = inject(HttpClient);
  private basePath = inject(APP_BASE_PATH);

  getConfig(type: ArrType): Observable<ArrConfig> {
    return this.http.get<ArrConfig>(`${this.basePath}/api/configuration/${type}`);
  }

  saveConfig(type: ArrType, config: ArrConfig): Observable<void> {
    return this.http.post<void>(`${this.basePath}/api/configuration/${type}`, config);
  }

  testInstance(type: ArrType, instance: ArrInstance): Observable<TestConnectionResult> {
    return this.http.post<TestConnectionResult>(`${this.basePath}/api/${type}/test`, instance);
  }
}
```

Each feature store injects only the API service it needs - no importing a massive shared service.

Same backend API contract. No backend changes needed.

### SignalR Hub
Same `/hubs/app` contract. The service:
1. Connects with automatic reconnection (exponential backoff, 2s initial, 30s max)
2. Exposes data via readonly signals
3. Manages connection lifecycle
4. Buffers messages (max 1000 items, same as current)

---

## 9. Authentication Preparation

### Dual Layout System
```
/auth/*    → AuthLayoutComponent (centered glass card, no sidebar)
/*         → ShellComponent (sidebar + toolbar + content)
```

### Auth Service (currently permissive)
```typescript
@Injectable({ providedIn: 'root' })
export class AuthService {
  private _isAuthenticated = signal(true);  // Always true for now
  private _user = signal<User | null>(null);

  readonly isAuthenticated = this._isAuthenticated.asReadonly();
  readonly user = this._user.asReadonly();

  login(credentials: LoginCredentials): Observable<AuthResult> {
    return of({ success: true }); // Placeholder
  }

  logout(): void { /* Placeholder */ }
}
```

### Auth Guard (currently allows all)
```typescript
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isAuthenticated() ? true : router.createUrlTree(['/auth/login']);
};
```

### What's Needed to Enable Auth Later
1. Implement `AuthService.login()` with credentials or Plex OAuth
2. Add token storage
3. Activate the HTTP interceptor to attach tokens and handle 401s
4. Everything else (guard, routes, layouts) is already wired

---

## 10. Performance & Accessibility

### Performance
- **Zoneless change detection** (Angular 21 default) - no zone.js overhead
- **Lazy loading** all feature routes
- **OnPush** change detection everywhere
- **Virtual scrolling** for logs, events, large tables (via Angular CDK)
- **Reduce effects toggle** - disables all GPU-intensive effects
- **`prefers-reduced-motion`** - automatic OS detection
- **Glass blur capped at 12-20px** - keeps GPU usage reasonable
- **Service Worker** for caching static assets (PWA)
- **Spartan brain** primitives are tree-shakeable - only what we use gets bundled

### Accessibility
- Spartan brain primitives handle ARIA, keyboard navigation, and focus management
- Semantic HTML (`<nav>`, `<main>`, `<section>`, `<header>`)
- Focus-visible outlines (purple ring matching brand)
- Color contrast meeting WCAG AA in both themes (4.5:1 for text, 3:1 for large text)
- Screen reader announcements for toasts and live regions
- Reduce effects toggle for motion sensitivity
- `@angular/aria` can be evaluated if needed for additional accessibility primitives

---

## 11. Testing Strategy

### Framework: Vitest (Angular 21 default)

### What to Test
| Layer | What | How |
|-------|------|-----|
| UI components | Rendering, inputs, outputs, a11y | Component tests with Angular Testing Library |
| Stores | State transitions, computed values | Unit tests calling store methods |
| Services | API calls, hub connection | Unit tests with mocked HttpClient |
| Pipes/Directives | Transformations, DOM behavior | Unit tests |
| Integration | Page workflows | Component tests with stores + mocked APIs |

### Coverage Goals
- UI components: Every variant and state
- Stores: Every method and edge case
- Services: Happy path + error handling

---

## 12. Migration & Deployment

### Development Approach
- Build `frontend-v2` independently while `frontend` continues to serve production
- Backend API remains unchanged - both frontends consume the same endpoints
- Both can run simultaneously on different ports during development

### Switching Over
When `frontend-v2` reaches feature parity:
1. Build `frontend-v2` and output to `code/ui/` (same output directory the backend serves)
2. Update Dockerfile to use `frontend-v2`
3. Archive `frontend` directory

---

## 13. Implementation Phases

### Phase 1: Foundation & Scaffolding ✅
- [x] Scaffold Angular 21 project in `code/frontend-v2/`
- [x] Install and configure: @ng-icons/tabler-icons, Inter font, SCSS design system (Spartan UI and TailwindCSS dropped in favor of custom lightweight components)
- [x] Set up build tooling (ESLint, Vitest)
- [x] Create global styles: `_variables.scss`, `_tokens.scss`, `_themes.scss`, `_glass.scss`, `_typography.scss`, `_reset.scss`, `_animations.scss`, `_scrollbar.scss`
- [x] Implement `ThemeService` (dark/light toggle + reduce effects)
- [x] Build core UI components: button, input, card, spinner, toast-container, icon, toggle
- [x] Build layout: shell, nav-sidebar, page-header
- [x] Set up routing skeleton with lazy loading
- [x] Wire up auth guard (permissive) and auth layout placeholder

### Phase 1.5: Design Review ✅
- [x] Run the app and present the shell layout (sidebar + toolbar + content area) in both themes
- [x] Show core UI component showcase (buttons, inputs, cards, toggles in glass style)
- [x] Validate glassmorphism look and feel in dark theme
- [x] Validate light theme readability and contrast
- [x] Validate reduced-effects mode (no blur, no animations)
- [x] Get user approval on visual direction before proceeding to feature implementation
- [x] Iterate on feedback: responsive sidebar, mobile overlay, sidebar section labels, arr SVG icons

### Phase 2: Core Infrastructure ✅
- [x] Implement domain-split API services (11 services: general-config, queue-cleaner, malware-blocker, download-cleaner, blacklist-sync, arr, download-client, notification, jobs, events, system)
- [x] Implement HTTP interceptors (error handling, base URL)
- [x] Implement SignalR hub service with signal-based state (AppHubService with auto-reconnect)
- [x] Build remaining UI components: select, number-input, textarea, chip-input, badge, accordion, modal, paginator, tabs, empty-state, loading-state, toast-container
- [x] Set up signal-based state patterns (component-local signals; NgRx signal stores deferred to Phase 5 if needed)

### Phase 3: Feature Pages ✅
- [x] Dashboard (manual events banner, recent logs/events timelines, jobs management with run-now)
- [x] Logs viewer (real-time SignalR, level filtering, search, expandable entries, copy)
- [x] Events viewer (server-side pagination, auto-polling, severity/type/search filters, expandable data)
- [x] General settings
- [x] Queue cleaner settings
- [x] Malware blocker settings
- [x] Download cleaner settings
- [x] Blacklist sync settings

### Phase 4: Complex Settings ✅
- [x] Unified arr settings (Sonarr/Radarr/Lidarr/Readarr/Whisparr via single component with `:type` param)
- [x] Download client settings (CRUD with modal, type selection, test connection)
- [x] Notification settings + provider modals (event flag toggles, Discord fully wired)

### Phase 5: Polish & Parity (IN PROGRESS)
- [x] Wire ToastContainer into app shell so toasts display
- [x] Wire ConfirmDialogComponent into app shell for destructive action confirmations
- [x] Unsaved changes guards (pendingChangesGuard with HasPendingChanges interface, JSON snapshot dirty tracking)
- [x] Confirmation dialogs for delete operations (arr instances, download clients, notifications)
- [x] Version display in sidebar footer (current version + update available link)
- [x] Support section on dashboard (GitHub, Discord, Donate - conditional on displaySupportBanner)
- [x] Feature parity verification against current frontend
- [x] Sidebar logo icon (reused 128.png from old frontend)
- [x] Mobile sidebar closes on nav link click (navClicked output + smooth transform transition)
- [x] Fix arr settings route param binding (input alias 'type' for `:type` route param)
- [x] ApplicationPathService (base-path.service.ts) for dev/prod API routing
- [x] Dynamic base path in index.html (server-injected `_server_base_path`, proper title/favicon/manifest/iOS meta)
- [x] SignalR hub URL respects base path (hub.service.ts uses ApplicationPathService.buildHubUrl)
- [x] PWA manifest (manifest.webmanifest with proper app metadata and icons)
- [x] Responsive fixes (toast container mobile width, filter toolbar wrapping, min-width reductions)
- [x] Accessibility audit and fixes:
  - Toast container: aria-live="polite", aria-atomic
  - Modal: aria-labelledby linking to title
  - Confirm dialog: aria-labelledby + aria-describedby
  - Spinner: uses CSS custom property for reduced motion support
  - Destructive button: uses --color-primary-text instead of hardcoded #fff
- [x] Light theme contrast improvements:
  - text-tertiary: 0.40 → 0.50 opacity (WCAG AA compliant)
  - text-disabled: 0.25 → 0.35 opacity
  - input-placeholder: 0.35 → 0.45 opacity
  - scrollbar-thumb: 0.12 → 0.20 opacity
- [x] DocumentationService ported (field-to-anchor mappings for all settings sections)
- [x] Fix spinner animation (component-scoped @keyframes for reliable rendering)
- [x] Fix SignalR hub lifecycle (moved start/stop to ShellComponent - no more race conditions)
- [x] Support banner card animations (staggered entrance + hover lift/glow + heart pulse)
- [x] Sidebar: "Become a Sponsor" link + "Suggested Apps" section (Huntarr)
- ~~Service Worker~~ Skipped (not needed - Cleanuparr always needs live backend)
- [ ] Performance profiling (glass effects on lower-end devices)
- [ ] Migration to production (update Dockerfile to build frontend-v2 instead of frontend)

---

## 14. Decisions Log

All major decisions have been resolved:

| Decision | Choice | Rationale |
|----------|--------|-----------|
| UI component framework | **Spartan UI** | Headless primitives (brain) for behavior + a11y; we own the styling. shadcn/ui approach for Angular. Signal-native, zoneless-ready, 50+ components. |
| CSS framework | **TailwindCSS v4** + **SCSS** | Tailwind for utility classes + Spartan integration. SCSS for glass mixins, variables, and component-scoped styles. |
| Icon library | **Tabler Icons** | 5,900+ MIT-licensed icons, consistent design, ideal for dashboards, Angular packages available. |
| Font | **Inter** + **JetBrains Mono** | Inter designed for screens/UI, excellent at small sizes. JetBrains Mono for logs and code. |
| Theme | **Dark (default) + Light** | Both themes use Cleanuparr's purple brand. Sidebar stays dark purple in both. |
| Navigation | **Nested `/settings/*`** | Settings grouped under collapsible section to avoid crowded flat sidebar. |
| Arr settings | **Single unified component** | One `ArrSettingsComponent` with `:type` param, reducing duplication from 5 components to 1. |
| State management | **@ngrx/signals** | Keep the proven NgRx signal store pattern from current frontend. |
| Dashboard | **Redesigned** | New layout with stats cards, activity feed, jobs grid, quick actions. Better than current. |
| Testing | **Vitest** | Angular 21 default. Faster than Jasmine/Karma. |
| PrimeNG | **Dropped** | Replaced by Spartan UI + custom glassmorphism styles. Full control. |
| API services | **Split by domain** | One API service per backend controller. No monolithic ConfigurationService. Each feature injects only what it needs. |
| Design checkpoint | **Phase 1.5** | Theme and layout must be approved before building feature pages. Avoids rework. |
| Responsive breakpoints | **Single 768px** | One breakpoint for desktop/mobile split. Simpler to maintain, covers the important transition point. |
| Mobile sidebar | **Overlay drawer + backdrop** | Sidebar slides in from left as overlay on mobile, with dismiss-on-backdrop-tap and auto-close on navigation. |
| Arr icons | **SVG files from old frontend** | Reuse the actual Sonarr/Radarr/etc. logos (light + colored variants) rather than generic Tabler icons. |
| Sidebar section labels | **Dedicated CSS variable** | Section labels (SETTINGS, Arr Applications) use `--sidebar-section-label` instead of `--text-tertiary` to stay readable on the dark sidebar in both themes. |

---

## References

- [Angular v21 Release](https://blog.angular.dev/announcing-angular-v21-57946c34f14b)
- [Angular 21 - What's New](https://angular.love/angular-21-whats-new/)
- [Angular Signals Guide](https://angular.dev/guide/signals)
- [Spartan UI](https://www.spartan.ng/)
- [Spartan UI - Getting Started](https://dev.to/this-is-angular/getting-started-with-spartanui-shadcn-like-ui-components-for-angular-8df)
- [Tabler Icons](https://github.com/tabler/tabler-icons)
- [Angular Best Practices 2026](https://www.ideas2it.com/blogs/angular-development-best-practices)
- [Dark Glassmorphism UI in 2026](https://medium.com/@developer_89726/dark-glassmorphism-the-aesthetic-that-will-define-ui-in-2026-93aa4153088f)
- [Glassmorphism Implementation Guide](https://playground.halfaccessible.com/blog/glassmorphism-design-trend-implementation-guide)
