# INTEGRITY.md — Deklarasi Ownership & External Assistance (Phase 1)

## 1. Ownership

Mahindra Irvan Saputra (`uchihas538.zippo@gmail.com`) — pemilik seluruh keputusan desain,
arsitektur, dan bisnis yang diambil dalam proyek ini. Semua commit di `main` dan PR
(`feat/integration-dotnet-aspire`, `feat/login-sso`) adalah karya sendiri; AI dipakai
sebagai asisten penulisan/refactor, bukan pengambil keputusan.

## 2. AI tools

- **Claude Code (CLI)** — digunakan untuk: draft entity/service/repository/views/test,
  refactor clean-architecture, mencari akar penyebab kegagalan test, dan menyusun
  dokumentasi (`PLAN.md`, `AI_USAGE.md`, `REVIEW.md`, dokumen ini).
- Prompt diberikan dalam bahasa Indonesia; pola yang paling berpengaruh direkam di
  `AI_USAGE.md` (I-1..I-5).

## 3. External assistance (bukan AI)

- **Duende IdentityServer 8**, **ASP.NET Core Identity**, **Serilog**, **Aspire** —
  paket NuGet pihak ketiga (lisensi/logic mereka, bukan karya saya).
- **Bootstrap 5** — CSS framework di `wwwroot/lib/bootstrap` (template ASP.NET default).
- **Template scaffold** `dotnet new mvc` — titik awal struktur proyek; sebagian besar
  sudah diganti (controller, view, domain).
- **Stack Overflow / dokumentasi resmi .NET** — referensi API (`ExecuteUpdateAsync`,
  `WebApplicationFactory`, `ModuleInitializer`), bukan kode yang disalin utuh.

## 4. Templates / snippets yang adaptasi

- Pola **state machine workflow** (`WorkflowRules.Resolve`) dan **conditional update
  concurrency** disusun sendiri berdasar pola EF Core umum — bukan copy snippet.
- Pola **idempotency lewat UNIQUE constraint** (`IsClientRequestIdViolation` menangkap
  `SqliteException 19/2067`) adalah kode asli, bukan dari template.
- **`ModuleInitializer`** di `AssemblyInfo.cs` mengikuti contoh dokumentasi .NET untuk
  set env var test (`DOTNET_USE_POLLING_FILE_WATCHER`).

## 5. Yang bukan karya saya

- Kode pihak ketiga di `bin/`, `obj/`, `wwwroot/lib/`, NuGet packages.
- Konten default template yang belum diubah di view/layout standar.

## 6. Pernyataan

Tidak ada credential, data perusahaan nyata, atau data pribadi di repository. Secret
(`Oidc:ClientSecret`, password akun dev, `Microsoft:ClientId/ClientSecret`) hanya lewat
user-secrets / environment AppHost dan tidak pernah di-commit (REVIEW.md R-02).
Karya ini dibuat untuk assessment take-home; keputusan bisnis dan kebenaran implementasi
sepenuhnya tanggung jawab saya.
