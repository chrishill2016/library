# Bookshelf

A family library app for keeping track of the books we own. It's an installable, offline-first Progressive Web App styled like an old leather-bound book.

**Today:**
- Sign in with Google, then share one library with family. The owner invites people by email, and they join when they sign in.
- **Scan to add:** point the camera at the barcode and the book is looked up (title, author, publisher, year, cover) and added. There's a rapid mode with undo and a "confirm each book" mode. Duplicates are flagged, not re-added.
- **Find online** by title or author for books without a barcode, or look up an ISBN from the add form.
- **Cover photos:** photograph a cover when there's no artwork (or you prefer your own). The app guesses where the book is; drag the corners to adjust and it's straightened into a clean cover, shrunk to about 800px. Works offline and syncs to the other phone.
- Add, edit and remove books; search by title, author or ISBN; sort by title (ignoring "The/A/An"), author surname or date added.
- Everything is stored on the device and works offline. Changes sync through Firebase when online.

See [ROADMAP.md](ROADMAP.md) for what's next (ratings and the reading log).

## Stack

- Blazor WebAssembly (standalone), .NET 10 LTS, csharp
- MudBlazor UI with a pluggable theme registry (`Theming/ThemeRegistry.cs`)
- IndexedDB for local storage, with an outbox queue for sync
- Firebase: Google sign-in (Auth) and Firestore Lite as the shared cloud copy. The SDK is copied into `wwwroot/lib/firebase` by `tools/update-firebase.sh`, so the app works offline.
- xUnit and bUnit tests, plus Firestore security-rule tests against the emulator
- GitHub Pages hosting, deployed by GitHub Actions

## Project layout

```
src/Bookshelf/
  Features/Books/       Shelf, details and edit pages; BookService; search and ISBN helpers
  Features/Scan/        ScanPage, ScanService (rapid/confirm, duplicates, undo), BarcodeScanner (wwwroot/js/scanner.js)
  Features/Lookup/      Google Books + Open Library sources, BookLookupService (merge), PendingLookupService, Find online page
  Features/Photos/      PhotoService, PhotoButton, CoverCropper, IPhotoStore (wwwroot/js/photos.js, cover-geometry.js)
  Features/Account/     AuthService, sign-in screen
  Features/Library/     LibraryService (create, invite, join), setup screen, library settings
  Features/Settings/    Library, sync, theme, account, about
  Components/           AppGate (sign-in required), sync status indicator, update banner
  Services/Storage/     ILocalStore + IndexedDB implementation (wwwroot/js/local-store.js)
  Services/Sync/        ISyncService, IRemoteStore
  Services/Firebase/    FirebaseClient (wwwroot/js/firebase.js), FirestoreRemoteStore
  Services/Browser/     Connectivity and service-worker update hooks (wwwroot/js/browser.js)
  Theming/              AppTheme, ThemeRegistry, ThemeService
  Models/               Book, OutboxEntry, SyncState
tests/Bookshelf.Tests/  xUnit + bUnit tests
tests/firestore-rules/  Security rule tests for Firestore and Storage (Node + emulators)
firestore.rules         Firestore security rules
storage.rules           Storage security rules (cover photos)
.github/workflows/      ci.yml (PRs) and deploy.yml (main → GitHub Pages)
```

## How sync works

Every change is written to IndexedDB first, together with an entry in a local **outbox** (one entry per book, written in the same transaction). Deletes are soft (`IsDeleted`) so they can sync.

`SyncService` runs when the app starts, after every local change, when the device comes back online, and on demand from "Sync now". Each sync:

1. **Pulls** remote changes since the last cursor. If the remote copy has a newer `LastModified`, it replaces the local one and any queued local edit is dropped (last write wins).
2. **Pushes** whatever is left in the outbox, and only clears entries that weren't re-edited while the push was in flight.

The remote is Firestore (`FirestoreRemoteStore`):
- **Pull cursor:** the newest server-side `syncedAt` seen, so device clocks never matter. Each pull re-reads the last two minutes to catch writes that committed slightly out of order.
- **When sync runs:** it starts once a signed-in user has a library.

## Scanning and book lookup

- **Scanning** uses the browser's `BarcodeDetector` (Chrome on Android) with the rear camera. Only EAN-13 "Bookland" codes (978/979 with a valid checksum) count, so price barcodes are ignored. Holding a book in view doesn't re-add it.
- **Lookup** asks Google Books and Open Library in parallel:
  - Google's details win, and Open Library fills the gaps (often the cover).
  - A source that's offline or over quota is skipped.
  - "Not found" is only reported when every source answered.
- **Offline or unknown books** are saved by ISBN (`LookupState`):
  - `pending` books are filled in automatically when the app opens or comes back online (`PendingLookupService`).
  - `notFound` books are flagged on the shelf so you can add details.
- **Covers** are stored as links. The service worker caches CORS-readable cover images (up to 2,000) so they show offline. API calls bypass the service worker.
- **Google Books API key:** `BookLookup:GoogleBooksApiKey` in `appsettings.json` (currently the Firebase browser key). The Books API must be enabled in the Google Cloud project, and allowed on that key, or Google requests fail and Open Library is used alone.

## Cover photos

- **Taking a photo:** the Photo button (book page and edit form) opens the camera directly; the gallery icon next to it picks an existing picture.
- **Cropping:** the photo opens full screen with four corner handles (`CoverCropper`, drawn by `photos.js` so dragging stays smooth).
  - The handles start on a guess (`cover-geometry.js`): on a 256px copy it finds strong straight edges on each side, picks the four that make the most convincing closed outline, then moves each side outwards past strong bands inside the cover (like a title strip). With no convincing outline it falls back to a centred 2:3 box.
  - "Whole photo" uses the full frame; "Guess again" goes back to the guess.
- **Processing** happens in `photos.js`:
  - EXIF rotation is applied, the outlined area is perspective-corrected into a flat rectangle (its size from the outline's sides) scaled to at most 800px, then saved as an 80% JPEG (typically 40–150 KB).
  - It's stored in IndexedDB (`photos` store) and shown from there.
- **Book field:** `PhotoId` names the photo; your own photo is shown instead of `CoverUrl`.
- **Upload:** photos attached to a saved book upload to `libraries/{libraryId}/covers/{photoId}.jpg` when online (`PhotoService`).
  - Uploads happen on save, on reconnect, when the library opens, and with "Sync now".
  - Photos taken but never saved are dropped after a day.
- **Replacing or removing:** a replaced or removed photo, or one belonging to a deleted book, is deleted from Storage. A tombstone record keeps the delete queued while offline.
- **Other phones** download a photo the first time it's shown and keep it in IndexedDB for offline use.

## Accounts and sharing

- Sign-in is required. A user's sign-in and library are remembered on the device, so the app opens offline.
- **Firestore layout:**
  - `libraries/{id}` holds `name`, `ownerUid`, `memberUids`, `invitedEmails` and `members` (uid → name and email).
  - Books are stored at `libraries/{id}/books/{bookId}`.
- **Owner:** can invite or cancel invites by email.
- **Invited person:** sees the invitation when they sign in. Joining adds only themselves, which the rules enforce.
- **Switching accounts:** if a different account signs in on the same device, local books are cleared and that account's library is downloaded.

## Firebase setup

- **Project:** `bookshelf-3a8ee`. The web config is in `src/Bookshelf/wwwroot/appsettings.json`; it's public by design, and the rules protect the data.
- **Authorized domains:** Authentication → Settings must include `chrishill2016.github.io` and `localhost`.
- **Deploying rules:**
  - Paste `firestore.rules` into Firestore → Rules → Publish, and `storage.rules` into Storage → Rules → Publish.
  - Or run `firebase deploy --only firestore:rules,storage`.
  - The Storage rules read library membership from Firestore. On first publish, the console asks to let Storage read Firestore: allow it.
- **Rules tests:** `cd tests/firestore-rules && npm ci && npm test` (needs Java). CI runs them on every PR and before every deploy.
  - In sandboxes with an HTTP proxy, run them with the proxy variables and `JAVA_TOOL_OPTIONS` unset. The Storage emulator's Firestore lookups otherwise fail.
- **Updating the SDK:** `tools/update-firebase.sh <version>`.
- **Local emulator testing:** add `"EmulatorHost": "127.0.0.1"` under `Firebase` in `appsettings.json` (don't commit it), then run `firebase emulators:start --only auth,firestore,storage`.

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

- **Pull requests to `main`:** restore, build, test and Firestore rule tests (`ci.yml`).
- **Push to `main`:** rule tests, then build, test, publish and deploy to GitHub Pages (`deploy.yml`). The workflow:
  - stamps the version as `1.0.<run number>` (shown in the footer and Settings);
  - sets `<base href>` to `/<repo-name>/`;
  - copies `index.html` to `404.html` so deep links work;
  - adds `.nojekyll`;
  - refreshes `index.html`'s hash in the service worker manifest.

One-time setup: in the repo, go to **Settings → Pages → Build and deployment** and set the source to **GitHub Actions**.

## Fonts

EB Garamond and Cormorant Garamond are self-hosted in `wwwroot/fonts` so they work offline. Both are under the SIL Open Font License (see the licence files in that folder).
