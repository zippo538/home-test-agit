# TASKS.md — Task Breakdown

Dependency: T1 → T2 → T3 → T4 → T5 → T6 → T7. T5 dapat jalan paralel dengan T6 setelah T4 selesai. Estimasi jam kerja efektif; total ± 5 jam, di dalam timebox 4–8 jam (§12).

---

## T1 — Bootstrap repository & tag awal (0.25 h)
- `git init`, `.gitignore` (.NET + `*.db`), commit scaffold.
- Tulis `PLAN.md` versi awal.
- Tag `assessment-start`.
- **DoD:** tag ada, working tree bersih, tidak ada credential atau data nyata (§12).

## T2 — Schema, constraint, seed reproducible (0.75 h)
- Paket `Microsoft.EntityFrameworkCore.Sqlite` + `Design`.
- Entity: User, Application, Environment, AccessLevel, AccessRequest, AuditEvent.
- Konfigurasi: unique `(RequesterId, ClientRequestId)`, FK, `CHECK` status & action, trigger append-only (ADR-008).
- Migration awal + seeder (5 user, 2 aplikasi, 2 environment, 2 access level, PolicyVersion `v1`).
- **DoD:** hapus DB → `dotnet ef database update` → data demo kembali (AC-DB-01..04). Trigger terbukti menolak UPDATE audit (uji manual dicatat sebagai evidence di REVIEW.md).

## T3 — Domain logic (1 h)
- `ApprovalPolicy`: `IsHighRisk`, `NextAfterManagerApprove`, `NextAfterSystemOwnerApprove`.
- `RequestStateMachine`: state valid, `IsTerminal`, transisi diizinkan/tidak.
- `AuthorizationService`: `CanApprove`, `CanReject`, `CanView`, `GetStage`; cek self-approve (rule 6), direct-report (rule 4), app ownership (rule 5).
- **DoD:** unit test tabel kebenaran (4 kombinasi Environment × AccessLevel) + tiap aturan otorisasi lulus. Tidak ada aturan bisnis di controller.

## T4 — API + error mapping (1 h)
- Create (idempoten, ADR-003), list/detail, approval inbox, approve, reject (ADR-004).
- Envelope error + urutan 404 → 403 → 409 (ADR-007).
- Logging terstruktur per transisi.
- **DoD:** semua skenario §7 dapat dipicu lewat HTTP dengan status code sesuai; audit tercatat atomik (AC-AUDIT-01).

## T5 — Test backend (1 h)
- Integration test (WebApplicationFactory + SQLite temporer): `AC-DEMO-01..07`, `AC-TEST-03..05`.
- Test race idempotency: dua create paralel → 1 baris.
- Test concurrency: dua approve Version sama → satu 200, satu 409, Version naik 1.
- **DoD:** `dotnet test` hijau; command ditulis di README.md (AC-TEST-06).

## T6 — Frontend (1 h)
- `AccountController` user switcher; middleware memaksa pilih user (AC-AUTH-01).
- Form create (AC-UI-02) — hidden `ClientRequestId` di-generate saat render.
- My Requests list + detail (AC-UI-03/05) dengan audit timeline.
- Approval Inbox (AC-UI-04) — Manager & System Owner.
- State loading/empty/error/conflict (AC-UI-06); 409 ditampilkan sebagai alert, bukan stack trace.
- **DoD:** seluruh 7 skenario demo dapat diklik manual end-to-end.

## T7 — Dokumentasi & tag akhir (0.5 h)
- `README.md`: setup, run, migrate/seed, `dotnet test`, demo user, demo flow.
- `AI_USAGE.md`: 2–5 interaksi AI paling berpengaruh + section "Three things AI got wrong".
- `REVIEW.md`: self-review (finding, severity, action, status, evidence), known limitations (A2, A6, trade-off ADR-001), deferred work (A7).
- `INTEGRITY.md`: deklarasi ownership, tools AI, bantuan eksternal, template/snippet.
- Isi bagian "Perubahan Plan" di PLAN.md.
- Tag `phase-1-complete`.
- **DoD:** §11 lengkap; §14 terpenuhi.

---

## Peta Task → Acceptance

| Task | Menutup |
|------|---------|
| T2 | AC-DB-01..04 |
| T3 | AC-FLOW-01..09, AC-AUTH-05 |
| T4 | AC-CREATE-01..07, AC-CONC-01..04, AC-AUDIT-01..03, AC-AUTH-03/06 |
| T5 | AC-TEST-01..06, AC-DEMO-01..07 |
| T6 | AC-UI-01..06, AC-AUTH-01/04, AC-VIS-01..04 |
| T7 | AC-DONE-01..06 |

## Yang sengaja tidak dikerjakan (deferred)

- Cancel/withdraw request; backup atau delegated approver (A7).
- Notifikasi email; SSO/OIDC.
- Pagination/filter lanjutan pada list (volume demo kecil).
- Multi-hop approval lebih dari 2 tahap; kebijakan selain `v1`.
- **Phase 2 apa pun** — menunggu Change Request assessor (§12).
