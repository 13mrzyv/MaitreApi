# Avtomatlaşdırılmış testlər

**`maitre_db` gündəlik istifadə üçündür; `maitre_test` yalnız testlər üçündür.**
Testləri əsas bazada işlətməyin. Aşağıdakı addımları layihə kökündə açılmış
**ayrıca PowerShell pəncərəsində** icra edin.

### Test bazasını yaratmaq və qoşulmaq

PostgreSQL istifadəçi adı hər sistemdə `postgres` olmaya bilər.
Aşağıda `$testUser` dəyərini öz PostgreSQL istifadəçi adınızla əvəz edin
(məsələn, `maitre_user`). Host və portu da öz serverinizə uyğunlaşdırın.
Bazanı yaradan istifadəçinin buna icazəsi olmalıdır.

```powershell
$testHost = 'localhost'
$testPort = 5432
$testUser = 'postgres' # Öz PostgreSQL istifadəçi adınızla əvəz edin.
$testDb = 'maitre_test'

psql -h $testHost -p $testPort -U $testUser -d postgres -c "CREATE DATABASE $testDb;"
psql -h $testHost -p $testPort -U $testUser -d $testDb -c "SELECT current_database();"
```

İkinci komanda `maitre_test` göstərməlidir. Baza artıq mövcuddursa ilk komandanı
keçin. Mövcud bazanı silməyin və ya sıfırlamayın. Soruşulan şifrə seçdiyiniz
istifadəçiyə aid olmalıdır; `-d postgres` isə istifadəçi deyil, qoşulmaq üçün
istifadə olunan inzibati bazanın adıdır.

### Test bağlantılarını təyin etmək

Aşağıdakı nümunədə `YOUR_POSTGRES_PASSWORD` yerinə öz lokal PostgreSQL şifrənizi
qoyun. İstifadəçi və port fərqlidirsə, dəyişənləri uyğunlaşdırın:

```powershell
# Əvvəlki addımdakı host, port, istifadəçi və baza dəyişənləri istifadə olunur.
$testPassword = 'YOUR_POSTGRES_PASSWORD'

$env:ConnectionStrings__DefaultConnection = "Host=$testHost;Port=$testPort;Database=$testDb;Username=$testUser;Password=$testPassword"
$urlUser = [Uri]::EscapeDataString($testUser)
$urlPassword = [Uri]::EscapeDataString($testPassword)
$env:PGTEST_URL = "postgresql://$($urlUser):$($urlPassword)@$($testHost):$($testPort)/$testDb"
$env:MAITRE_API_DLL = 'bin/Release/net10.0/Maitre.Api.dll'
```

Test bağlantısı parser-i sadə connection string gözləyir: test şifrəsində `;`,
dırnaq və əvvəl/sonda boşluq istifadə etməyin. URL üçün şifrə yuxarıda avtomatik kodlaşdırılır.

### Migration və testlər

Əvvəl setup bölməsindəki `dotnet tool restore` və `dotnet restore` addımlarının
icra olunduğuna əmin olun ([əsas README](../README.md)). Sonra:

```powershell
dotnet build --configuration Release
dotnet ef database update --project src/Maitre.Api --configuration Release --no-build
node tests/product-smoke.mjs
node tests/order-smoke.mjs
node tests/report-smoke.mjs
node tests/security-smoke.mjs
psql -X -v ON_ERROR_STOP=1 -d $env:PGTEST_URL -f tests/database-smoke.sql
```

Komandaları **bir-bir və ardıcıl** işlədin. Node testləri API-ni özləri boş lokal
portda başladır və sonda bağlayır; test üçün ayrıca `dotnet run` lazım deyil.
`test-support.mjs` köməkçi fayldır, ayrıca test kimi işə salınmır.

Testlər aşağıdakı təhlükəsizlik şərtlərini yoxlayır:

- Baza adı `_test` ilə bitməlidir; `maitre_test` uyğundur.
- Hər iki bağlantıda baza, host, port və istifadəçi eyni olmalıdır.
- Migration-lar tətbiq edilməli, Products, Orders və OrderItems cədvəlləri başlanğıcda boş olmalıdır. Seed restoranları qalmalıdır.

Boşluq yoxlaması keçmirsə, məlumatları avtomatik silməyin. Yeni baza adı seçin,
sonunu `_test` saxlayın və bütün test bağlantılarını həmin bazaya yönəldin.

Node testlərinin sonunda **`PASS`** görünməsi həmin test dəstinin yoxlamalarının
uğurlu olması deməkdir. SQL testi `DO`, `BEGIN`, `ROLLBACK` kimi nəticələr verir;
`ERROR` olmamalıdır. Uğurlu testlər yaratdıqları məlumatları təmizləyir;
proses qəfil bağlanarsa məlumat qala bilər.

Bitirdikdən sonra test terminalını bağlayın. Eyni terminaldan əsas bazaya qayıtmaq
istəyirsinizsə, əvvəl test dəyişənlərini silin:

```powershell
Remove-Item Env:\ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
Remove-Item Env:\PGTEST_URL -ErrorAction SilentlyContinue
Remove-Item Env:\MAITRE_API_DLL -ErrorAction SilentlyContinue
```
