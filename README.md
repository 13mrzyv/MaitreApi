# MaitreApi

## 1. Layihə haqqında

MaitreApi restoranların məhsul və sifarişlərini idarə etmək üçün hazırlanmış API-dir.
Məhsul yaratmaq, qiyməti və satış statusunu dəyişmək, sifariş yaratmaq və izləmək mümkündür.
Ən çox satılan 5 məhsul üçün hesabat da mövcuddur.
Layihə C# / .NET 10, ASP.NET Core Minimal API, EF Core, PostgreSQL və Scalar istifadə edir.

## 2. Lazım olan proqramlar

- **.NET 10 SDK**
- **PostgreSQL 18** və onunla birlikdə quraşdırılan **psql**
- **Node.js 18 və ya daha yeni versiya** — avtomatlaşdırılmış testlər üçün

PostgreSQL servisi işləməlidir. PowerShell-də proqramları yoxlayın:

```powershell
dotnet --version
psql --version
node --version
```

`psql` tanınmırsa, PostgreSQL-in `bin` qovluğunu həmin terminalın PATH-inə əlavə edin
(quraşdırdığınız versiyaya uyğun yolu istifadə edin):

```powershell
$env:Path += ';C:\Program Files\PostgreSQL\18\bin'
```

## 3. PostgreSQL database qurulması

### Qoşulmaq və əsas bazanı yaratmaq

PowerShell-də PostgreSQL-ə qoşulun. Soruşulduqda quraşdırma zamanı seçdiyiniz şifrəni yazın:

```powershell
psql -h localhost -p 5432 -U postgres -d postgres
```

Açılan `psql` pəncərəsində icra edin:

```sql
CREATE DATABASE maitre_db;
```

Sonra `\q` yazıb PowerShell-ə qayıdın. `maitre_db` artıq mövcuddursa, yenidən yaratmayın.
Başqa istifadəçi ilə qoşulursunuzsa, `postgres` əvəzinə onun adını yazın;
bazanı yaratmaq üçün həmin istifadəçinin icazəsi olmalıdır.

### Connection string

`src/Maitre.Api/appsettings.json` daxilində `ConnectionStrings` hissəsini öz
PostgreSQL məlumatlarınıza uyğunlaşdırın:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=maitre_db;Username=postgres;Password=YOUR_POSTGRES_PASSWORD"
}
```

`Database` baza adı, `Username` PostgreSQL istifadəçisi, `Password` isə onun şifrəsidir.
`YOUR_POSTGRES_PASSWORD` nümunədir; öz şifrənizlə əvəz edin. Serveriniz başqa portdadırsa,
`Port` dəyərini də dəyişin. Real şifrə yazılmış faylı GitHub-a göndərməyin.

**Qeyd:** `$env:ConnectionStrings__DefaultConnection` təyin edilibsə,
`appsettings.json` dəyərini əvəz edir. Fayldakı bağlantıdan istifadə etmək üçün
həmin PowerShell pəncərəsində dəyişəni silin:

```powershell
Remove-Item Env:\ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
```

### Migration-ları tətbiq etmək

Terminalı **`Maitre.slnx` faylının yerləşdiyi layihə kökündə** açın.
Aşağıdakı yoxlama `True` qaytarmalıdır:

```powershell
Test-Path .\Maitre.slnx
```

Sonra ardıcıllıqla icra edin; xəta olarsa növbəti komandaya keçməyin:

```powershell
dotnet tool restore
dotnet restore
dotnet build --configuration Release
dotnet ef database update --project src/Maitre.Api --configuration Release --no-build
```

Migration-lar cədvəlləri və 3 restoranı yaradır: 

## 4. Layihəni işə salmaq

Eyni kök qovluqdan başladın:

```powershell
dotnet run --project src/Maitre.Api
```

Mövcud `http` profili tətbiqi Development mühitində başladır. Profili açıq seçmək üçün:

```powershell
dotnet run --project src/Maitre.Api --launch-profile http
```

Tətbiq işləyərkən brauzerdə açın:

- [Scalar API interfeysi](http://localhost:5102/scalar/v1)
- [Health yoxlaması](http://localhost:5102/health) — `Healthy` qaytarmalıdır.

## 5. Avtomatlaşdırılmış testlər

Testlər real PostgreSQL üzərində, əsas bazadan ayrı test bazasında işləyir.
Quraşdırma, təhlükəsizlik qaydaları və bütün test komandaları üçün
[test təlimatına](tests/TEST_README.md) baxın.


# Tapşırıq haqqında qeydlərim

## 1\. Əsas qərarlarım və onların səbəbləri

API-ni .NET 10 və Minimal API ilə hazırladım, çünki bu ölçüdə tapşırıq üçün sadə və rahat yanaşma idi.
Məhsul, sifariş və hesabat işlərini ayrı saxlamaq üçün Vertical Slice Architecture istifadə etdim, hər endpoint-in kodunu isə bir faylda topladım.
Database üçün PostgreSQL, sorğular və migration-lar üçün EF Core seçdim; əlavə repository və MediatR qatına ehtiyac görmədim.
Endpoint-ləri Scalar ilə yoxlamaq imkanı yaratdım və əsas qaydalar üçün real PostgreSQL üzərində işləyən avtomatlaşdırılmış testlər yazdım.

## 2\. Tələblərdəki açıq məqamlar üçün verdiyim qərarlar



Tələblərdə bəzi məqamlar tam dəqiq olmadığı üçün özüm qərarlar verdim.



\- Satış hesabatında ləğv edilməmiş bütün sifarişləri nəzərə aldım.

\- Eyni məhsul bir sifarişdə iki dəfə göndərilərsə, xəta qaytarmağı seçdim.

\- Fərqli məhsulların eyni adda olmasına icazə verdim.

\- Məhsulun adı və qiyməti sonradan dəyişsə də, əvvəlki sifarişlərdə köhnə məlumatların qalmasını təmin etdim.

\- Hesabatda eyni məhsulun fərqli adları varsa, satışları birləşdirib adlardan birini sabit qayda ilə göstərməyi seçdim.3. Tapşırığa nə qədər vaxt sərf etdim

Tapşırığa ümumilikdə 2-3 saat vaxt sərf etdim.
Bu müddətə layihənin qurulması, endpoint-lərin hazırlanması, testlər və sənədləşdirmə daxildir.

## 4\. Daha çox vaxtım olsaydı, nəyi dəyişərdim

Daha çox xəta və paralel sorğu ssenarisini testlərə əlavə edərdim.
Testlərin hər dəyişiklikdən sonra avtomatik işləməsi üçün GitHub Actions qurardım.
Böyük həcmdə sifarişlə siyahılama və hesabat sorğularının sürətini ölçər, nəticəyə görə təkmilləşdirmə edərdim.

