# REVIEW.md — Self-Review & Deferred Work (Phase 1)

## Status

- Tests: **60/60 hijau** — via `ModuleInitializer` di `AssemblyInfo.cs`
  (`DOTNET_USE_POLLING_FILE_WATCHER=1` otomatis untuk semua run; lihat R-05).
- Build: Release bersih, 1 warning di IdentityServer (CS9107, tidak fatal).
- Tag: `assessment-start` / `phase-1-complete` dibuat setelah komit perbaikan ini.

## Findings

Format: ID · severity · finding · action · status · evidence.

| ID | Sev | Finding | Action | Status | Evidence |
|----|-----|---------|--------|--------|----------|
| R-01 | High | DB `.db` terlacak git (`accessrequesthub.db`, `AccessRequestHub.IdentityServer/accessrequesthub.db`) — melanggar aturan "tidak ada file DB di repo" (AC-DONE-02) | Untrack + abaikan via `.gitignore` | Fixed | `git ls-files \| grep .db` → kosong |
| R-02 | High | `ClientId` Microsoft + `DevelopmentAccounts:Passwords.Default=Password123!` di `appsettings.json` (bukan user-secrets) — `ClientSecret` Microsoft juga tidak ikut dipindah | Pindah keduanya ke user-secrets/env; secret hanya lewat konfig aman; seeder dev skip bila password tak dikonfigurasi | Fixed | `AccessRequestHub.IdentityServer/appsettings.json` — `Passwords:{}` + tanpa `ClientId` |
| R-03 | Med | Path basi di README: `tests/…`, `AccessRequestHub.Persitence` (typo), `home-test-agit.csproj` | Koreksi ke struktur proyek aktual (`*.Tests/…`, `AccessRequestHub.Persistence`) | Fixed | `README.md` — grep pola lama → kosong |
| R-04 | Med | `AI_USAGE.md` pernah jadi salinan `PLAN.md`; sekarang sudah ditulis ulang | Verifikasi isinya beda; biarkan | Fixed | `AI_USAGE.md` |
| R-05 | Med | Run paralel penuh gagal spurious `IOException` (inotify `max_user_instances=128`): tiap `HubAppFactory` per test membuka watcher | `ModuleInitializer` di `AssemblyInfo.cs` set `DOTNET_USE_POLLING_FILE_WATCHER=1` untuk semua run — tanpa env manual | Fixed | 60/60 hijau di run standar |
| R-06 | High | `UseExceptionHandler("/Home/Error")` + `AccessDeniedPath="/Home/Forbidden"` menunjuk ke `HomeController` yang **tidak ada**; view `Home/Error.cshtml`, `Home/Forbidden.cshtml` absen (`Forbidden`/`NotFound` ada di `Shared/`) → 500 tak tertangani gagal render | `HomeController` + `Views/Shared/Error.cshtml` dibuat; `HomeEndpointsTests` (`/Home/Error`, `/Home/Forbidden`) hijau | Fixed | `AccessRequestHub.Web/Controllers/HomeController.cs`, `Views/Shared/Error.cshtml` |
| R-07 | Low | Trigger append-only **tidak** di migration (rencana D-08/ADR-008 bilang via `migrationBuilder.Sql`) — dipasang saat startup via `AuditAppendOnlyGuard` | Update DECISIONS/README supaya sesuai; DB tanpa bootstrapper tidak punya trigger | Open | `grep trg_audit Migrations/*.cs` → 0; `DatabaseBootstrapper.cs` |
| R-08 | Low | `specs/PLAN.md` sempat hilang dari working tree (file lain utuh); isinya duplikat kandidat | Dipulihkan via `git checkout -- specs/PLAN.md`; `specs/` memang terlacak (5 file) | Fixed | `git ls-files \| grep specs/` → 5 file |

## Findings yang sudah benar (konfirmasi)

- Idempotency race-safe: `UNIQUE (RequesterId, ClientRequestId)` + `IsClientRequestIdViolation` → `Replay()` / 409 `idempotency_mismatch`.
- Concurrency: conditional `ExecuteUpdateAsync` + `rows==0` → 409; audit atomic dalam transaksi sama.
- Error mapping 404→403→400→409; 404 untuk non-pemilik (tidak bocorkan keberadaan).
- Audit append-only dua lapis (tanpa update/delete endpoint + trigger `RAISE(ABORT)`).
- Otorisasi server-side; actor selalu dari principal (`arh_user_id`), bukan payload.

## Known limitations

- **SQLite single-writer** — penulisan konkuren di-serialize (`Default Timeout=30` + transaksi pendek). Uji concurrency membuktikan logika, bukan throughput (ADR-001).
- **Duplikat bisnis** lintas `ClientRequestId` berbeda tidak dicegah (D-18).
- **ClientRequestId** unik per requester, bukan global (D-06) — mencegah user lain menabrak ID.
- **System Owner self-request** ditolak saat create (D-12), bukan tertahan.
- **Header `X-Demo-User`** hanya aktif bila `Auth:AllowHeader=true` (D-16); jangan aktifkan di production.
- **Trigger append-only** bergantung pada `DatabaseBootstrapper` (R-07).
- **Tidak ada production-grade** Identity DB migration/deployment, signing key persistence, provisioning secret (perlu config per-env).

## Deferred work (di luar Phase 1)

- Cancel/withdraw request; backup/delegated approver (D-18, A7).
- Notifikasi email; SSO/OIDC tambahan di luar Microsoft.
- Pagination/filter lanjutan pada list (volume demo kecil).
- Multi-hop approval > 2 tahap; kebijakan selain `v1`.
- **Phase 2 apa pun** — menunggu Change Request assessor (§12).

## Pre-tag checklist (`phase-1-complete`)

1. R-01 untrack DB; R-02 pindah secret; R-03 koreksi README; R-05 polling watcher; R-06 perbaiki error path — **selesai**.
2. `dotnet test` hijau (60/60).
3. Tag `assessment-start` (jika belum) dan `phase-1-complete`.

## Catatan eksekusi perbaikan (R-01..R-06)

- `accessrequesthub.db` + `AccessRequestHub.IdentityServer/accessrequesthub.db` di-`git rm --cached` dan diabaikan `.gitignore`; keduanya tidak lagi terlacak.
- `appsettings.json` (IdentityServer) dikosongkan `DevelopmentAccounts.Passwords` ke `{}` dan dihapus `Microsoft.ClientId`; secret (client secret, password dev, `Microsoft:ClientId/ClientSecret`) tetap diberikan via user-secrets/AppHost env — tidak pernah di-commit.
- R-07 dibiarkan Open (bukan blocker tag): trigger tetap dipasang saat startup (`AuditAppendOnlyGuard`) — catatan arsitektur, bukan bug.
