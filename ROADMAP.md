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
- [x] **Phase 5: Reviews and reading log.**
  - Personal 1–5 stars, review, status (Want to read / Reading / Read) and read dates (day, month or just year) per book, visible to the other person.
  - Shelf shows your stars and status, pins books you're reading, filters by status and sorts by your rating.
  - Rules let each person write only their own reading, with emulator tests.
  - Reading lists: an ordered "to read next" list each (drag to reorder), with wishlist books we don't own yet.
- [x] **At a glance (dashboard):** my books finished this year (calendar year or last 12 months, set in Settings), per-year chart, "year in books", ratings breakdown, most-read authors, five-star books and fun facts; a summary strip on the shelf. Later: counts for lent/sold/Kindle and genres once those exist, and side-by-side stats for both of us.
- [x] **More bindings (themes):** Cloth-bound Green, Library at Night, Parchment (light) and Marbled Endpapers (light), alongside Leather & Gilt; each checked for readable contrast.
- [x] **Firebase budget alert** set up in the console, so costs never get silly.

## Later / ideas

- **Where a book is** (beyond on the shelf / wishlist):
  - **Sold or given away:** keep the book, its ratings and reading history, but take it off the shelf (e.g. "Sold, March 2027"), with a filter to see past books.
  - **Lent out / borrowed:** books we've lent (to whom, since when) and books we've borrowed (from whom, from a library, due back), with reminders and "Returned".
  - **Kindle (and other e-books):** a format on each book (paper, Kindle, audiobook) so e-books can be logged and rated without counting as on the shelf, plus a filter by format.
- **Synopsis for each book:** a short description on the book page.
  - Filled in automatically when a book is scanned or found online (Google Books gives a description for most books; Open Library for some).
  - Existing books get theirs on a one-off "fill in missing synopses" run, when online.
  - Editable by hand (for books not found online, or to shorten a long blurb); searchable, with a "Read more" for long ones.
- **Genres and tags:**
  - **Genre** on each book (e.g. Crime, Fantasy, History, Cookery, Children's), suggested automatically from Google Books categories / Open Library subjects when a book is scanned or found online, and editable.
  - A tidy, fixed genre list so similar online categories ("Fiction / Mystery & Detective", "Detective and mystery stories") land in one genre.
  - Free-form **tags** too (e.g. "Signed", "Kids", "Loft box 3", "Christmas"), several per book.
  - Filter and browse the shelf by genre or tag, and "fill in missing genres" for books already on the shelf.
- Owner can remove a member, rename the library, or transfer ownership.
- Live updates (Firestore listeners) instead of syncing on start, after edits, on reconnect and with "Sync now".
- Export or backup of the collection (CSV or JSON).
- Bulk "retry lookup" for books flagged as not found.
- "Take cover photo" straight from the scan list for books not found online; shelf filter for books without covers.
- Show the other person's stars on the shelf too, or a "we both loved it" filter.
- Barcode scanning on iPhone (needs a JS barcode library, as Safari has no `BarcodeDetector`).
