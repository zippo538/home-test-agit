# AI_USAGE.md — Deklarasi Penggunaan AI

> Catatan kejujuran: interaksi di bawah direkonstruksi dari riwayat commit, isi kode, dan hasil
> review (lihat `PLAN.md` §6). Kutipan prompt adalah parafrase pola yang dipakai — sesuaikan
> dengan prompt asli bila perlu. Interaksi **paling berpengaruh** berarti: mengubah arah kode
> secara permanen, bukan sekadar menyelesaikan tugas kecil.

## Tools yang dipakai

| Tool | Pemakaian |
|---|---|
| Claude Code (CLI) | Drafting kode, refactor, scaffold test, debug, drafting dokumentasi |
| (tambahan lain bila ada) | — |

Pembagian kerja: **AI** menulis draft besar (entity, service, repository, view, test) dan
mencari penyebab kegagalan; **manusia** mengambil keputusan bisnis (trade-off, scope,
security posture), me-review setiap commit sebelum masuk `main`, dan memutuskan revert
(`0c50182`) saat refactor `a01137e` tidak lolos review.

## 2–5 interaksi AI paling berpengaruh

### I-1 — Idempotency yang aman terhadap race (tidak berhenti di "cek dulu, lalu insert")

Diminta: cara membuat create request tidak menghasilkan baris kedua, terutama kalau
user klik dua kali / dua request datang hampir bersamaan.

Yang diambil dari jawaban AI: ** tidak** version cek-lalu-insert, tapi
`UNIQUE (RequesterId, ClientRequestId)` di database + menangkap `SqliteException`
(SQLITE_CONSTRAINT_UNIQUE, code 19/2067) di `AccessRequestRepository.TryAddAsync`,
lalu query ulang pemenang dan mengembalikan request asli (`Replay`).

Yang benar-benar mengubah desain: pemisahan hasil `CreateAccessRequestResult` dengan
flag `IsReplay`, plus pembandingan payload di `Replay()` sehingga retry dengan
`ClientRequestId` sama tetapi payload beda menjadi **409 `idempotency_mismatch`**
(D-07), bukan "request asli dikembalikan diam-diam".

Bukti: `AccessRequestRepository.cs` (`IsClientRequestIdViolation`),
`AccessRequestService.CreateAsync`, test `IdempotencyTests.cs`
(retry sekuensial, race paralel, payload beda, ID sama untuk user beda).

### I-2 — Optimistic concurrency lewat conditional UPDATE, bukan change tracking

Diminta: dua approver bertindak bersamaan; pastikan hanya satu transisi per `Version`.

Yang diambil dari jawaban AI: `ExecuteUpdateAsync` bersyarat
(`WHERE Id = ? AND Version = ? AND Status = ?`) + cek `rows == 0` → 409 `stale_version`,
dengan audit event ditulis dalam **transaksi yang sama** (gagal insert audit → rollback).
Pre-check `Version` dipertahankan sebagai fast-path, tapi kepastian tetap di
conditional update.

Konsekuensi yang diikuti: entity tidak pernah dimodifikasi lewat change tracker untuk
transisi; semantik "0 baris = konflik" jadi mudah diuji, dan
`ConcurrencyTests.Dua_approve_paralel_dengan_version_sama…`
menegaskan `Version` naik tepat satu (bukan dua).

Bukti: `AccessRequestRepository.TryTransitionAsync`, `WorkflowRules.Resolve`,
test `ConcurrencyTests.cs`.

### I-3 — Audit append-only dijaga database, bukan hanya konvensi aplikasi

Diminta: audit event tidak boleh bisa di-update/delete, idealnya oleh database.

Yang diambil dari jawaban AI: trigger SQLite `BEFORE UPDATE`/`BEFORE DELETE` pada
`AuditEvents` yang me-`RAISE(ABORT, 'AuditEvents is append-only')`.

Perubahan yang terjadi saat implementasi (bukti AI corected halfway): rencana awal
(D-08/ADR-008) menulis trigger lewat `migrationBuilder.Sql(...)` di migration, tetapi
yang berjalan sekarang **menginstall trigger saat startup** lewat
`Data/DatabaseBootstrapper.AuditAppendOnlyGuard.InstallAsync` (CREATE TRIGGER IF NOT EXISTS),
dan **tidak ada** trigger di file migration. Konsekuensi yang perlu diperhatikan:
database yang dibangun tanpa melewati `DatabaseBootstrapper` tidak punya trigger.

Bukti: `grep trg_audit AccessRequestHub.Persistence/Migrations/*.cs` → 0 kemunculan;
`AuditAppendOnlyGuard` + test `AuditTests.UPDATE_pada_AuditEvents_ditolak_database`.

### I-4 — Test harness: satu database temporer per test via WebApplicationFactory

Diminta: test integration yang memang benar menjalankan aplikasi web, mampu menguji
race dan query multi-koneksi.

Yang diambil dari jawaban AI: `HubAppFactory` (subclass `WebApplicationFactory<Program>`)
yang memakai **file SQLite temporer per test** (bukan `:memory:` shared) +
`HubClient` yang mengelola cookie dan antiforgery token seperti browser sungguhan.
Basisnya `IntegrationTest : IAsyncLifetime` membersihkan `.db`/`-wal`/`-shm`.

Masalah yang muncul justru dari desain ini (lihat AI Wrong #2): satu factory per test
= satu `FileSystemWatcher` baru per factory, dan di mesin dengan
`inotify.max_user_instances=128`, beberapa test di run paralel penuh gagal spurious
(`IOException`), memakai env `DOTNET_USE_POLLING_FILE_WATCHER=1` agar suite hijau penuh
(terverifikasi: 60/60 pass setelah polling watcher di-set via `ModuleInitializer`
di `AssemblyInfo.cs`), dan jadi perbaikan permanen (REVIEW.md R-05).

Bukti: `tests/AccessRequestHub.Tests/Infrastructure/`, log test run, data
`cat /proc/sys/fs/inotify/max_user_instances` → 128.

### I-5 — Clean-architecture split (Models + Persistence) dan kenapa sempat di-revert

Diminta: pisahkan domain dari infrastruktur supaya aturan bisnis bisa diuji tanpa memuat
`DbContext`.

Yang diambil dari jawaban AI: pecah monolit Web jadi `AccessRequestHub.Models`
(entity + domain murni) dan `AccessRequestHub.Persistence` (DbContext, repository,
bootstrap), dengan referensi proyek yang mengarah ke domain.

Human review ke-1: **revert** (`0c50182`) karena churn terlalu besar untuk satu commit
dan menyentuh banyak file yang tidak perlu. Human review ke-2: **re-apply** (`cde8c5d`)
setelah penyesuaian, dilanjut merge PR #2 (`feat/integration-dotnet-aspire`).

Konsekuensi yang masih terasa sampai sekarang: beberapa path di `README.md` menunjuk
ke lokasi lama (`tests/…` sebelum folder dipindah, `AccessRequestHub.Persitence`
typo, `home-test-agit.csproj` nama lama). Ini jadi item untuk dibersihkan sebelum
tag `phase-1-complete` (lihat `PLAN.md` §6 P3/P7).

## What AI got right

- Struktur data model + constraint DB (UNIQUE, CHECK, FK RESTRICT) sudah
  review-passed dan cocok `specs/SPEC.md` §9.
- Ketegasan mapping error 400/403/404/409 ke halaman/banner yang berbeda di UI,
  dan menolak "hanya sembunyikan tombol di UI" untuk aksi authorization.
- Menjaga urutan otorisasi: 404 dulu (jangan bocorkan keberadaan), baru 403, baru 409.
- Membuat test race idempotency dan concurrency **lewat HTTP sungguhan**, bukan
  by-pass ke service, sehingga jalur otorisasi ikut teruji.

## Three things AI got wrong

### W-1 — Rute error hard-coded ke controller yang tidak ada

`Program.cs` (Web **dan** IdentityServer) memakai `app.UseExceptionHandler("/Home/Error")`,
dan `ServiceCollectionExtensions` men-set `options.AccessDeniedPath = "/Home/Forbidden"`.
Padahal **tidak ada `HomeController`** dan **tidak ada** view `Views/Home/Error.cshtml`
atau `Views/Home/Forbidden.cshtml` (`Forbidden.cshtml`/`NotFound.cshtml` ada, tapi
di `Views/Shared/`). Akibat: 500 yang tak tertangani lain menumpuk jadi 500 dengan
halaman error yang gagal render. Ditemukan saat review final, belum diperbaiki —
harus dibuatkan controller/target yang benar sebelum tag `phase-1-complete`.

### W-2 — Satu `WebApplicationFactory` baru per test tanpa menonaktifkan file watcher

`HubAppFactory` dibuat fresh untuk setiap instantiation, sementara setiap factory
menambah jatah inotify. Di mesin dengan `inotify.max_user_instances=128`, run paralel
penuh membuang sebagian test sebagai `IOException` — tampilannya seperti kegagalan
konkurensi/otorisasi padahal hanya terbatas resource OS. Penanganan yang benar:
set polling watcher di `HubAppFactory` (satu baris), bukan mengganti per-test.

### W-3 — Drift dokumentasi setelah refactor, termasuk duplikat AI_USAGE.md ini

Rencana awal menulis trigger lewat `migrationBuilder.Sql` di migration (D-08/ADR-008),
tapi implementasi meng-install-nya saat startup — yang di docs masih tertulis "via
migration". README juga menunjuk path proyek lama (`tests/…`, `AccessRequestHub.Persitence`,
`home-test-agit.csproj`) setelah folder dipindah/pisah. Dan **`AI_USAGE.md` ini sendiri**
sempat berupa salinan `PLAN.md` karena dibuat dari template PLAN. Nilai jawaban:
dokumen task meminta `PLAN.md` **dan** `AI_USAGE.md` sebagai dua deliverable berbeda;
menyalin satu ke yang lain menghilangkan bagian pengakuan penggunaan AI yang diminta.

## Urutan penggunaan (ringkas)

1. **Desain** → dibantu AI menyusun alternatif (constraint vs cek-lalu-insert,
   change tracking vs conditional UPDATE, SQLite vs Postgres) → dipilih dan dicatat
   di `specs/DECISIONS.md`.
2. **Implementasi** → AI draft besar; manusia review per commit terutama pada layer
   shared (`AccessRequestService`, `AccessRequestRepository`).
3. **Debug** → AI dipakai untuk jelaskan cara men-disable/reconfigure file watcher
   pada test factory; verifikasi dilakukan langsung (catat `/proc/sys/fs/inotify/...`).
4. **Review akhir** → human review menemukan tiga isi `AI Wrong` di atas; AI dipakai
   untuk menuliskan kembali temuan secara terstruktur.
