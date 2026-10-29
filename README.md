# Access Request Hub

MVP untuk memusatkan permintaan akses aplikasi internal: request, approval bertingkat,
idempotency, optimistic concurrency, dan audit trail append-only.

Stack: **ASP.NET Core MVC (.NET 10)** · **Duende IdentityServer 8** · **ASP.NET Core Identity** · **EF Core + SQLite** · **Razor + Bootstrap**

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

Dari root repository, restore, build, dan test:

```bash
dotnet restore
dotnet build
dotnet test AccessRequestHub.Tests/AccessRequestHub.Tests.csproj
```

Authentication berjalan melalui dua host. Lihat bagian 9 untuk konfigurasi secrets. Jalankan
IdentityServer dan MVC app di terminal terpisah:

```bash
dotnet run --project AccessRequestHub.IdentityServer --launch-profile https
```

```bash
dotnet run --project AccessRequestHub.Web --launch-profile https
```

Buka `https://localhost:7019`; login akan dialihkan ke Duende IdentityServer. Host HTTP lokal
juga tersedia di `http://localhost:5149` (IdentityServer) dan `http://localhost:5135` (MVC app).

Saat startup, IdentityServer menginisialisasi database business dan identity secara terpisah.
IdentityServer memigrasikan schema workflow dan memasang trigger append-only pada audit table.

---

## 3. Database

### Lokasi

File business `accessrequesthub.db` berada di root project. Connection string ada di `AccessRequestHub.Web/appsettings.json`. Identity credentials disimpan terpisah di `accessrequesthub.identity.db` dan tidak berisi data workflow:

```
Data Source=accessrequesthub.db;Default Timeout=30
```

`Default Timeout=30` memberi ruang saat SQLite mengunci file (single-writer).

### Membuat / memperbarui schema

Schema business diinisialisasi oleh IdentityServer saat startup. Untuk migration workflow secara manual, gunakan host project yang mengandung `AppDbContext`:

```bash
cd AccessRequestHub.Persistence
dotnet ef database update
```

### Reset ke kondisi bersih (reproducible)

```bash
# Hentikan aplikasi terlebih dahulu (file DB terkunci saat app berjalan)
cd ../
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

Test suite berada di `AccessRequestHub.Tests` dan mencakup workflow, otorisasi, concurrency, idempotency, audit, dan validasi.

---

## 5. Demo users

| Email | Nama | Password | Peran |
|---|---|---| ---|
| `alice@example.local` | Alice | `DemoAlice2026!` | Requester (atasan: Bob) |
| `bob@example.local` | Bob | `DemoBob2026!Ab` | Manager Alice — approver tahap 1 |
| `carol@example.local` | Carol | `DemoCarol2026!` |System Owner **CRM** — approver tahap 2 |
| `dana@example.local` | Dana | `DemoDana2026!` |System Owner **Finance Portal** — approver tahap 2 |
| `erin@example.local` | Erin | `DemoErin2026!` | Admin/Auditor — melihat semua request + audit, **bukan approver** |

Akun domain di atas dipetakan ke akun ASP.NET Core Identity. Pada Development, IdentityServer hanya membuat akun yang password-nya diberikan lewat user secrets atau environment configuration (lihat bagian 9); password tidak disediakan secara default.

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
3. **Keluar**, lalu masuk sebagai **Bob** → menu **Approvals** → request Alice ada di inbox.
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
| GET | `/Account/Login` | Publik | Landing page untuk memulai SSO |
| GET | `/Account/Sso` | Publik | OIDC challenge ke IdentityServer |
| POST | `/Account/Logout` | Login | Keluar dari MVC app dan IdentityServer |
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
| Belum login | **302** | Diarahkan ke login SSO |
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

## 9. Authentication dan secrets

MVC app menggunakan OIDC Authorization Code + PKCE dengan Duende IdentityServer. Untuk Development,
client secret harus tersedia di user secrets pada **kedua** project, dengan nilai yang sama. Dari
root repository:

```bash
dotnet user-secrets init --project AccessRequestHub.IdentityServer
dotnet user-secrets set "Oidc:ClientSecret" "<strong-random-client-secret>" --project AccessRequestHub.IdentityServer
dotnet user-secrets init --project AccessRequestHub.Web
dotnet user-secrets set "Oidc:ClientSecret" "<same-strong-random-client-secret>" --project AccessRequestHub.Web
```

Tambahkan password sementara yang unik untuk setiap akun demo ke user secrets IdentityServer
(gunakan email yang tercantum di bagian 5):

```bash
dotnet user-secrets set "DevelopmentAccounts:Passwords:alice@example.local" "<strong-temporary-password>" --project AccessRequestHub.IdentityServer
```

Ulangi untuk setiap akun yang perlu diuji. Seeder hanya aktif pada Development dan tidak membuat
atau mengubah password tanpa nilai konfigurasi eksplisit. Jangan commit password, client secret,
signing key, atau Duende license. Untuk HTTPS lokal, trust development certificate bila diperlukan:

```bash
dotnet dev-certs https --trust
```

Header `X-Demo-User` hanya didukung oleh authentication handler test ketika `Auth:AllowHeader=true`;
jangan aktifkan di production. Actor untuk setiap aksi diambil dari principal server-side, bukan
dari payload form. Otorisasi workflow tetap ditegakkan di service layer.

### Sign in dengan Microsoft (opsional, personal account)

Halaman login IdentityServer bisa menampilkan tombol **Masuk dengan Microsoft** bila provider
Microsoft dikonfigurasi. Tombol hanya muncul bila `Microsoft:ClientId` dan `Microsoft:ClientSecret`
tersedia; tanpa konfigurasi itu, login lokal (email + password) tetap berfungsi seperti biasa.

Siapkan app registration Microsoft personal (consumers) di portal Azure/microsoft:

1. Buat **App registration** dengan tipe akun *Personal Microsoft accounts only*.
2. Catat **Application (client) ID**.
3. Tambahkan **Client secret** (bisa Secret description bebas; catat nilainya).
4. Di **Authentication**, tambahkan Web redirect URI: `http://localhost:5149/signin-microsoft`
   (sesuaikan host/port bila memakai profil lain).

Lalu set di user secrets IdentityServer:

```bash
dotnet user-secrets set "Microsoft:ClientId" "<application-client-id>" --project AccessRequestHub.IdentityServer
dotnet user-secrets set "Microsoft:ClientSecret" "<client-secret-value>" --project AccessRequestHub.IdentityServer
```

Setelah login Microsoft pertama pada suatu akun, halaman **ExternalSubject** menampilkan subject
(`sub`) akun itu. Operator menautkannya secara eksplisit ke satu pengguna domain dengan menambahkan
mapping berikut lalu me-restart IdentityServer:

```bash
dotnet user-secrets set "Microsoft:ApprovedSubjects:<subject>" "<domain-user-id>" --project AccessRequestHub.IdentityServer
```

`<domain-user-id>` adalah ID numerik pengguna domain (misalnya `1` untuk Alice, `2` untuk Bob,
`3` untuk Carol, `4` untuk Dana, `5` untuk Erin). Setelah mapping terpasang, login Microsoft
berikutnya akan masuk sebagai pengguna domain tersebut, dan role (`Requester`, `Manager`,
`SystemOwner`, `Admin`) diambil dari data domain seperti biasa.

**Catatan penting:** satu akun Microsoft hanya bisa tertaut ke **satu** pengguna domain.
Alias Outlook seperti `mahindra+alice@outlook.com` dan `mahindra+bob@outlook.com` adalah akun
Microsoft yang sama (subject identik), sehingga keduanya akan masuk sebagai pengguna domain
yang sama. Untuk menguji beberapa role dengan SSO Microsoft, gunakan akun Microsoft yang
benar-benar berbeda (registrasi terpisah), atau gunakan akun demo lokal (email + password)
untuk role lainnya. Pencocokan email Microsoft tidak dipakai untuk menautkan akun; hanya
mapping eksplisit oleh operator yang berlaku, dan tanpa mapping akun akan ditolak.

IdentityServer Development tidak memerlukan production license. Deployment non-Development membutuhkan
`Duende:LicenseKey`, signing key material, client secret, dan redirect URI HTTPS yang sesuai dari
secure configuration. Patuhi persyaratan lisensi Duende.

---

## 10. Aspire

```
dotnet restore AccessRequestHub.AppHost/AccessRequestHub.AppHost.csproj
dotnet run --project AccessRequestHub.AppHost --launch-profile https

```


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
- Signing key persistence, production-grade Identity database migration/deployment, serta production provisioning perlu dikonfigurasi sesuai environment sebelum deployment.

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
