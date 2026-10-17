# Roadmap

## Phases

- [x] **Phase 1: The bookshelf.** Add, edit, delete, search and sort books. Offline-first storage, Leather & Gilt theme with a theme registry, sync status indicator, update prompt, CI/CD.
- [ ] **Phase 2: Sign-in and sharing.** Firebase project (new, dedicated), Google sign-in, Firestore remote store behind `IRemoteStore`, one shared library with both of us as members, invite by email. Firestore security rules so only members can read or write. The pull cursor should use a server timestamp so device clock differences can't hide changes.
- [ ] **Phase 3: Scan to add.** Scan ISBN barcodes with the camera using Chrome's `BarcodeDetector` (we're both on Android). Look up details and cover art from Open Library, with Google Books as a fallback. Also **search by title/author** for older books without barcodes.
- [ ] **Phase 4: Cover photos.** Photograph a cover when no artwork is found. Store it in Firebase Storage (we're on the Blaze plan), resized on the device to about 800px JPEG before upload, to keep storage and bandwidth costs small.
- [ ] **Phase 5: Reviews and reading log.** Personal 1–5 star rating, review and "read on" dates for each of us per book, visible to the other.

## Later / ideas

- Shelves or tags (e.g. "Fiction", "Kids", "Loft box 3") and filtering by them.
- More themes ("bindings"): Parchment (light), Library at Night, Cloth-bound Green.
- Firebase budget alert, so costs never get silly.
- Export or backup of the collection (CSV or JSON).
