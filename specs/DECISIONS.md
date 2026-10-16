# DECISIONS.md — ADR & Asumsi

Format: keputusan, konteks, opsi, pilihan, konsekuensi.

---

## ADR-001 — EF Core + provider SQLite untuk LINQ

**Konteks.** Butuh relational DB dengan query LINQ dan setup reproducible untuk assessor.
**Opsi.** SQLite; PostgreSQL via Docker; SQL Server LocalDB.
**Pilihan.** SQLite + `Microsoft.EntityFrameworkCore.Sqlite`.
**Konsekuensi.** Nol dependensi eksternal, file DB tunggal yang mudah dihapus lalu di-seed ulang (AC-DB-01). Trade-off: penulisan konkuren di-serialize; logika optimistic concurrency tetap teruji, tapi bukan uji throughput.

## ADR-002 — Razor Views + Bootstrap, bukan SPA

**Konteks.** UI butuh switcher, form, list, inbox, detail + timeline, state loading/empty/error.
**Opsi.** Razor server-rendered; Blazor; SPA terpisah.
**Pilihan.** Razor + Bootstrap 5 (sudah tersedia di `wwwroot/lib/bootstrap`).
**Konsekuensi.** Waktu habis untuk correctness, bukan tooling. Approve/reject lewat endpoint JSON kecil agar jalur otorisasi sama dengan yang diuji.

## ADR-003 — Idempotency ditegakkan unique constraint DB

**Konteks.** Retry create tidak boleh membuat request kedua, termasuk saat race.
**Opsi.** Cek-lalu-insert; unique constraint + tangkap violation; in-memory lock.
**Pilihan.** `UNIQUE (RequesterId, ClientRequestId)` + tangkap `DbUpdateException`/`SqliteException` → re-query → kembalikan request asli.
**Konsekuensi.** Benar terhadap race. Kode harus membedakan violation unique dari error DB lain.

## ADR-004 — Optimistic concurrency dengan conditional UPDATE

**Konteks.** Dua approver bisa bertindak pada version yang sama.
**Opsi.** EF concurrency token; `ExecuteUpdate` + rows affected; pessimistic lock.
**Pilihan.** `UPDATE ... WHERE Id=? AND Version=?`; rows affected == 0 → 409.
**Konsekuensi.** Semantik eksplisit dan mudah diuji; perlu re-read setelah update.

## ADR-005 — Audit append-only di lapisan DB

**Konteks.** §9 menyarankan constraint/trigger DB agar audit tak bisa diubah.
**Opsi.** Konvensi aplikasi saja; trigger DB.
**Pilihan.** Keduanya: repository tak mengekspos update/delete **dan** trigger `RAISE(ABORT)`.
**Konsekuensi.** Bahkan bug aplikasi tidak bisa merusak jejak audit.

## ADR-006 — Aturan routing di satu modul domain

**Konteks.** §9: `isHighRisk` dan routing tidak boleh tersebar di controller.
**Pilihan.** `Domain/ApprovalPolicy` sebagai satu-satunya definisi.
**Konsekuensi.** Unit test tabel kebenaran langsung; controller tipis.

## ADR-007 — Urutan cek 404 → 403 → 409

**Konteks.** Pesan error tidak boleh membocorkan keberadaan request yang tak boleh dilihat user.
**Pilihan.** Ketidaknampakan → 404 lebih dulu; baru otorisasi → 403; baru konflik → 409.
**Konsekuensi.** AC-AUTH-03 tetap terpenuhi untuk approver sah yang salah tahap; non-pemilik mendapat 404.

## ADR-008 — Trigger append-only ditulis lewat raw SQL di migration

**Konteks.** Trigger `RAISE(ABORT)` di SQLite perlu SQL mentah, bukan API EF.
**Pilihan.** `migrationBuilder.Sql(...)` pada migration.
**Konsekuensi.** Trigger ikut reproducible saat DB dibangun ulang.

---

## Asumsi (§10) — keputusan yang diambil

| # | Asumsi | Pilihan | Konsekuensi |
|---|--------|---------|-------------|
| A1 | Siapa yang boleh create | Hanya user dengan `ManagerId` tidak null. | User tanpa manager (Bob/Carol/Dana/Erin) tidak melihat form create; disertai pesan jelas. |
| A2 | System Owner request ke aplikasinya sendiri | Tidak boleh self-approve; **tidak ada fallback** → request tertahan `PendingSystemOwner`. | Didokumentasikan sebagai known limitation; UI menandai "no eligible approver". Alternatif (tolak saat create) ditolak karena menambah aturan bisnis yang tidak diminta task. |
| A3 | Scope ClientRequestId | Unik **per requester**, bukan global. | Constraint `(RequesterId, ClientRequestId)`. |
| A4 | Retry payload berbeda, ClientRequestId sama | Kembalikan request asli; payload baru diabaikan. | Idempotency menang atas validasi ulang. |
| A5 | Pesan 409 | Dibedakan: `TERMINAL_STATE` vs `STALE_VERSION`. | Klien bisa membedakan "sudah selesai" dari "data basi". |
| A6 | Duplikat bisnis | Tidak dilarang; dicatat sebagai limitation. | Dua request identik dengan ClientRequestId berbeda tetap sah. |
| A7 | Cancel/withdraw & backup approver | Deferred. | Ditulis di REVIEW.md bagian deferred work. |
| A8 | Isi audit event | actor, action, from→to, version, reason, timestamp, policy version. | Sesuai AC-AUDIT-02. |
| A9 | Visibilitas | Requester miliknya; approver sesuai tahap + scope; Erin semua. | Query terfilter per peran di server. |
