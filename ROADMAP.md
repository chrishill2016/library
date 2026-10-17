# Roadmap

## Phases

- [x] **Phase 1: The bookshelf.** Add, edit, delete, search and sort books. Offline-first storage, Leather & Gilt theme with a theme registry, sync status indicator, update prompt, CI/CD.
- [x] **Phase 2: Sign-in and sharing.**
  - Google sign-in (required) and Firestore sync with a server-time cursor.
  - "The Hill Library" shared by invite.
  - Security rules with emulator tests in CI.
- [x] **Phase 3: Scan to add.**
  - Camera barcode scanning with rapid (undo) and confirm modes, and duplicate warnings.
  - Google Books + Open Library lookup.
  - Offline scans filled in automatically later.
  - Find online by title/author, and covers cached for offline use.
- [x] **Phase 4: Cover photos.**
  - Photo from the book page or edit form (camera or gallery), cropped by dragging corners from a guessed outline and straightened, shrunk to about 800px JPEG.
  - Works offline, uploads to Firebase Storage, and syncs to the other phone.
  - Storage rules allow members only, with emulator tests.
- [ ] **Phase 5: Reviews and reading log.** Personal 1–5 star rating, review and "read on" dates for each of us per book, visible to the other.

## Later / ideas

- Shelves or tags (e.g. "Fiction", "Kids", "Loft box 3") and filtering by them.
- More themes ("bindings"): Parchment (light), Library at Night, Cloth-bound Green.
- Firebase budget alert, so costs never get silly.
- Owner can remove a member, rename the library, or transfer ownership.
- Live updates (Firestore listeners) instead of syncing on start, after edits, on reconnect and with "Sync now".
- Export or backup of the collection (CSV or JSON).
- Bulk "retry lookup" for books flagged as not found.
- "Take cover photo" straight from the scan list for books not found online; shelf filter for books without covers.
- Barcode scanning on iPhone (needs a JS barcode library, as Safari has no `BarcodeDetector`).
