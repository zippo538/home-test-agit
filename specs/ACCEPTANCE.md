# Acceptance Criteria — Access Request Hub (Phase 1)

Sumber: `ACCESS_REQUEST_HUB_TASK.md` §6 (Business Rules), §7 (Skenario Demo), §14 (Definition of Done).

## A. Autentikasi & Otorisasi

| ID | Kriteria |
|----|----------|
| AC-AUTH-01 | Belum ada user simulasi terpilih → akses halaman manapun diarahkan ke User Switcher. |
| AC-AUTH-02 | Requester diambil dari current user session, bukan dari payload form. |
| AC-AUTH-03 | User bukan assigned approver memanggil approve/reject → **403 Forbidden**. |
| AC-AUTH-04 | Erin (Admin/Auditor) melihat semua request + audit; approve/reject tidak tersedia. |
| AC-AUTH-05 | Requester approve/reject request sendiri → ditolak (rule 6), meski ia approver sah. |
| AC-AUTH-06 | Otorisasi **wajib** server-side; hide tombol di UI tidak dianggap cukup (rule 13). |

## B. Pembuatan Request & Idempotensi

| ID | Kriteria |
|----|----------|
| AC-CREATE-01 | Form create: Application, Environment, AccessLevel (dropdown master), Justification (wajib), ClientRequestId (generated client-side). |
| AC-CREATE-02 | Validasi enum terhadap master; Justification non-kosong. Pelanggaran → **400**. |
| AC-CREATE-03 | ClientRequestId sama dari requester sama → tidak ada request kedua; request asli dikembalikan. |
| AC-CREATE-04 | Dua create ClientRequestId sama paralel (race) → tepat satu baris terbuat; unique constraint DB menahan yang kedua. |
| AC-CREATE-05 | `PolicyVersion = "v1"` disimpan saat create (rule 12), immutable setelahnya. |
| AC-CREATE-06 | `Version` (concurrency token) diinisialisasi saat create. |
| AC-CREATE-07 | Status awal **wajib** `PendingManager` (rule 1). |

## C. State Machine & Routing

| ID | Kriteria |
|----|----------|
| AC-FLOW-01 | State valid: `PendingManager`, `PendingSystemOwner`, `Approved`, `Rejected`. |
| AC-FLOW-02 | `isHighRisk = (Environment == Production) OR (AccessLevel == Admin)` diisolasi di satu modul domain, bukan di controller. |
| AC-FLOW-03 | Manager approve non-high-risk → `Approved` (terminal). |
| AC-FLOW-04 | Manager approve high-risk → `PendingSystemOwner`. |
| AC-FLOW-05 | System Owner approve → `Approved` (terminal). |
| AC-FLOW-06 | Reject (Manager atau System Owner) → `Rejected` (terminal); reason **wajib** non-kosong (rule 7). |
| AC-FLOW-07 | Manager hanya memproses direct report miliknya yang `PendingManager` (rule 4). |
| AC-FLOW-08 | System Owner hanya memproses request `PendingSystemOwner` untuk aplikasi miliknya (rule 5). |
| AC-FLOW-09 | Transisi dari state terminal ditolak dengan conflict yang jelas (rule 8). |

## D. Concurrency & Conflict

| ID | Kriteria |
|----|----------|
| AC-CONC-01 | Approve/reject memakai `UPDATE ... WHERE Id=? AND Version=?`; rows affected == 0 → **409 Conflict**. |
| AC-CONC-02 | Dua action simultan pada Version sama → satu sukses, lainnya 409 bertema "version stale". |
| AC-CONC-03 | Action pada request terminal → 409 bertema "sudah terminal", dibedakan dari stale (asumsi §10.5). |
| AC-CONC-04 | Version bertambah satu setiap transisi sukses. |

## E. Audit Trail

| ID | Kriteria |
|----|----------|
| AC-AUDIT-01 | Audit event disimpan dalam **transaksi yang sama** dengan perubahan state (rule 11, atomic). |
| AC-AUDIT-02 | Audit memuat: Actor, Action, StatusFrom, StatusTo, Version, Reason, Timestamp, PolicyVersion (§10.8). |
| AC-AUDIT-03 | Append-only: tidak ada endpoint update/delete (rule 9). |
| AC-AUDIT-04 | Detail request menampilkan audit timeline kronologis. |

## F. Visibilitas (§10.9)

| ID | Kriteria |
|----|----------|
| AC-VIS-01 | Requester hanya melihat request miliknya di My Requests. |
| AC-VIS-02 | Manager melihat Approval Inbox berisi direct report yang `PendingManager`. |
| AC-VIS-03 | System Owner melihat request `PendingSystemOwner` untuk aplikasinya. |
| AC-VIS-04 | Erin melihat semua request + audit. |

## G. Skenario Demo Wajib (§7)

| ID | Skenario | Expected |
|----|----------|----------|
| AC-DEMO-01 | Alice → CRM, NonProduction, Read; Bob approve | `Approved` (single-hop) |
| AC-DEMO-02 | Alice → CRM, Production, Read; Bob approve → Carol | Bob → `PendingSystemOwner`; Carol → `Approved` |
| AC-DEMO-03 | Alice → Finance Portal, NonProduction, Admin; Bob approve | `PendingSystemOwner` (Dana) |
| AC-DEMO-04 | Non-approver memanggil approve | 403 Forbidden |
| AC-DEMO-05 | ClientRequestId sama disubmit ulang | Tetap satu request; response = request asli |
| AC-DEMO-06 | Dua action pada Version sama | Satu sukses, satu 409 |
| AC-DEMO-07 | Reject dengan reason | Reason tersimpan, audit ada, request terminal |

## H. Automated Tests (§8)

| ID | Kriteria |
|----|----------|
| AC-TEST-01 | Test flow standard (single-hop approve) lulus. |
| AC-TEST-02 | Test flow high-risk (two-hop approve) lulus. |
| AC-TEST-03 | ≥1 test authorization failure (403). |
| AC-TEST-04 | ≥1 test idempotensi (ClientRequestId sama). |
| AC-TEST-05 | ≥1 test stale/concurrency (409). |
| AC-TEST-06 | Command test ditulis di README.md dan reproducible. |

## I. Database & Reproducibility

| ID | Kriteria |
|----|----------|
| AC-DB-01 | Hapus DB → migrate + seed → demo user + master data kembali konsisten. |
| AC-DB-02 | PK, FK, unique constraint (ClientRequestId per requester), constraint relevan ada. |
| AC-DB-03 | Tabel audit terpisah, append-only (idealnya trigger DB). |
| AC-DB-04 | Invariant kritis dijaga constraint DB: unique, FK integrity, NOT NULL. |

## J. Frontend (§8)

| ID | Kriteria |
|----|----------|
| AC-UI-01 | User Switcher (login simulasi). |
| AC-UI-02 | Form create request dengan validation feedback (server + client). |
| AC-UI-03 | My Requests: list + navigasi detail. |
| AC-UI-04 | Approval Inbox untuk Manager dan/atau System Owner sesuai peran. |
| AC-UI-05 | Detail: status, field lengkap, tombol approve/reject (hanya bila berhak), audit timeline. |
| AC-UI-06 | State loading, empty, error/conflict ditampilkan. |

## K. Definition of Done (§11, §14)

| ID | Kriteria |
|----|----------|
| AC-DONE-01 | Git history bertahap; tag `assessment-start` dan `phase-1-complete`. |
| AC-DONE-02 | PLAN.md, AI_USAGE.md, REVIEW.md, INTEGRITY.md, README.md tersedia. |
| AC-DONE-03 | Flow create → approve/reject → audit berjalan end-to-end. |
| AC-DONE-04 | Business rules di-enforce di backend. |
| AC-DONE-05 | DB reproducible, test utama lulus, evidence lengkap. |
| AC-DONE-06 | Phase 2 tidak diimplementasi sebelum Change Request assessor (§12). |
