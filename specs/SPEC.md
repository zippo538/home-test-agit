# SPEC.md — Kontrak Teknis

## 1. Master Data (seed)

**Users**

| Email | Display | Manager | Peran fungsional |
|-------|---------|---------|------------------|
| alice@example.local | Alice | Bob | Requester |
| bob@example.local | Bob | — | Manager of Alice |
| carol@example.local | Carol | — | System Owner CRM |
| dana@example.local | Dana | — | System Owner Finance Portal |
| erin@example.local | Erin | — | Admin/Auditor (bukan approver) |

**Applications**: CRM (owner Carol), Finance Portal (owner Dana).
**Environments**: NonProduction, Production.
**AccessLevels**: Read, Admin.
**PolicyVersion**: `v1`.

## 2. Enumerasi

```
RequestStatus  = PendingManager | PendingSystemOwner | Approved | Rejected
AuditAction    = Created | ManagerApproved | ManagerRejected
               | SystemOwnerApproved | SystemOwnerRejected
```

Terminal = `Approved` | `Rejected`.

## 3. Domain Module (satu tempat aturan)

```
ApprovalPolicy
  bool IsHighRisk(Environment env, AccessLevel level)
      => env == Production || level == Admin

  RequestStatus NextAfterManagerApprove(Environment env, AccessLevel level)
      => IsHighRisk(env, level) ? PendingSystemOwner : Approved

  RequestStatus NextAfterSystemOwnerApprove() => Approved
```

```
ApprovalStage GetStage(RequestStatus status)
  PendingManager     -> Manager
  PendingSystemOwner -> SystemOwner
  Approved|Rejected  -> None (terminal)
```

## 4. Endpoint

| Method | Path | Tujuan | Sukses | Gagal |
|--------|------|--------|--------|-------|
| GET | `/Requests` | My Requests (requester) / semua (Erin) | 200 | — |
| GET | `/Requests/{id}` | Detail + audit timeline | 200 | 404 |
| GET | `/Requests/Create` | Form create | 200 | — |
| POST | `/Requests` | Create (idempoten) | 201 baru / 200 idempotent-hit | 400, 403 |
| GET | `/Approvals` | Approval Inbox sesuai peran | 200 | — |
| POST | `/api/requests/{id}/approve` | Approve tahap aktif | 200 | 400, 403, 404, 409 |
| POST | `/api/requests/{id}/reject` | Reject tahap aktif (reason wajib) | 200 | 400, 403, 404, 409 |
| GET/POST | `/Account/Switch` | User switcher (login simulasi) | 302 | — |

Approve/reject berupa endpoint JSON (`/api/...`) supaya jalur otorisasi identik antara UI dan test.

## 5. DTO

**CreateRequestInput** — `ClientRequestId` (wajib, generated client-side), `ApplicationId`, `EnvironmentId`, `AccessLevelId`, `Justification` (wajib). **Tidak ada** field `Requester` — diambil dari session.

**ApproveInput** — `Version` (int, wajib).
**RejectInput** — `Version` (wajib), `Reason` (wajib, non-kosong).

**RequestView** — `Id`, `Requester`, `Application`, `Environment`, `AccessLevel`, `Justification`, `Status`, `PolicyVersion`, `Version`, `CreatedAt`, `UpdatedAt`, `CanApprove`, `CanReject`.

**AuditEventView** — `Actor`, `Action`, `StatusFrom`, `StatusTo`, `Version`, `Reason`, `PolicyVersion`, `Timestamp`.

## 6. Error Model

Envelope seragam:

```json
{ "error": { "code": "STALE_VERSION", "message": "…", "details": {} } }
```

| HTTP | Code | Kapan |
|------|------|-------|
| 400 | `VALIDATION_FAILED` | Field wajib kosong, enum di luar master, `Reason` kosong saat reject |
| 403 | `FORBIDDEN` | Bukan assigned approver, self-approve, bukan pembuat |
| 404 | `NOT_FOUND` | Request tidak ada, atau tidak terlihat oleh user |
| 409 | `STALE_VERSION` | `Version` di payload ≠ `Version` di DB |
| 409 | `TERMINAL_STATE` | Request sudah `Approved`/`Rejected` |

**Urutan pengecekan: 404 → 403 → 409 → 200**, agar tidak membocorkan keberadaan request yang tak boleh dilihat.

## 7. Idempotency Create

1. Lookup `(RequesterId, ClientRequestId)` via LINQ.
2. Jika ada → kembalikan request asli, HTTP 200, tanpa mutasi apa pun (payload baru diabaikan — asumsi §10.4).
3. Jika tidak → insert request + insert audit event `Created` dalam satu transaksi.
4. Jika insert melanggar unique constraint (race) → `DbUpdateException` → re-query → kembalikan request asli.

## 8. Concurrency Approve/Reject

```
BEGIN TRANSACTION
  SELECT request WHERE Id = @id            -- cek visibilitas & status
  if not found              -> 404 NOT_FOUND
  if !CanAct(user, request) -> 403 FORBIDDEN
  if status is terminal     -> 409 TERMINAL_STATE
  rows = UPDATE AccessRequests
           SET Status = @next, Version = Version + 1, UpdatedAt = @now
           WHERE Id = @id AND Version = @expectedVersion
  if rows == 0              -> 409 STALE_VERSION        -- race kalah
  INSERT AuditEvent(...)                                -- atomik, transaksi sama
COMMIT
```

`UPDATE ... WHERE Version = @expectedVersion` adalah implementasi literal §9 baris "Concurrency".

## 9. Skema DB (ringkas)

```sql
CREATE TABLE Users (
  Id INTEGER PRIMARY KEY, Email TEXT NOT NULL UNIQUE, DisplayName TEXT NOT NULL,
  ManagerId INTEGER NULL REFERENCES Users(Id), Role TEXT NOT NULL);

CREATE TABLE Applications (
  Id INTEGER PRIMARY KEY, Name TEXT NOT NULL UNIQUE,
  OwnerUserId INTEGER NOT NULL REFERENCES Users(Id));

CREATE TABLE Environments (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL UNIQUE);
CREATE TABLE AccessLevels (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL UNIQUE);

CREATE TABLE AccessRequests (
  Id INTEGER PRIMARY KEY, ClientRequestId TEXT NOT NULL,
  RequesterId INTEGER NOT NULL REFERENCES Users(Id),
  ApplicationId INTEGER NOT NULL REFERENCES Applications(Id),
  EnvironmentId INTEGER NOT NULL REFERENCES Environments(Id),
  AccessLevelId INTEGER NOT NULL REFERENCES AccessLevels(Id),
  Justification TEXT NOT NULL,
  Status TEXT NOT NULL CHECK (Status IN ('PendingManager','PendingSystemOwner','Approved','Rejected')),
  PolicyVersion TEXT NOT NULL,
  Version INTEGER NOT NULL,
  CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
  UNIQUE (RequesterId, ClientRequestId));

CREATE TABLE AuditEvents (
  Id INTEGER PRIMARY KEY,
  RequestId INTEGER NOT NULL REFERENCES AccessRequests(Id),
  ActorUserId INTEGER NOT NULL REFERENCES Users(Id),
  Action TEXT NOT NULL CHECK (Action IN ('Created','ManagerApproved','ManagerRejected','SystemOwnerApproved','SystemOwnerRejected')),
  StatusFrom TEXT NULL, StatusTo TEXT NOT NULL,
  Version INTEGER NOT NULL, Reason TEXT NULL,
  PolicyVersion TEXT NOT NULL, Timestamp TEXT NOT NULL);

CREATE TRIGGER trg_audit_no_update BEFORE UPDATE ON AuditEvents
BEGIN SELECT RAISE(ABORT, 'AuditEvents is append-only'); END;

CREATE TRIGGER trg_audit_no_delete BEFORE DELETE ON AuditEvents
BEGIN SELECT RAISE(ABORT, 'AuditEvents is append-only'); END;
```

## 10. Struktur Folder

```
/Controllers     RequestsController, ApprovalsController, RequestsApiController, AccountController
/Domain          ApprovalPolicy, RequestStateMachine, AuthorizationService, Errors
/Data            AppDbContext, Migrations, Seed
/Models          Entities, ViewModels, Dtos
/Views           Requests/, Approvals/, Account/, Shared/
/tests           AccessRequestHub.Tests (xUnit)
```

## 11. Logging

Satu log terstruktur per transisi: `requestId`, `actorId`, `from`, `to`, `version`, `outcome` (`ok`/`stale`/`terminal`/`forbidden`). Tidak mencatat secret; tidak ada credential nyata (§12).
