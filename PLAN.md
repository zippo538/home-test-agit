# PLAN.md — Access Request Hub (Phase 1)

## 1. Pemahaman Masalah

Permintaan akses aplikasi internal saat ini lewat email/chat. Tiga masalah nyata yang harus dijawab MVP:

1. **Tidak terlacak** → butuh single source of truth: request, approval, audit trail.
2. **Request terkirim dua kali** → butuh idempotency pada create.
3. **Dua approver bertindak bersamaan** → butuh optimistic concurrency; hanya satu transisi per version yang berhasil.

Non-goal Phase 1: SSO, email nyata, cloud, microservices, broker, Kubernetes (§12). Tidak ada cancel/withdraw dan backup approver (§10.7).

> **Catatan status:** dokumen ini adalah rencana awal (pra-implementasi). Ringkasan akhir
> apa yang benar-benar dibangun + perubahan plan ada di `REVIEW.md`; test akhir **60/60 hijau**.

## 2. Arsitektur

ASP.NET Core MVC (.NET 10), satu proses, satu database SQLite. Dua permukaan:

- **MVC + Razor Views + Bootstrap 5** — UI manusia (user switcher, form, list, inbox, detail + timeline).
- **JSON API** — dipakai UI via fetch untuk approve/reject, sekaligus kontrak yang bisa diuji dan didemokan langsung.

```
Views (Razor + Bootstrap)
        │  form post / fetch JSON
        ▼
Controllers  ──  tipis: bind, panggil service, map hasil → HTTP status
        ▼
Application/Domain  ──  AccessRequestService, ApprovalPolicy (isHighRisk + routing),
        │               AuthorizationService  ← SATU tempat aturan bisnis
        ▼
Data (EF Core + LINQ, SQLite)
        │  AppDbContext, repository, transaction
        ▼
SQLite (accessrequest.db)  ── constraint DB sebagai jaring terakhir
```

Prinsip: controller tidak mengandung aturan bisnis. `IsHighRisk` dan routing hanya hidup di `ApprovalPolicy`.

## 3. Data Model

**User** — `Id`, `Email` (unique), `DisplayName`, `ManagerId` (FK self, nullable), `Role` (`Requester`/`Manager`/`SystemOwner`/`Admin`).

**Application** — `Id`, `Name` (unique), `OwnerUserId` (FK User).

**Environment** / **AccessLevel** — master kecil, `Name` (unique). Disimpan sebagai tabel (bukan enum C#) agar dropdown dan validasi server membaca sumber yang sama.

**AccessRequest**

| Kolom | Catatan |
|-------|---------|
| `Id` | PK |
| `ClientRequestId` | unique bersama `RequesterId` |
| `RequesterId` | FK User |
| `ApplicationId` | FK Application |
| `EnvironmentId` / `AccessLevelId` | FK master |
| `Justification` | NOT NULL |
| `Status` | `PendingManager`/`PendingSystemOwner`/`Approved`/`Rejected` |
| `PolicyVersion` | NOT NULL, `v1` |
| `Version` | concurrency token, NOT NULL |
| `CreatedAt`, `UpdatedAt` | |

Constraints:
- `UNIQUE (RequesterId, ClientRequestId)` ← inti idempotency (§9).
- FK ke User/Application/master, `ON DELETE RESTRICT`.
- `CHECK` status ∈ himpunan nilai valid → invariant state dijaga DB.

**AuditEvent** (append-only) — `Id`, `RequestId` (FK), `ActorUserId` (FK), `Action` (`Created`/`ManagerApproved`/`ManagerRejected`/`SystemOwnerApproved`/`SystemOwnerRejected`), `StatusFrom` (nullable), `StatusTo`, `Version`, `Reason` (nullable), `PolicyVersion`, `Timestamp`.

Append-only ditegakkan dua lapis: (a) tidak ada endpoint update/delete; (b) trigger SQLite `BEFORE UPDATE` / `BEFORE DELETE` pada tabel audit yang `RAISE(ABORT, ...)`.

## 4. Urutan Implementasi

Mengikuti §13:

1. `git init`, commit scaffold, tag `assessment-start`, tulis PLAN.md.
2. Paket NuGet `Microsoft.EntityFrameworkCore.Sqlite` + `Design`; schema + constraint + seed reproducible.
3. Domain: `ApprovalPolicy` (isHighRisk + routing), state machine, `AuthorizationService`.
4. API: create (idempoten), list/detail, inbox, approve, reject; error mapping 400/403/404/409.
5. Test backend: standard, high-risk, authorization, idempotency, concurrency — dijalankan sebelum UI.
6. Frontend minimal: user switcher → form → my requests → inbox → detail + timeline.
7. Dokumentasi (README, AI_USAGE, REVIEW, INTEGRITY), lalu tag `phase-1-complete`.

## 5. Strategi Test

- **Unit** — `ApprovalPolicy`: tabel kebenaran `IsHighRisk` (4 kombinasi Environment × AccessLevel) dan routing tiap hasil.
- **Integration** (WebApplicationFactory + SQLite) — endpoint nyata; assert status code + state akhir + baris audit.
- Setiap skenario §7 punya satu test bernama sesuai `AC-DEMO-*`.
- **Concurrency** — kirim dua approve dengan Version sama; assert tepat satu 200 dan satu 409, dan `Version` naik satu (bukan dua).
- **Idempotency** — create dua kali dengan `ClientRequestId` sama; assert 1 baris, response identik. Varian race: dua task paralel → assert tetap 1 baris (unique constraint).
- DB per test: SQLite file temporer, di-seed ulang, agar tidak ada urutan test yang bocor.

Command ditulis di README.md: `dotnet test`.

## 6. Trade-off

**T6.1 — Unique constraint DB vs cek-lalu-insert di aplikasi.**
Dipilih constraint DB. Cek-lalu-insert punya TOCTOU window; dua request paralel bisa sama-sama lolos cek. Constraint adalah satu-satunya cara yang benar terhadap race (§9, AC-CREATE-04). Konsekuensi: harus menangkap `DbUpdateException`/`SqliteException` violation dan mengembalikan request asli alih-alih error.

**T6.2 — Conditional UPDATE (`ExecuteUpdate`) vs EF change tracking.**
Dipilih `ExecuteUpdate ... WHERE Id AND Version` + cek rows affected. Change tracking dengan concurrency token juga valid, tetapi rows-affected eksplisit membuat semantik "0 berarti 409" terbaca langsung di kode dan mudah diuji. Konsekuensi: perlu re-read entity setelah update untuk response.

**T6.3 — SQLite vs PostgreSQL.**
Dipilih SQLite: nol setup, reproducible untuk assessor, memenuhi §8 ("relational DB"). Konsekuensi: penulisan konkuren di-serialize, jadi uji concurrency mengukur logika optimistic concurrency, bukan throughput. Trade-off dicatat di REVIEW.md.

**T6.4 — MVC server-rendered vs SPA.**
Dipilih Razor + Bootstrap: sudah ada di template, minim dependensi, fokus waktu ke correctness. Approve/reject tetap lewat endpoint JSON agar jalur otorisasi seragam dan bisa diuji seperti API.

## 7. Asumsi (§10 — keputusan yang diambil)

Ringkas; detail dan konsekuensi di DECISIONS.md.

| # | Asumsi |
|---|--------|
| 1 | Hanya user yang punya Manager yang boleh create request. |
| 2 | System Owner yang mengajukan request ke aplikasinya sendiri: tidak boleh self-approve, tidak ada fallback → request tertahan di `PendingSystemOwner`. Dicatat di REVIEW.md + UI menampilkan badge "no eligible approver". |
| 3 | `ClientRequestId` unik **per requester** (bukan global). |
| 4 | Retry dengan payload berbeda tapi `ClientRequestId` sama → kembalikan request asli, payload baru diabaikan (idempotency menang). |
| 5 | Pesan 409 dibedakan: `TERMINAL_STATE` vs `STALE_VERSION`. |
| 6 | Duplikat bisnis (ClientRequestId beda, akses sama) **tidak** dilarang → limitation. |
| 7 | Tanpa cancel/withdraw dan backup approver (deferred). |
| 8 | Isi audit event: actor, action, status from→to, version, reason, timestamp, policy version. |
| 9 | Visibilitas: requester miliknya; approver sesuai tahap + scope; Erin semua. |

## 8. Perubahan Plan

Log keputusan yang berubah dari rencana awal selama pengerjaan (§11 "perubahan plan").

| Waktu | Perubahan | Alasan |
|-------|-----------|--------|
| _(diisi saat implementasi)_ | | |

**Log aktual** ada di `REVIEW.md` ("Catatan eksekusi perbaikan") dan ringkasan
perubahan desain (IdentityServer, Aspire, split Models/Persistence, trigger via
bootstrapper, owner self-request ditolak saat create) direkonstruksi di
`AI_USAGE.md` (I-1..I-5) dan `README.md` (bagian 9–11).
