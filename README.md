# Access Request Hub

MVP untuk memusatkan permintaan akses aplikasi internal: request, approval bertingkat,
idempotency, optimistic concurrency, dan audit trail append-only.

Stack: **ASP.NET Core MVC (.NET 10)** · **EF Core + LINQ** · **SQLite** · **Razor Views + Bootstrap 5**

> Catatan: dokumen task menyebut "Blade". Blade adalah template engine Laravel (PHP) dan tidak
> dapat dipakai bersama LINQ/.NET. Padanannya di .NET adalah **Razor**, sehingga frontend memakai
> Razor + Bootstrap.

---

## 1. Prasyarat

| Kebutuhan | Versi |
|---|---|
| .NET SDK | 10.0 atau lebih baru |
| EF Core CLI (opsional, untuk migration manual) | 10.x — `dotnet tool install --global dotnet-ef` |
| Database | SQLite — tidak perlu instalasi server |

Verifikasi:

```bash
dotnet --version
```

---

## 2. Menjalankan program

Dari root repository:

```bash
dotnet restore
dotnet build
dotnet run
```

Aplikasi berjalan di **http://localhost:5135** (profil `http` pada `Properties/launchSettings.json`).

Saat startup, aplikasi otomatis:

1. menjalankan migration (membuat schema + seed), dan
2. memasang trigger append-only pada tabel audit.

Buka `http://localhost:5135` — halaman pertama adalah **User Switcher**. Pilih satu demo user
untuk mulai (tidak ada sandi; ini login simulasi Phase 1).

Profil HTTPS (opsional):

```bash
dotnet run --launch-profile https
```

---

## 3. Database

### Lokasi

File `accessrequesthub.db` dibuat di root project. Connection string ada di `appsettings.json`:

```
Data Source=accessrequesthub.db;Default Timeout=30
```

`Default Timeout=30` memberi ruang saat SQLite mengunci file (single-writer).

### Membuat / memperbarui schema

Otomatis saat `dotnet run`. Secara manual:

```bash
dotnet ef database update
```

### Reset ke kondisi bersih (reproducible)

```bash
# Hentikan aplikasi terlebih dahulu (file DB terkunci saat app berjalan)
rm -f accessrequesthub.db accessrequesthub.db-shm accessrequesthub.db-wal
dotnet run
```

Schema, master data, dan trigger akan dibuat ulang sepenuhnya.

### Migration

```bash
dotnet ef migrations add <NamaMigration>
```

### Seed data

Seed memakai ID tetap (`HasData`), jadi reproducible:

| Tabel | Isi |
|---|---|
| Users | alice (manager: bob), bob, carol, dana, erin (auditor) |
| Applications | CRM (owner carol), Finance Portal (owner dana) |
| PolicyVersions | `v1` (aktif) |

### Invariant yang dijaga database

- `UNIQUE (RequesterId, ClientRequestId)` — inti idempotency; aman terhadap race.
- `CHECK` pada `Environment`, `AccessLevel`, `Status`, `Version >= 1`.
- `CHECK (Status <> 'Rejected' OR length(trim(RejectReason)) > 0)` — reject wajib beralasan.
- FK ke `Users`, `Applications`, `PolicyVersions`, `AccessRequests` dengan `RESTRICT`.
- Trigger `trg_audit_no_update` dan `trg_audit_no_delete` — `UPDATE`/`DELETE` pada `AuditEvents`
  di-`ABORT` oleh database, sehingga audit tetap append-only walaupun ada bug aplikasi.

---

## 4. Test

```bash
dotnet test
```

> **Status:** project test otomatis belum dibuat di repository ini. Perintah di atas akan
> melaporkan bahwa tidak ada project test yang ditemukan. Rencana cakupan test (flow standard,
> high-risk, authorization failure, idempotency, concurrency) ada di `specs/ACCEPTANCE.md`
> bagian H dan `specs/TASKS.md` task T5. Sampai itu selesai, verifikasi dilakukan manual
> memakai **Demo Flow** di bagian 6.

---

## 5. Demo users

| Email | Nama | Peran |
|---|---|---|
| alice@example.local | Alice | Requester (atasan: Bob) |
| bob@example.local | Bob | Manager Alice — approver tahap 1 |
| carol@example.local | Carol | System Owner **CRM** — approver tahap 2 |
| dana@example.local | Dana | System Owner **Finance Portal** — approver tahap 2 |
| erin@example.local | Erin | Admin/Auditor — melihat semua request + audit, **bukan approver** |

Berganti user: menu **Ganti User** di navbar, atau langsung ke `/Account/Switch`.

---

## 6. Demo flow

Aturan routing: **high-risk = Environment `Production` ATAU AccessLevel `Admin`**.

| Kondisi | Jalur |
|---|---|
| Non-high-risk (NonProduction **dan** Read) | Manager → **Approved** |
| High-risk | Manager → System Owner aplikasi → **Approved** |

```
PendingManager ──approve (non-high-risk)──► Approved (terminal)
      │
      ├──approve (high-risk)──► PendingSystemOwner ──approve──► Approved
      │                                │
      └──reject (reason wajib)         └──reject (reason wajib)
                 ▼                                ▼
            Rejected (terminal)             Rejected (terminal)
```

### 1. Standard request — Alice → CRM, NonProduction, Read
1. Login **Alice** → **Ajukan Request** → Application `CRM`, Environment `NonProduction`,
   Access Level `Read`, isi Justification → **Kirim Request**.
2. Status awal `PendingManager`.
3. **Ganti User** ke **Bob** → menu **Approvals** → request Alice ada di inbox.
4. **Tinjau** → **Approve**.
5. Hasil: status **`Approved`** (terminal). Audit berisi `Created` lalu `ManagerApproved`.

### 2. Production request — Alice → CRM, Production, Read (high-risk)
1. Login **Alice**, buat request `CRM` / `Production` / `Read`.
2. Login **Bob** → Approvals → Approve.
3. Status menjadi **`PendingSystemOwner`**, dan request pindah ke inbox **Carol** (owner CRM).
4. Login **Carol** → Approvals → Approve.
5. Hasil: **`Approved`**. Audit: `Created` → `ManagerApproved` → `SystemOwnerApproved`.

### 3. Admin access — Alice → Finance Portal, NonProduction, Admin (high-risk)
1. Login **Alice**, buat request `Finance Portal` / `NonProduction` / `Admin`.
2. Login **Bob** → Approve.
3. Status `PendingSystemOwner`, masuk inbox **Dana** (owner Finance Portal) — **bukan** Carol.
4. Login **Dana** → Approve → **`Approved`**.

### 4. Unauthorized approval
1. Buat request sebagai Alice (status `PendingManager`).
2. Login **Carol** (atau Dana/Erin), lalu panggil aksi approve pada request tersebut.
3. Hasil: **403 Forbidden** — di UI approver yang salah tahap tidak melihat tombolnya,
   dan server tetap menolak bila endpoint dipanggil langsung.

### 5. Duplicate submit (idempotency)
1. Login Alice, isi form create, lalu klik **Kirim Request** dua kali cepat (atau refresh
   halaman hasil).
2. Aplikasi mengirim `ClientRequestId` yang sama dan tombol dinonaktifkan setelah submit.
3. Hasil: tetap **satu** baris di `AccessRequests`; pesan menandakan request yang sama
   dikembalikan (idempotent).

### 6. Concurrent action
1. Siapkan request `PendingManager` milik Alice, catat nilai **Version** di halaman detail.
2. Buka request itu di dua tab/sesi sebagai Bob, lalu klik **Approve** di keduanya.
3. Hasil: satu aksi berhasil; aksi kedua mendapat **409 conflict** dengan banner konflik
   dan tombol **Muat ulang**.

### 7. Rejected request
1. Login **Bob**, buka request `PendingManager` milik Alice.
2. Isi kolom **Reason** pada form Reject → klik **Reject**.
3. Hasil: status **`Rejected`** (terminal), reason tersimpan dan tampil di detail,
   audit event `Rejected` tercatat. Percobaan approve/reject berikutnya ditolak **409**.
4. Reject tanpa reason (atau hanya spasi) ditolak **400** dan state tidak berubah.

### 8. Auditor view
Login **Erin** → menu **All Requests** menampilkan seluruh request beserta audit trail.
Erin tidak punya inbox approval, dan aksi approve/reject terhadap Erin ditolak **403**.

---

## 7. Route

| Method | Route | Akses | Keterangan |
|---|---|---|---|
| GET/POST | `/Account/Switch` | Publik | User switcher (login simulasi) |
| GET | `/Requests` | Login | My Requests (milik sendiri) |
| GET | `/Requests/All` | Auditor | Semua request (selain auditor → 403) |
| GET/POST | `/Requests/Create` | Requester dengan atasan | Form create |
| GET | `/Requests/Details/{id}` | Pemilik, manager, System Owner terkait, auditor | Detail + audit timeline |
| GET | `/Approvals` | Manager / System Owner | Approval inbox sesuai peran |
| POST | `/Requests/Approve` | Approver tahap aktif | Body: `id`, `version`, antiforgery token |
| POST | `/Requests/Reject` | Approver tahap aktif | Body: `id`, `version`, `reason` |

Semua POST memakai antiforgery token.

---

## 8. Error mapping

| Kondisi | HTTP | Perilaku UI |
|---|---|---|
| Field wajib kosong, enum tidak valid, reject tanpa reason | **400** | Banner konflik berisi pesan validasi per-field |
| Belum login | **302** | Diarahkan ke User Switcher |
| Bukan assigned approver / self-approval / bukan auditor | **403** | Halaman Forbidden |
| Request tidak ada, atau bukan milik user | **404** | Halaman Not Found |
| Status terminal, belum giliran, version basi, idempotency payload berbeda | **409** | Banner konflik dengan kode + tombol muat ulang |
| Error tak terduga | **500** | Halaman error generik; detail hanya di log |

Kode konflik yang dibedakan: `already_final` (sudah terminal), `not_your_stage` (belum giliran),
`stale_version` (version basi), `idempotency_mismatch` (ClientRequestId dipakai dengan payload
berbeda).

Urutan pengecekan: **404 → 403 → 400 → 409**. Request yang tidak berhak dilihat menghasilkan 404,
bukan 403, supaya keberadaan data tidak bocor.

---

## 9. Authentication (simulasi)

Tidak ada SSO/OAuth pada Phase 1. Identitas berasal dari dua sumber:

1. **Cookie** `arh.demo` — diisi oleh User Switcher. Ini jalur normal saat memakai browser.
2. **Header** `X-Demo-User: <email>` — **hanya aktif bila** `Auth:AllowHeader=true`, dan
   default-nya `false`. Ditujukan untuk test otomatis, bukan untuk produksi.

Mengaktifkan header secara lokal:

```bash
Auth__AllowHeader=true dotnet run
```

Actor pada setiap aksi selalu diambil dari principal yang terautentikasi di server, bukan dari
payload form. Otorisasi ditegakkan di service layer; menyembunyikan tombol di UI bukan pengaman.

---

## 10. Struktur project

```
Controllers/     Account (switcher), Requests (create/list/detail/approve/reject), Approvals (inbox)
Auth/            DemoAuthenticationHandler, CurrentUser
Filters/         DomainExceptionFilter (403/404 → halaman error)
Models/          Entity, enum, RiskPolicy, WorkflowRules, ApprovalAuthorization, ViewModel
Repository/      AppDbContext, repository + interface (semua query LINQ)
Services/        AccessRequestService (aturan bisnis), exception domain, DI extension
Data/            DatabaseBootstrapper (migrate + trigger audit append-only)
Migrations/      Migration EF Core + seed
Views/           Razor + Bootstrap (Requests, Approvals, Account, Shared)
specs/           ACCEPTANCE, PLAN, SPEC, DECISIONS, TASKS
```

Aturan bisnis terpusat di `Models/RiskPolicy.cs` (definisi high-risk) dan
`Models/WorkflowRules.cs` (tabel transisi). Controller hanya membind request, memanggil service,
dan memetakan hasil ke HTTP status.

---

## 11. Known limitations

- **System Owner tidak bisa mengajukan request ke aplikasinya sendiri.** Ditolak saat create
  (validation error) untuk menghindari deadlock self-approval tanpa menambah fitur fallback.
- **Belum ada cancel/withdraw** dan **backup/delegated approver**.
- **Duplikat bisnis tidak dicegah**: dua request dengan akses sama tetapi `ClientRequestId`
  berbeda tetap sah.
- **`ClientRequestId` unik per requester**, bukan global — mencegah user lain menabrak ID dan
  membocorkan keberadaan request.
- **SQLite single-writer**: penulisan konkuren di-serialize. Optimistic concurrency tetap bekerja,
  tetapi ini bukan uji throughput.
- **Belum ada automated test project** (lihat bagian 4).
- Dokumentasi deliverable lain (`PLAN.md`, `AI_USAGE.md`, `REVIEW.md`, `INTEGRITY.md`) belum
  tersedia di root repository.

---

## 12. Troubleshooting

**`Unable to copy file ... because it is being used by another process`**
Aplikasi sebelumnya masih berjalan. Hentikan dulu:

```bash
taskkill //F //IM home-test-agit.exe      # Windows
pkill -f home-test-agit                   # bash/WSL
```

**`database is locked`**
Pastikan hanya satu instance aplikasi berjalan. `Default Timeout=30` di connection string
memberi waktu tunggu; transaksi aplikasi sengaja dibuat pendek.

**Perubahan schema tidak terlihat**
Jalankan `dotnet ef database update`, atau reset database (bagian 3). Ingat: saat aplikasi
berjalan, file `.db` terkunci.
