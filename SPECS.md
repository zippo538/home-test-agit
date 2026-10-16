# Access Request Hub: SPECS

Stack: **ASP.NET Core MVC**, **EF Core + LINQ**, **SQLite**, **Razor views + Bootstrap 5**, **xUnit**.

Dokumen ini berisi lima bagian: Acceptance, Plan, Spec, Decision, Task.

> **Catatan istilah:** "Blade" adalah template engine Laravel (PHP) dan tidak bisa dipakai bersama LINQ (.NET). Padanan di .NET adalah **Razor**, jadi frontend memakai Razor view + Bootstrap. Keputusan ini dicatat sebagai D-01.

---

## 1. ACCEPTANCE

Format: Given / When / Then. Setiap AC dipetakan ke test di bagian 5.5.

### 1.1 Flow bisnis

| ID | Skenario | Given | When | Then |
|---|---|---|---|---|
| AC-01 | Standard request | Alice login | Alice buat CRM, NonProduction, Read; Bob approve | Status `Approved`, audit: Created, ManagerApproved |
| AC-02 | Production request | Alice login | Alice buat CRM, Production, Read; Bob approve | Status `PendingSystemOwner` (inbox Carol); Carol approve → `Approved` |
| AC-03 | Admin access | Alice login | Alice buat Finance Portal, NonProduction, Admin; Bob approve | Status `PendingSystemOwner` (inbox Dana, bukan Carol) |
| AC-04 | Unauthorized approval | Request `PendingManager` milik Alice | Carol / Dana / Erin / Alice memanggil approve | 403, state dan audit tidak berubah |
| AC-05 | Duplicate submit | Alice kirim `ClientRequestId` X | Kirim ulang, atau 2 request paralel dengan X | Tepat 1 baris di `AccessRequests`; retry mengembalikan request yang sama |
| AC-06 | Concurrent action | Request versi N | 2 action (approve/reject) paralel dengan `version=N` | Satu berhasil, satu mendapat 409 dengan pesan stale version |
| AC-07 | Rejected request | Request menunggu Bob | Bob reject dengan reason | Status `Rejected`, reason tersimpan, audit `Rejected`; approve/reject berikutnya ditolak 409 |

### 1.2 Aturan tambahan

| ID | Kriteria |
|---|---|
| AC-08 | Reject tanpa reason (kosong/whitespace) ditolak 400, state tidak berubah |
| AC-09 | Approve/reject pada status `Approved`/`Rejected` ditolak 409 (terminal) |
| AC-10 | Requester diambil dari user login; field requester di payload diabaikan |
| AC-11 | Justification kosong, Application/Environment/AccessLevel tidak valid ditolak 400 dengan pesan per-field |
| AC-12 | Requester tidak bisa approve request sendiri walau menjadi manager/System Owner (403) |
| AC-13 | Manager hanya bisa memproses direct report; System Owner hanya aplikasinya sendiri |
| AC-14 | Carol approve request CRM saat masih `PendingManager` ditolak 409 (belum gilirannya) |
| AC-15 | Setiap transisi menyimpan audit event dalam transaksi yang sama; jika audit gagal, state ikut rollback |
| AC-16 | `UPDATE`/`DELETE` pada `AuditEvents` ditolak oleh database (trigger) |
| AC-17 | `PolicyVersion` dan `IsHighRisk` tersimpan di request saat create |
| AC-18 | Erin dapat melihat semua request dan audit, tetapi approve/reject ditolak 403 |
| AC-19 | Alice hanya melihat request miliknya; mengakses request orang lain mendapat 404 |
| AC-20 | UI menampilkan state loading, empty, validation error, dan conflict yang jelas |
| AC-21 | Database dibuat ulang dari migration + seed dengan satu command; `dotnet test` lulus |

### 1.3 Definition of Done (Phase 1)

- AC-01 sampai AC-21 lulus (UI-only AC dicek manual dan dicatat di README).
- Tag `assessment-start` dan `phase-1-complete` ada.
- PLAN.md, AI_USAGE.md, REVIEW.md, INTEGRITY.md, README.md tersedia.

---

## 2. PLAN

### 2.1 Pemahaman masalah

Email/chat tidak bisa dilacak, request bisa dobel, dan dua approver bisa bertabrakan. Solusinya adalah satu sumber kebenaran dengan state machine ketat, idempotency, optimistic concurrency, dan audit atomic. Fokus penilaian: correctness dan ownership, bukan jumlah fitur.

### 2.2 Arsitektur

Satu proyek web monolit dengan pemisahan folder (tanpa microservice):

```
AccessRequestHub/
├─ src/AccessRequestHub.Web/
│  ├─ Domain/          # entity, enum, RiskPolicy, WorkflowRules (murni, tanpa EF)
│  ├─ Data/            # AppDbContext, Migrations, Seed, trigger SQL
│  ├─ Services/        # AccessRequestService (create, approve, reject, query)
│  ├─ Auth/            # DemoAuthHandler, CurrentUser
│  ├─ Controllers/     # Account, Requests, Approvals
│  ├─ Views/           # Razor + Bootstrap
│  └─ Program.cs
├─ tests/AccessRequestHub.Tests/   # xUnit + WebApplicationFactory
└─ README.md PLAN.md AI_USAGE.md REVIEW.md INTEGRITY.md SPECS.md
```

Alur request: `Controller → AccessRequestService → DbContext (LINQ) → SQLite`. Aturan routing dan high-risk hidup di `Domain/`, bukan di controller.

### 2.3 Urutan implementasi

1. Setup repo, tag `assessment-start`, PLAN.md awal.
2. Domain + schema + migration + seed + trigger audit.
3. Service: create (idempotent), approve, reject, query.
4. Auth simulasi + controller + error mapping.
5. Test backend (flow, authz, idempotency, concurrency, atomic, append-only).
6. Razor views (switcher, create, my requests, inbox, detail + timeline).
7. Dokumentasi, jalankan semua test, tag `phase-1-complete`.

### 2.4 Strategi test

Prioritas pada integration test via `WebApplicationFactory` dengan SQLite **file temporer** per test class (bukan in-memory bersama), supaya concurrency test memakai koneksi berbeda dan hasilnya realistis. Domain rules diuji sebagai unit test cepat.

### 2.5 Trade-off utama

| Trade-off | Pilihan | Alasan |
|---|---|---|
| Workflow engine generik vs aturan eksplisit | Aturan eksplisit di `WorkflowRules` | Scope kecil; Phase 2 bisa memperluas tanpa rewrite |
| API JSON + SPA vs MVC server-rendered | MVC Razor | Sesuai stack, lebih sedikit bagian bergerak, authz tetap di server |
| Concurrency via `rowversion` vs kolom `Version` | Kolom `Version` int manual | SQLite tidak punya rowversion; manual lebih eksplisit |
| Idempotency via cek-lalu-insert vs unique constraint | Unique constraint + tangkap violation | Aman terhadap race |

### 2.6 Risiko

| Risiko | Mitigasi |
|---|---|
| SQLite single-writer menyebabkan `database is locked` saat concurrency test | `Default Timeout=30` di connection string, transaksi pendek |
| Waktu habis | Backend + test dulu, UI belakangan, deferred dicatat di REVIEW.md |
| Kode AI tidak dipahami | Review manual, catat koreksi di AI_USAGE.md |

---

## 3. SPEC

### 3.1 Data model

**Users**

| Kolom | Tipe | Constraint |
|---|---|---|
| Id | INTEGER | PK |
| Email | TEXT | NOT NULL, UNIQUE |
| DisplayName | TEXT | NOT NULL |
| ManagerId | INTEGER | NULL, FK → Users(Id) |
| IsAuditor | INTEGER | NOT NULL DEFAULT 0 |

**Applications**

| Kolom | Tipe | Constraint |
|---|---|---|
| Id | INTEGER | PK |
| Name | TEXT | NOT NULL, UNIQUE |
| SystemOwnerId | INTEGER | NOT NULL, FK → Users(Id) |

**PolicyVersions**

| Kolom | Tipe | Constraint |
|---|---|---|
| Id | INTEGER | PK |
| Code | TEXT | NOT NULL, UNIQUE (`v1`) |
| IsActive | INTEGER | NOT NULL |

**AccessRequests**

| Kolom | Tipe | Constraint |
|---|---|---|
| Id | TEXT (Guid) | PK |
| ClientRequestId | TEXT (Guid) | NOT NULL |
| RequesterId | INTEGER | NOT NULL, FK → Users |
| ApplicationId | INTEGER | NOT NULL, FK → Applications |
| Environment | TEXT | NOT NULL, CHECK IN ('NonProduction','Production') |
| AccessLevel | TEXT | NOT NULL, CHECK IN ('Read','Admin') |
| Justification | TEXT | NOT NULL, CHECK (length(trim(Justification)) > 0) |
| Status | TEXT | NOT NULL, CHECK IN ('PendingManager','PendingSystemOwner','Approved','Rejected') |
| IsHighRisk | INTEGER | NOT NULL (snapshot saat create) |
| PolicyVersionId | INTEGER | NOT NULL, FK → PolicyVersions |
| RejectReason | TEXT | NULL |
| Version | INTEGER | NOT NULL DEFAULT 1 |
| CreatedAt / UpdatedAt | TEXT (UTC) | NOT NULL |

Constraint tambahan:
- `UNIQUE (RequesterId, ClientRequestId)` untuk idempotency.
- `CHECK (Status <> 'Rejected' OR length(trim(RejectReason)) > 0)`.
- Index pada `(Status, ApplicationId)` dan `RequesterId` untuk inbox dan My Requests.

**AuditEvents** (append-only)

| Kolom | Tipe | Constraint |
|---|---|---|
| Id | INTEGER | PK AUTOINCREMENT |
| RequestId | TEXT | NOT NULL, FK → AccessRequests |
| ActorId | INTEGER | NOT NULL, FK → Users |
| Action | TEXT | NOT NULL, CHECK IN ('Created','ManagerApproved','SystemOwnerApproved','Rejected') |
| FromStatus | TEXT | NULL (null saat Created) |
| ToStatus | TEXT | NOT NULL |
| RequestVersion | INTEGER | NOT NULL |
| PolicyVersionCode | TEXT | NOT NULL |
| Reason | TEXT | NULL |
| OccurredAt | TEXT (UTC) | NOT NULL |

Trigger (ditulis lewat `migrationBuilder.Sql`):

```sql
CREATE TRIGGER trg_audit_no_update BEFORE UPDATE ON AuditEvents
BEGIN SELECT RAISE(ABORT, 'AuditEvents is append-only'); END;

CREATE TRIGGER trg_audit_no_delete BEFORE DELETE ON AuditEvents
BEGIN SELECT RAISE(ABORT, 'AuditEvents is append-only'); END;
```

### 3.2 Seed

| Data | Nilai |
|---|---|
| Users | alice (manager: bob), bob, carol, dana, erin (IsAuditor) |
| Applications | CRM (owner carol), Finance Portal (owner dana) |
| PolicyVersions | `v1` aktif |

Seed memakai ID tetap lewat `HasData` supaya reproducible.

### 3.3 Domain rules

```csharp
// Domain/RiskPolicy.cs
public static bool IsHighRisk(Environment env, AccessLevel level)
    => env == Environment.Production || level == AccessLevel.Admin;
```

Tabel transisi (satu-satunya sumber kebenaran):

| Dari | Action | Syarat | Ke |
|---|---|---|---|
| PendingManager | Approve | actor = manager requester, bukan requester | Approved (non-high-risk) atau PendingSystemOwner (high-risk) |
| PendingManager | Reject | actor = manager requester, reason wajib | Rejected |
| PendingSystemOwner | Approve | actor = System Owner aplikasi, bukan requester | Approved |
| PendingSystemOwner | Reject | actor = System Owner aplikasi, reason wajib | Rejected |
| Approved / Rejected | apa pun | terminal | ditolak (409) |

Request baru selalu dimulai di `PendingManager`.

### 3.4 Urutan pengecekan (decision order)

Untuk approve/reject, service memeriksa berurutan:

1. Request ada, jika tidak → **404**.
2. Actor adalah requester → **403** (self-approval).
3. Actor bukan manager requester dan bukan System Owner aplikasi → **403** (tidak berhak).
4. Reject tanpa reason → **400**.
5. Status terminal → **409** `already_final`.
6. Status tidak sesuai peran actor (contoh Carol saat `PendingManager`) → **409** `not_your_stage`.
7. `Version` payload ≠ versi saat ini, atau conditional update gagal → **409** `stale_version`.

Alasan urutan: hak akses dicek sebelum state agar tidak membocorkan informasi ke user yang tidak berhak.

### 3.5 Implementasi inti (LINQ)

**Create idempotent**

```csharp
var existing = await db.AccessRequests
    .FirstOrDefaultAsync(r => r.RequesterId == user.Id && r.ClientRequestId == dto.ClientRequestId);
if (existing != null) return Replay(existing, dto);   // payload sama → hasil lama; beda → 409

try {
    await using var tx = await db.Database.BeginTransactionAsync();
    db.AccessRequests.Add(entity);
    db.AuditEvents.Add(createdEvent);
    await db.SaveChangesAsync();
    await tx.CommitAsync();
} catch (DbUpdateException ex) when (IsUniqueViolation(ex)) {
    // race: request lain menang → ambil yang sudah ada
    var winner = await db.AccessRequests.AsNoTracking()
        .FirstAsync(r => r.RequesterId == user.Id && r.ClientRequestId == dto.ClientRequestId);
    return Replay(winner, dto);
}
```

`SELECT` di awal hanya optimasi; **unique constraint** yang menjamin tidak ada baris ganda. `IsUniqueViolation` memeriksa `SqliteException.SqliteErrorCode == 19` (constraint) dengan extended code 2067.

**Approve/reject dengan optimistic concurrency**

```csharp
await using var tx = await db.Database.BeginTransactionAsync();

var rows = await db.AccessRequests
    .Where(r => r.Id == id && r.Version == expectedVersion && r.Status == fromStatus)
    .ExecuteUpdateAsync(s => s
        .SetProperty(r => r.Status, toStatus)
        .SetProperty(r => r.Version, r => r.Version + 1)
        .SetProperty(r => r.RejectReason, reason)
        .SetProperty(r => r.UpdatedAt, now));

if (rows == 0) throw new ConflictException("stale_version");

db.AuditEvents.Add(auditEvent);
await db.SaveChangesAsync();
await tx.CommitAsync();
```

Hanya satu update yang cocok dengan `Version == N`; pemanggil kedua mendapat `rows == 0`. Audit dan perubahan state satu transaksi sehingga atomic.

### 3.6 Authentication simulasi

- Halaman **User Switcher**: pilih seeded user, server menulis cookie auth (claim `userId`).
- `DemoAuthHandler` membaca cookie; header `X-Demo-User: <email>` hanya aktif jika `Auth:AllowHeader=true` (dipakai test, default `false`).
- Authorization tidak bergantung pada UI: semua pengecekan ada di `AccessRequestService` dan `[Authorize]`.
- Form POST memakai antiforgery token.

### 3.7 Routes dan view

| Method | Route | Akses | Keterangan |
|---|---|---|---|
| GET/POST | `/Account/Switch` | Publik (demo) | User switcher |
| GET | `/Requests` | Login | My Requests (milik sendiri) |
| GET | `/Requests/All` | Auditor | Semua request |
| GET/POST | `/Requests/Create` | Requester (punya manager) | Form create |
| GET | `/Requests/{id}` | Pemilik, manager, SO terkait, auditor | Detail + audit timeline |
| GET | `/Approvals` | Manager / SO | Inbox sesuai peran |
| POST | `/Requests/{id}/Approve` | Approver | Body: `version` |
| POST | `/Requests/{id}/Reject` | Approver | Body: `version`, `reason` |

Query inbox:

```csharp
// Manager: PendingManager untuk direct report
db.AccessRequests.Where(r => r.Status == PendingManager && r.Requester.ManagerId == me.Id);
// System Owner: PendingSystemOwner untuk aplikasi miliknya
db.AccessRequests.Where(r => r.Status == PendingSystemOwner && r.Application.SystemOwnerId == me.Id);
```

### 3.8 Error mapping

| Kondisi | HTTP | Perilaku UI |
|---|---|---|
| Validasi gagal | 400 | Form tampil ulang dengan pesan per-field |
| Belum login | 302 → switcher | |
| Tidak berhak | 403 | Halaman Forbidden |
| Tidak ditemukan / bukan milik (requester lain) | 404 | Halaman Not Found |
| Conflict (terminal, bukan giliran, stale, idempotency payload beda) | 409 | Halaman/banner conflict dengan `code` dan tombol "muat ulang" |
| Error tak terduga | 500 | Halaman error generik, detail hanya di log |

Logging: Serilog/ILogger dengan `RequestId` (TraceIdentifier), actor, action, hasil. Tanpa data sensitif.

### 3.9 Spesifikasi UI

| Halaman | Isi |
|---|---|
| Layout | Navbar Bootstrap, nama user aktif, dropdown switcher |
| Create | Dropdown Application/Environment/AccessLevel, textarea Justification, hidden `ClientRequestId` (Guid dibuat saat form dirender), validation summary, tombol submit dinonaktifkan setelah klik |
| My Requests | Tabel (aplikasi, env, level, status badge, tanggal), empty state |
| Approvals | Tabel request menunggu, tombol Approve, form Reject dengan reason, empty state |
| Detail | Ringkasan, status badge, flag high-risk, policy version, timeline audit (actor, aksi, dari→ke, waktu, reason) |
| Conflict | Alert Bootstrap `alert-warning` berisi alasan dan link ke detail terbaru |

---

## 4. DECISION

Format ringkas ADR. Status semua: **Accepted** kecuali dinyatakan lain.

| ID | Keputusan | Alternatif | Alasan / konsekuensi |
|---|---|---|---|
| D-01 | Frontend **Razor + Bootstrap 5** (bukan Blade) | Blade/Laravel | Blade bukan bagian .NET; Razor adalah padanannya dan cocok dengan LINQ |
| D-02 | **ASP.NET Core MVC** server-rendered, tanpa API JSON terpisah | SPA + Web API | Scope kecil, authz server-side jelas; JSON API bisa ditambah di Phase 2 |
| D-03 | **EF Core + LINQ** untuk semua query; raw SQL hanya untuk trigger di migration | Dapper | Sesuai permintaan, migration terintegrasi |
| D-04 | **Kolom `Version` int manual** + `ExecuteUpdateAsync` bersyarat | `[ConcurrencyCheck]` + `DbUpdateConcurrencyException` | Update atomik di DB dan mudah dibuktikan lewat `rows == 0`; SQLite tanpa rowversion |
| D-05 | Idempotency lewat **UNIQUE (RequesterId, ClientRequestId)** dan tangkap violation | Cek-lalu-insert, lock aplikasi | Satu-satunya cara yang aman dari race |
| D-06 | `ClientRequestId` unik **per requester**, bukan global | Unik global | Mencegah user lain "menabrak" ID dan membocorkan keberadaan request |
| D-07 | Retry dengan `ClientRequestId` sama tetapi payload beda → **409** `idempotency_mismatch`; payload sama → kembalikan request asli | Selalu kembalikan asli | Mencegah salah paham bahwa payload baru diproses |
| D-08 | Audit **append-only via trigger SQLite** (UPDATE/DELETE di-ABORT) | Hanya konvensi aplikasi | Invariant tetap aman walau ada bug aplikasi |
| D-09 | Audit event ditulis **dalam transaksi yang sama** dengan perubahan state | Outbox/event terpisah | Syarat atomic; transaksi lokal SQLite cukup |
| D-10 | `IsHighRisk` dan `PolicyVersionId` **disimpan di request** saat create | Hitung ulang saat dibaca | Request lama tetap bisa dijelaskan saat policy berubah di Phase 2 |
| D-11 | Hanya user **yang punya manager** boleh membuat request | Semua user boleh | Mencegah request tanpa approver (Manager stage tidak bisa jalan) |
| D-12 | Request ditolak saat create jika requester adalah System Owner aplikasi target (validation error) | Biarkan macet | Menghindari deadlock self-approval tanpa menambah fitur fallback |
| D-13 | Urutan cek: 404 → 403 → 400 → 409 | Cek state dulu | Tidak membocorkan state ke user tak berhak |
| D-14 | Peran tidak sesuai tahap (Carol saat `PendingManager`) → **409**; bukan approver terkait sama sekali → **403** | Semua 403 | Membedakan "tidak berhak" dan "belum gilirannya" |
| D-15 | Requester hanya melihat request sendiri; request orang lain → **404** | 403 | Tidak mengungkap keberadaan data |
| D-16 | Auth simulasi: cookie; header `X-Demo-User` hanya untuk test via config flag | Header selalu aktif | Header bypass tidak boleh aktif di luar test; dicatat di REVIEW.md |
| D-17 | Test memakai **SQLite file temporer** + `WebApplicationFactory` | In-memory shared connection | Concurrency test realistis dengan banyak koneksi |
| D-18 | Tidak ada cancel/withdraw, backup approver, atau pencegahan duplikat bisnis lintas `ClientRequestId` | Implementasi | Di luar scope; masuk deferred di REVIEW.md |
| D-19 | Phase 2 **tidak** diimplementasikan; hanya menjaga `WorkflowRules` terisolasi | Pre-build | Dilarang sebelum Change Request diberikan |

---

## 5. TASK

Estimasi total sekitar 7,5 jam (di bawah batas 8 jam). Centang saat selesai; commit kecil per task.

### T0. Setup (0,5 jam)
- [ ] `git init`, `.gitignore` (.NET, `*.db`), commit awal
- [ ] Tag `assessment-start`
- [ ] Buat solution: `AccessRequestHub.Web` (MVC) dan `AccessRequestHub.Tests` (xUnit)
- [ ] Tambah paket: `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.AspNetCore.Mvc.Testing`
- [ ] Tulis PLAN.md awal (dari bagian 2 dan 4 dokumen ini)

### T1. Domain, schema, seed (1,5 jam)
- [ ] Enum dan entity: `User`, `Application`, `PolicyVersion`, `AccessRequest`, `AuditEvent`
- [ ] `RiskPolicy.IsHighRisk` dan `WorkflowRules` (tabel transisi)
- [ ] `AppDbContext`: konfigurasi FK, UNIQUE, CHECK, index, konversi enum ke string
- [ ] Migration awal + `migrationBuilder.Sql` untuk trigger audit
- [ ] Seed users, applications, policy `v1`
- [ ] Unit test `RiskPolicy` (4 kombinasi) dan `WorkflowRules` (tabel transisi)

### T2. Service layer (1,5 jam)
- [ ] `CreateAsync`: validasi, requester dari user login, D-11, D-12, idempotency (D-05 sampai D-07)
- [ ] `ApproveAsync` dan `RejectAsync`: urutan cek D-13, conditional update, audit atomic
- [ ] `GetMyRequestsAsync`, `GetDetailAsync` (dengan audit timeline), `GetInboxAsync`, `GetAllAsync`
- [ ] Exception domain: `ValidationException`, `ForbiddenException`, `NotFoundException`, `ConflictException(code)`
- [ ] Logging di setiap action

### T3. Auth, controller, error mapping (1 jam)
- [ ] `DemoAuthHandler` + `CurrentUser` + flag `Auth:AllowHeader`
- [ ] `AccountController` (switcher), `RequestsController`, `ApprovalsController`
- [ ] Antiforgery pada semua POST
- [ ] Filter/middleware pemetaan exception → 400/403/404/409/500

### T4. Automated tests (1,5 jam)
Test integrasi memakai SQLite file temporer per test class.

| Test | AC |
|---|---|
| Standard flow: Bob approve → Approved + audit 2 event | AC-01 |
| Production flow: Bob → PendingSystemOwner → Carol → Approved | AC-02 |
| Admin Finance Portal: menunggu Dana, bukan Carol | AC-03 |
| Carol/Dana/Erin/Alice approve → 403, tidak ada perubahan | AC-04, AC-12, AC-13, AC-18 |
| Duplicate sekuensial → 1 baris | AC-05 |
| Duplicate paralel (`Task.WhenAll`, 10 request) → 1 baris | AC-05 |
| Payload beda dengan ID sama → 409 | D-07 |
| Dua approve paralel dengan version sama → satu sukses, satu 409 | AC-06 |
| Approve dengan version lama → 409 `stale_version` | AC-06 |
| Reject dengan reason → tersimpan + audit + approve berikutnya 409 | AC-07, AC-09 |
| Reject tanpa/whitespace reason → 400 | AC-08 |
| Carol approve saat `PendingManager` → 409 `not_your_stage` | AC-14 |
| Payload requester palsu diabaikan | AC-10 |
| Validasi field create | AC-11 |
| Audit gagal → state rollback (inject failing interceptor) | AC-15 |
| `UPDATE`/`DELETE` AuditEvents via SQL mentah gagal | AC-16 |
| `IsHighRisk` dan `PolicyVersion` tersimpan | AC-17 |
| Alice akses request orang lain → 404 | AC-19 |
| Build ulang DB dari migration + seed | AC-21 |

### T5. Frontend Razor + Bootstrap (1 jam)
- [ ] `_Layout` dengan navbar dan user switcher
- [ ] Create form dengan validation summary, `ClientRequestId` hidden, anti double-click
- [ ] My Requests dan All (auditor) dengan empty state
- [ ] Approvals inbox: Approve, form Reject + reason
- [ ] Detail: badge status, flag high-risk, policy version, timeline audit
- [ ] Halaman Forbidden, NotFound, Conflict, Error
- [ ] Cek manual 7 skenario demo dan catat hasilnya

### T6. Dokumentasi dan penutupan (0,5 jam)
- [ ] README.md: prasyarat, `dotnet ef database update`, `dotnet run`, `dotnet test`, tabel demo users, demo flow 7 skenario
- [ ] AI_USAGE.md: 2-5 interaksi + "Three things AI got wrong"
- [ ] REVIEW.md: finding, severity, action, status, evidence; limitation (D-16, D-18, SQLite single-writer); deferred work
- [ ] INTEGRITY.md: deklarasi, tools AI, bantuan eksternal, snippet adaptasi
- [ ] Pastikan tidak ada credential atau file `.db` di repo
- [ ] Jalankan `dotnet test`, tag `phase-1-complete`

### Aturan timebox
Jika mendekati 8 jam: hentikan di state yang stabil dan runnable, pindahkan sisa task ke bagian deferred di REVIEW.md. Urutan yang dikorbankan lebih dulu: polish UI (T5), lalu test tambahan non-wajib. Yang tidak boleh dikorbankan: T1 sampai T4 dan T6.

### Command referensi

```bash
dotnet new mvc -n AccessRequestHub.Web
dotnet new xunit -n AccessRequestHub.Tests
dotnet ef migrations add InitialCreate -p src/AccessRequestHub.Web
dotnet ef database update -p src/AccessRequestHub.Web
dotnet run --project src/AccessRequestHub.Web
dotnet test
```
