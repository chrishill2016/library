# Bookshelf

A family library app for keeping track of the books we own. It's an installable, offline-first Progressive Web App styled like an old leather-bound book.

**Today (Phase 1):** add, edit and remove books; search by title, author or ISBN; sort by title (ignoring "The/A/An"), author surname or date added. Everything is stored on the device and works offline. See [ROADMAP.md](ROADMAP.md) for what's next (shared Firebase sync, barcode scanning, cover photos, ratings).

## Stack

- Blazor WebAssembly (standalone), .NET 10 LTS, csharp
- MudBlazor UI with a pluggable theme registry (`Theming/ThemeRegistry.cs`)
- IndexedDB for local storage, with an outbox queue for sync
- xUnit and bUnit tests
- GitHub Pages hosting, deployed by GitHub Actions

## Project layout

```
src/Bookshelf/
  Features/Books/       Shelf, details and edit pages; BookService; search and ISBN helpers
  Features/Settings/    Theme picker, sync, about
  Components/           Sync status indicator, update banner
  Services/Storage/     ILocalStore + IndexedDB implementation (wwwroot/js/local-store.js)
  Services/Sync/        ISyncService, IRemoteStore (not configured until Phase 2)
  Services/Browser/     Connectivity and service-worker update hooks (wwwroot/js/browser.js)
  Theming/              AppTheme, ThemeRegistry, ThemeService
  Models/               Book, OutboxEntry, SyncState
tests/Bookshelf.Tests/  xUnit + bUnit tests
.github/workflows/      ci.yml (PRs) and deploy.yml (main → GitHub Pages)
```

## How sync works

Every change is written to IndexedDB first, together with an entry in a local **outbox** (one entry per book, written in the same transaction). Deletes are soft (`IsDeleted`) so they can sync.

`SyncService` runs when the app starts, after every local change, when the device comes back online, and on demand from "Sync now". Each sync:

1. **Pulls** remote changes since the last cursor. If the remote copy has a newer `LastModified`, it replaces the local one and any queued local edit is dropped (last write wins).
2. **Pushes** whatever is left in the outbox, and only clears entries that weren't re-edited while the push was in flight.

Until Firebase is wired up in Phase 2, `NotConfiguredRemoteStore` is registered. Changes stay safely queued, and the header shows "On device" with the pending count.

## Themes

Themes are called "bindings" in the UI. To add one:

1. Add a factory method in `Theming/ThemeRegistry.cs` that returns an `AppTheme` (key, name, description, `IsDark`, swatches, `MudTheme`).
2. Add it to `ThemeRegistry.All`.
3. Optionally add `.theme-<key>` texture rules in `wwwroot/css/app.css`.

The selected theme is saved per device.

## Run locally

Requires the .NET 10 SDK.

```bash
dotnet run --project src/Bookshelf      # then open the URL it prints
dotnet test                             # run all tests
```

Offline caching and the "New version available" prompt only apply to published builds (`service-worker.published.js`). In development, the service worker does nothing.

## Deployment

- **Pull requests to `main`:** restore, build and test (`ci.yml`).
- **Push to `main`:** build, test, publish and deploy to GitHub Pages (`deploy.yml`). The workflow:
  - stamps the version as `1.0.<run number>` (shown in the footer and Settings);
  - sets `<base href>` to `/<repo-name>/`;
  - copies `index.html` to `404.html` so deep links work;
  - adds `.nojekyll`;
  - refreshes `index.html`'s hash in the service worker manifest.

One-time setup: in the repo, go to **Settings → Pages → Build and deployment** and set the source to **GitHub Actions**.

## Fonts

EB Garamond and Cormorant Garamond are self-hosted in `wwwroot/fonts` so they work offline. Both are under the SIL Open Font License (see the licence files in that folder).
