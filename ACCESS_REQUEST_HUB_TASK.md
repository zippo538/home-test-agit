# Access Request Hub: Ringkasan Tugas Assessment

Take-home Enterprise Fullstack Engineering Assessment (AI-assisted diperbolehkan). Target waktu **4-8 jam**, fokus pada **correctness dan engineering ownership**, bukan banyaknya fitur.

## 1. Latar Belakang

Permintaan akses aplikasi internal masih lewat email/chat sehingga sulit dilacak, request bisa terkirim dua kali, dan dua approver bisa bertindak bersamaan pada request yang sama. Tugas: membangun MVP **Access Request Hub** sebagai single source of truth untuk request, approval, dan audit trail. Berjalan lokal dengan seeded users (tanpa SSO, email, atau cloud).

## 2. Demo Users

| User | Peran |
|---|---|
| alice@example.local | Requester, atasan: Bob |
| bob@example.local | Manager Alice (Manager approval) |
| carol@example.local | System Owner CRM |
| dana@example.local | System Owner Finance Portal |
| erin@example.local | Admin/Auditor (lihat semua request + audit, **bukan approver**) |

## 3. Master Data

- **Application**: CRM (owner Carol), Finance Portal (owner Dana)
- **Environment**: NonProduction, Production
- **Access Level**: Read, Admin
- **PolicyVersion** Phase 1: `v1`

## 4. Field Access Request

| Field | Ketentuan |
|---|---|
| ClientRequestId | Untuk idempotency; retry tidak boleh membuat request kedua |
| Requester | Dari current user, bukan dari payload |
| Application | CRM / Finance Portal |
| Environment | NonProduction / Production |
| AccessLevel | Read / Admin |
| Justification | Wajib diisi |
| Status | State model, mencegah illegal transition |
| PolicyVersion | `v1` untuk request Phase 1 |
| Version | Concurrency token |

## 5. Workflow dan Routing

**High-risk** = Environment = Production **ATAU** AccessLevel = Admin.

| Kondisi | Jalur approval |
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

Pseudocode:

```
isHighRisk = (environment == Production) OR (accessLevel == Admin)

saat Manager approve:
    jika isHighRisk -> PendingSystemOwner
    jika tidak      -> Approved
saat System Owner approve:
    -> Approved
```

## 6. Business Rules Wajib

1. Semua request butuh Manager approval terlebih dahulu.
2. Non-high-risk menjadi Approved setelah Manager approve.
3. High-risk lanjut ke System Owner approval setelah Manager approve.
4. Manager hanya memproses **direct report** yang menunggu Manager approval.
5. System Owner hanya memproses request untuk **aplikasi miliknya**.
6. Requester tidak boleh approve request sendiri.
7. Reject **wajib** ada reason.
8. Approved dan Rejected adalah **terminal**; transisi berikutnya ditolak.
9. Create request **idempotent** berdasarkan ClientRequestId, aman terhadap race.
10. Approve/reject terlindungi dari stale/concurrent action; hanya satu transisi per version yang berhasil.
11. Setiap transisi kritis menyimpan **audit event secara atomic** dengan perubahan state.
12. PolicyVersion saat request dibuat harus disimpan.
13. Authorization **wajib server-side**.

## 7. Skenario Demo Wajib

| Skenario | Expected outcome |
|---|---|
| Standard request | Alice → CRM, NonProduction, Read. Bob approve → Approved |
| Production request | Alice → CRM, Production, Read. Bob approve → menunggu Carol; Carol approve → Approved |
| Admin access | Alice → Finance Portal, NonProduction, Admin. Bob approve → menunggu Dana |
| Unauthorized approval | User yang bukan assigned approver memanggil endpoint → ditolak backend |
| Duplicate submit | ClientRequestId sama dikirim ulang/hampir bersamaan → tetap satu request |
| Concurrent action | Dua action pada version yang sama → satu berhasil, lainnya conflict yang jelas |
| Rejected request | Reason tersimpan, audit event ada, request tidak bisa diproses lagi |

## 8. Scope Teknis

### Frontend
- User switcher (login simulasi)
- Form create request dengan validation feedback
- My Requests (list + detail)
- Approval Inbox untuk Manager/System Owner
- Detail request dengan status dan audit timeline
- State loading, empty, error/conflict

### Backend / API
- Endpoint: create, list/detail, approval inbox, approve, reject
- Validasi dan authorization server-side
- Idempotency create yang aman terhadap race
- Optimistic concurrency untuk approve/reject
- Error response berbeda untuk validation (400), forbidden (403), not found (404), conflict (409)
- Basic logging

### Database
- Relational DB (SQLite/PostgreSQL/SQL Server/MySQL)
- PK, FK, unique constraint, dan constraint relevan
- Migration/schema + seed yang reproducible
- Audit trail append-only
- Invariant penting dijaga oleh constraint DB

### Automated Tests
- Flow standard dan high-risk
- Minimal 1 authorization failure
- Minimal 1 idempotency
- Minimal 1 stale/concurrency
- Command test ditulis di README

## 9. Kontrol Teknis yang Disarankan

| Kebutuhan | Pendekatan |
|---|---|
| Idempotency | Unique constraint di DB (per requester + ClientRequestId); tangani violation dengan mengembalikan request asli |
| Concurrency | `UPDATE ... WHERE id=? AND version=?`, cek rows affected, 0 berarti 409 |
| Audit atomic | State change dan audit event dalam satu transaksi |
| Audit append-only | Tanpa endpoint update/delete; idealnya trigger DB |
| Isolasi aturan | Fungsi `isHighRisk` dan routing di satu modul domain, bukan di controller |

## 10. Asumsi yang Perlu Ditulis di PLAN.md

1. Siapa yang boleh membuat request (hanya user yang punya manager?).
2. System Owner mengajukan request ke aplikasinya sendiri: tidak boleh self-approve dan tidak ada fallback (request tertahan atau ditolak saat create).
3. Scope ClientRequestId: unik per requester atau global.
4. Retry dengan payload berbeda: kembalikan request asli atau konflik.
5. Pesan conflict dibedakan: "sudah terminal" vs "version stale".
6. Duplikat bisnis (ClientRequestId berbeda, akses sama) tidak dilarang, dicatat sebagai limitation.
7. Tidak ada cancel/withdraw dan backup approver (deferred).
8. Isi audit event: actor, action, status dari→ke, version, reason, timestamp, policy version.
9. Visibilitas: requester hanya miliknya, approver sesuai tahap dan scope, Erin semua.

## 11. Deliverable

| Item | Isi |
|---|---|
| Git | History bertahap, tag `assessment-start` dan `phase-1-complete` |
| PLAN.md | Pemahaman masalah, arsitektur/data model, urutan implementasi, strategi test, 2-3 trade-off, perubahan plan |
| AI_USAGE.md | 2-5 interaksi AI paling berpengaruh + section "Three things AI got wrong" |
| REVIEW.md | Self-review: finding, severity, action, status, evidence; known limitations; deferred work |
| INTEGRITY.md | Deklarasi ownership, tools AI, bantuan eksternal, template/snippet |
| README.md | Setup, run, migrate/seed, test, demo users, demo flow |

## 12. Aturan Penting

- **Jangan implementasi Phase 2** sebelum dokumen Change Request diberikan assessor.
- Jangan perpanjang timebox; jika mendekati 8 jam, berhenti di state stabil dan tulis deferred work di REVIEW.md.
- Tidak boleh ada credential atau data perusahaan nyata di repository.
- Tidak perlu: OAuth/OIDC, email nyata, microservices, broker, Kubernetes, cloud deployment.

## 13. Urutan Pengerjaan yang Disarankan

1. Tag `assessment-start` + PLAN.md
2. Schema, constraint, seed
3. Domain logic (routing, state machine, authorization)
4. API + error mapping
5. Test backend (termasuk concurrency dan idempotency)
6. Frontend minimal
7. Dokumentasi, lalu tag `phase-1-complete`

## 14. Definition of Done (Phase 1)

Flow create → approve/reject → audit berjalan, business rules enforced di backend, database reproducible, test utama lulus, evidence engineering lengkap, dan tag `phase-1-complete` tersedia.
