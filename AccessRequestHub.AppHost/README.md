# Integrasi Aspire untuk backend dan Identity Server

Paket integrasi berdasarkan dua Program.cs yang Anda unggah. Target AppHost: .NET 10, Aspire 13.1.0 (versi dipatok agar dapat direproduksi). Ini bukan salinan solution lengkap. Kedua proyek asli, dependency, view, service, konfigurasi, dan database tetap diperlukan. Framework proyek asli tidak dapat diketahui dari Program.cs saja. SDK/runtime yang sesuai proyek asli juga harus terpasang.

## 1. Penempatan

Letakkan folder AccessRequestHub.AppHost sejajar dengan folder backend dan Identity Server. Contoh:

- SolutionRoot/AccessRequestHub.AppHost/AccessRequestHub.AppHost.csproj
- SolutionRoot/home_test_agit/home_test_agit.csproj
- SolutionRoot/AccessRequestHub.IdentityServer/AccessRequestHub.IdentityServer.csproj

Nama folder dan csproj tersebut adalah asumsi dari namespace. Edit dua atribut Include di AccessRequestHub.AppHost.csproj sesuai proyek Anda. Pertahankan AspireProjectMetadataTypeName="Backend" dan "IdentityServer", karena Program.cs AppHost memakai alias tersebut. Jangan menambahkan referensi AppHost ke proyek anak.

Salin replacements/Backend/Program.cs menggantikan Program.cs backend. File replacements/IdentityServer/Program.cs adalah salinan lengkap file kedua Anda, tanpa perubahan, disediakan agar pasangan file jelas. Folder replacements tidak ditambahkan sebagai proyek dalam solution.

Backend menggunakan konfigurasi OIDC yang sudah diregistrasikan oleh AddAccessRequestHub. Tambahan ConfigureAll menyamakan authority, client ID, secret dan callback ketika dijalankan AppHost. Asumsi: backend memiliki satu skema OIDC untuk Identity Server ini dan memakai discovery standar. Jika backend memiliki beberapa skema OIDC, ubah ConfigureAll menjadi Configure<OpenIdConnectOptions>("NAMA_SKEMA_HUB", ...) memakai nama skema sebenarnya. Jangan memakai ConfigureAll untuk beberapa provider. Implementasi AddAccessRequestHub tidak disertakan, sehingga konfigurasi skema dan event internal belum dapat diperiksa. Jika service tersebut memasang ConfigurationManager khusus, sesuaikan di registrasi asli agar memakai authority AppHost.

## 2. Database SQLite

Periksa DatabasePaths di appsettings.json AppHost SEBELUM menjalankan aplikasi.

Default paket menggunakan database baru:
- ../.data/accessrequesthub.db untuk backend dan koneksi Hub Identity Server.
- ../.data/identity.db untuk akun Identity Server.

Jika ingin memakai data lama, ganti dengan path absolut ke database yang sudah dipakai. Contoh Windows: "C:/Projects/AccessRequestHub/data/hub.db". Jangan menghapus database lama. Path relatif selalu dihitung dari folder AppHost. Nama database default adalah pilihan untuk paket ini, bukan nama yang ditemukan dalam unggahan.

AppHost menyusun koneksi Data Source dengan path absolut sehingga kedua proses mengakses file Hub yang sama. Jika koneksi asli memakai opsi khusus SQLite, pertahankan opsi tersebut pada fungsi DatabaseConnection. Bootstrapper kedua aplikasi tetap berjalan sesuai kode asli; backend menunggu discovery Identity Server berhasil sehingga bootstrap awal tidak berjalan bersamaan. Perilaku idempotensi/seeding bootstrapper belum dapat diperiksa karena implementasinya tidak disertakan.

## 3. Prasyarat dan secret

Pasang .NET 10 SDK untuk AppHost, serta runtime yang ditargetkan kedua proyek asli. Restore memerlukan akses NuGet. Docker tidak diperlukan untuk paket ini karena hanya menjalankan proyek .NET dan SQLite lokal.

Jalankan dari SolutionRoot:

```sh
dotnet dev-certs https --trust
dotnet user-secrets set "Parameters:oidc-client-secret" "GANTI_DENGAN_SECRET_ACAK_ANDA" --project AccessRequestHub.AppHost
```

Ganti placeholder dengan secret acak yang kuat. Jangan commit secret. AppHost menyuntikkan nilai yang sama ke Oidc:ClientSecret kedua aplikasi. Trust sertifikat juga harus berlaku pada proses AppHost karena health check mengakses HTTPS. Pada Linux, trust browser dapat memerlukan langkah tambahan sesuai distro/browser.

Jika Microsoft external login digunakan, secret Microsoft yang sudah tersimpan di User Secrets proyek Identity Server tetap digunakan. Environment variable yang dahulu HANYA ada dalam launchSettings.json proyek anak tidak dimuat karena launchProfileName:null. Pindahkan konfigurasi tambahan tersebut ke User Secrets proyek yang bersangkutan atau tambahkan WithEnvironment di AppHost. Daftarkan callback Microsoft https://localhost:7043/signin-microsoft pada konfigurasi aplikasi Microsoft Anda jika fitur tersebut aktif.

## 4. Jalankan kedua proyek

Opsional, daftarkan AppHost dalam solution (sesuaikan nama solution):

```sh
dotnet sln NAMA_SOLUTION.sln add AccessRequestHub.AppHost/AccessRequestHub.AppHost.csproj
```

Untuk .slnx, gunakan nama file .slnx Anda. Menambahkan ke solution tidak diperlukan untuk dotnet run berikut.

```sh
dotnet restore AccessRequestHub.AppHost/AccessRequestHub.AppHost.csproj
dotnet run --project AccessRequestHub.AppHost --launch-profile https
```

Buka URL dashboard beserta token login yang tercetak di terminal. Di Visual Studio yang mendukung versi SDK ini, set AccessRequestHub.AppHost sebagai Startup Project lalu jalankan.

| Komponen | URL |
| --- | --- |
| Backend | https://localhost:7019 |
| Identity Server | https://localhost:7043 |
| OIDC discovery/health check | https://localhost:7043/.well-known/openid-configuration |
| Dashboard | https://localhost:17221 |

Backend menunggu health check discovery Identity Server berhasil, kemudian mulai. Selanjutnya keduanya berjalan di bawah satu AppHost. Ctrl+C pada AppHost menghentikan proses aplikasi yang dikelolanya.

Port backend 7019 sengaja dipertahankan karena terdaftar pada RedirectUris dan PostLogoutRedirectUris file Identity Server asli. Port 7043 adalah pilihan konfigurasi paket ini. Endpoints menggunakan HTTPS tanpa proxy dengan port dan targetPort sama agar origin browser dan issuer OIDC konsisten. Jangan menjalankan instance lama pada port yang sama. Jika appsettings proyek anak memiliki Kestrel:Endpoints eksplisit, selaraskan/hapus override development tersebut agar tidak bertabrakan dengan endpoint AppHost.

ServiceDefaults tidak diperlukan untuk orkestrasi dasar. Dashboard menampilkan status resource dan console log; paket ini belum menambahkan instrumentasi OpenTelemetry untuk distributed traces atau metrics aplikasi. WithReference saja tidak mengonfigurasi authority OIDC browser, sehingga authority diberikan secara eksplisit.

## 5. Pemeriksaan setelah integrasi

1. Restore/build berhasil setelah kedua path ProjectReference disesuaikan.
2. Identity Server hidup dan discovery mengembalikan HTTP 200, issuer https://localhost:7043.
3. Backend beralih dari Waiting ke Running setelah Identity Server sehat.
4. Buka https://localhost:7019 dan login memakai akun yang tersedia dari database/seeder asli.
5. Setelah login, callback kembali ke https://localhost:7019/signin-oidc dan akses role tetap sesuai aplikasi asli.
6. Logout kembali melalui /signout-callback-oidc tanpa invalid redirect URI.
7. Data Hub terlihat sama dari kedua aplikasi.

Jika Identity Server berhenti sebelum discovery tersedia, lihat console log resource tersebut. Backend akan tetap menunggu; perbaiki error database, dependency, secret atau bootstrapper lebih dahulu. Jika terjadi invalid_client, pastikan Oidc:ClientSecret tidak ditimpa konfigurasi khusus. Jika invalid redirect URI, pastikan browser membuka backend pada HTTPS port 7019.

## Batas verifikasi

Paket ditinjau terhadap dua Program.cs. Struktur XML/JSON dan isi arsip diperiksa. Build dan uji login end-to-end belum dijalankan: lingkungan pembuatan tidak memiliki dotnet SDK dan unggahan tidak mencakup solution, csproj, appsettings, maupun implementasi AddAccessRequestHub/bootstrappers. Paket ini untuk Development lokal, bukan konfigurasi deployment produksi.

## Referensi teknis

- https://aspire.dev/integrations/dotnet/project-resources/
- https://aspire.dev/fundamentals/networking-overview/
- https://aspire.dev/get-started/first-app/
- https://www.nuget.org/packages/Aspire.Hosting.AppHost/13.1.0