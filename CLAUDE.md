# Working on Bookshelf

- Follow the README for stack, structure, sync design and deployment.
- Workflow agreed with Chris: when a change is ready, push the branch, **open a PR to `main` without asking**, and watch it (CI results and review comments) until it's merged. Fix CI failures on the PR yourself.
- Run `dotnet test` before pushing; run `tests/firestore-rules` (`npm ci && npm test`, needs Java) whenever `firestore.rules` or `storage.rules` changes. In a proxied sandbox, unset `JAVA_TOOL_OPTIONS` and the `*_PROXY` variables for that run.
- When rules change, remind Chris to publish them in the Firebase console (Firestore → Rules, Storage → Rules).
- Buttons and links back to the shelf must use `Href="./"`, never `Href=""` (MudBlazor treats an empty Href as "not a link").
