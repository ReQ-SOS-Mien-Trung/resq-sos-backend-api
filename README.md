# RESQ Backend API

RESQ Backend API là dịch vụ backend cho hệ thống hỗ trợ cứu hộ khẩn cấp, quản lý SOS, điều phối nhiệm vụ, đội cứu hộ, kho cứu trợ, tài chính đóng góp và thông báo thời gian thực.

## 1. Thông tin chạy hệ thống

### Công nghệ chính

- .NET 8, ASP.NET Core Web API
- Entity Framework Core 9
- PostgreSQL + PostGIS
- Redis
- SignalR
- JWT Bearer Authentication
- Swagger/OpenAPI

### Chạy bằng Docker

```bash
docker compose up -d
```

Các endpoint sau khi chạy Docker:

| Thành phần | URL/Port |
| --- | --- |
| API Base URL | `http://localhost:8080` |
| Swagger UI | `http://localhost:8080/swagger` |
| Health Check | `http://localhost:8080/health` |
| PostgreSQL/PostGIS | `localhost:5432` |
| Redis | `localhost:6379` |

Chạy Docker build từ source local:

```bash
docker compose -f docker-compose.local.yml up --build -d
```

### Chạy trực tiếp bằng .NET

```bash
dotnet restore
dotnet run --project RESQ.Presentation/RESQ.Presentation.csproj
```

Các profile local trong `RESQ.Presentation/Properties/launchSettings.json`:

| Profile | URL/Port |
| --- | --- |
| `http` | `http://localhost:5219` |
| `https` | `https://localhost:7296;http://localhost:5219` |
| IIS Express | `http://localhost:10467`, SSL `44301` |

## 2. Cấu hình thành phần bên trong phần mềm

Các cấu hình chính nằm trong:

- `RESQ.Presentation/appsettings.json`
- `RESQ.Presentation/appsettings.Development.json`
- `RESQ.Presentation/appsettings.Production.json`
- `docker-compose.yml`
- `docker-compose.local.yml`

### AppSettings

| Key | Giá trị demo/local | Mô tả |
| --- | --- | --- |
| `AppSettings:BaseUrl` | `http://localhost:5219` khi Development, production là `https://resq-sos-backend-api-production.up.railway.app` | Base URL backend dùng khi tạo link verify email/callback |
| `AppSettings:FEBaseUrl` | `https://resq-sos-mientrung.vercel.app` | Base URL frontend dùng cho reset password, success/fail redirect |

### Connection String

Local từ `appsettings*.json`:

```text
ConnectionStrings:ResQDb=Host=localhost;Port=5432;Database=RESQ;Username=postgres;Password=12345
```

Docker từ `docker-compose*.yml`:

```text
ConnectionStrings__ResQDb=Host=resq-db;Port=5432;Database=RESQ;Username=postgres;Password=postgres123
```

Thông tin database container:

| Key | Giá trị |
| --- | --- |
| `POSTGRES_USER` | `postgres` |
| `POSTGRES_PASSWORD` | `postgres123` |
| `POSTGRES_DB` | `RESQ` |
| Port | `5432` |

### API Port

| Môi trường | Cấu hình |
| --- | --- |
| Local Development | `http://localhost:5219`, `https://localhost:7296` |
| Docker container | `ASPNETCORE_URLS=http://+:8080` |
| Docker host mapping | `8080:8080` |
| Production BaseUrl | `https://resq-sos-backend-api-production.up.railway.app` |

### JWT Token

JWT được cấu hình tại section `JwtSettings`.

| Key | Giá trị |
| --- | --- |
| `JwtSettings:SecretKey` | `YourSuperSecretKeyForJwtTokenGeneration_AtLeast32Characters!` |
| `JwtSettings:Issuer` | `RESQ.API` |
| `JwtSettings:Audience` | `RESQ.Client` |
| `JwtSettings:AccessTokenExpirationMinutes` | `10080` trong `appsettings.json`, Docker override `60` |
| `JwtSettings:RefreshTokenExpirationDays` | `7` |

Lấy access token bằng API đăng nhập:

```http
POST /identity/auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "Admin@123"
}
```

Gửi token cho các API cần xác thực:

```http
Authorization: Bearer {access_token}
```

SignalR hỗ trợ nhận JWT qua query string `access_token` với các hub `/hubs/*`.

### Redis

Docker service:

```text
Redis__ConnectionString=resq-redis:6379
```

Redis container expose port `6379`. Instance name mặc định trong code là `RESQ_`.

### Database bootstrap/seed

Trong `docker-compose.local.yml` có:

```text
Database__BootstrapOnStartup=true
```

Khi bật cấu hình này, API sẽ tạo schema và chạy seed demo khi khởi động.

## 3. Cấu hình dịch vụ bên thứ ba

Nên cấu hình secret bằng biến môi trường khi deploy. Với .NET, dùng dấu `__` để map nested config, ví dụ `PayOS__ApiKey`.

### Email - SendGrid/SMTP

Section: `EmailSettings`

| Key | Giá trị hiện có / yêu cầu |
| --- | --- |
| `Provider` | `SendGrid` hoặc `Smtp` |
| `ApiKey` | SendGrid API key, có thể override bằng `SENDGRID_API_KEY` |
| `FromEmail` | `anntpse182743@fpt.edu.vn`, có thể override bằng `SENDGRID_FROM_EMAIL` |
| `FromName` | `RESQ System`, có thể override bằng `SENDGRID_FROM_NAME` |
| `ApiBaseUrl` | Mặc định `https://api.sendgrid.com/v3/mail/send` |
| SMTP optional | `SmtpHost`, `SmtpPort`, `SmtpUsername`, `SmtpPassword`, `EnableSsl` |

### Google Authentication

Section: `GoogleAuth`

| Key | Giá trị |
| --- | --- |
| `ClientId` | `431645974494-dc3n3v13n483nk25jmukubmrr3j6levu.apps.googleusercontent.com` |
| `ClientSecret` | Cấu hình trong `appsettings*.json` hoặc biến môi trường `GoogleAuth__ClientSecret` |

Endpoint liên quan:

```http
POST /identity/auth/google-login
```

### Firebase Admin SDK

Section: `Firebase`

| Key | Giá trị chính |
| --- | --- |
| `ProjectId` | `prm-pe-142` |
| `ClientEmail` | `firebase-adminsdk-fbsvc@prm-pe-142.iam.gserviceaccount.com` |
| `TokenUri` | `https://oauth2.googleapis.com/token` |
| `PrivateKey` | Service account private key, đặt trong `appsettings*.json` hoặc file mounted |

Docker mount file service account:

```yaml
source: ./firebase-admin.json
target: /app/PRM PE 142 Firebase Admin SDK.json
```

Endpoint liên quan:

```http
POST /identity/auth/firebase-phone-login
```

### PayOS

Section: `PayOS`

| Key | Giá trị |
| --- | --- |
| `ClientId` | `7d133e03-46cb-4827-b04e-c07b97b9dc0c` |
| `ApiKey` | PayOS API key |
| `ChecksumKey` | PayOS checksum/HMAC key |
| `BaseUrl` | `https://api-merchant.payos.vn` |
| `ReturnUrl` | `https://resq-sos-mientrung.vercel.app/success` |
| `CancelUrl` | `https://resq-sos-mientrung.vercel.app/fail` |

### ZaloPay Sandbox

Section: `ZaloPay`

| Key | Giá trị |
| --- | --- |
| `AppId` | `554` |
| `Key1` | ZaloPay key dùng tạo đơn/query |
| `Key2` | ZaloPay key dùng verify webhook/callback |
| `Endpoint` | `https://sb-openapi.zalopay.vn/v2/create` |
| `QueryEndpoint` | `https://sb-openapi.zalopay.vn/v2/query` |
| `CallbackUrl` | `https://resq-sos-backend-api-production.up.railway.app/finance/donations/zalopay-return` |
| `RedirectUrl` | `https://resq-sos-mientrung.vercel.app/success` |
| `CancelUrl` | `https://resq-sos-mientrung.vercel.app/fail` |

### MoMo

Code có service `MomoPaymentService`, nhưng `appsettings*.json` hiện chưa khai báo section `MomoAPI`. Nếu bật MoMo cần bổ sung:

| Key | Mô tả |
| --- | --- |
| `MomoAPI:MomoApiUrl` | Endpoint tạo payment, mặc định code fallback `https://test-payment.momo.vn/v2/gateway/api/create` |
| `MomoAPI:PartnerCode` | Partner code |
| `MomoAPI:AccessKey` | Access key |
| `MomoAPI:SecretKey` | Secret key ký HMAC |
| `MomoAPI:RedirectUrl` | URL redirect sau thanh toán |
| `MomoAPI:IpnUrl` | Webhook/IPN URL |

### AI Providers

Section: `AiProviders`

| Provider | Key | Giá trị |
| --- | --- | --- |
| Gemini | `ApiUrl` | `https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}` |
| Gemini | `DefaultModel` | `gemini-2.5-flash` |
| Gemini | `ApiKey` | Cần cấu hình qua DB/admin hoặc biến môi trường nếu dùng trực tiếp |
| OpenRouter | `ApiUrl` | `https://openrouter.ai/api/v1/chat/completions` |
| OpenRouter | `DefaultModel` | `openai/gpt-4o-mini` |
| OpenRouter | `ApiKey` | Cần cấu hình qua DB/admin hoặc biến môi trường nếu dùng trực tiếp |

Prompt secret:

```text
PromptSecrets:MasterKey=masterkey
```

### Goong Map API

Service `GoongMapService` yêu cầu:

| Key | Giá trị |
| --- | --- |
| `Goong:ApiKey` | API key Goong, chưa có sẵn trong `appsettings*.json` |
| `Goong:DirectionBaseUrl` | Mặc định `https://rsapi.goong.io` |

Endpoint dùng cho tính tuyến đường nhiệm vụ/cứu hộ.

## 4. Roles trong hệ thống

| RoleId | Role | Mô tả |
| --- | --- | --- |
| `1` | `Admin` | Quản trị toàn hệ thống |
| `2` | `Coordinator` | Điều phối SOS, mission, đội cứu hộ |
| `3` | `Rescuer` | Thành viên/đội trưởng đội cứu hộ |
| `4` | `Manager` | Quản lý kho cứu trợ |
| `5` | `Victim` | Người dân/người tạo SOS |

## 5. Tài khoản đăng nhập demo

API login thường dùng `username` và `password`:

```http
POST /identity/auth/login
```

Body:

```json
{
  "username": "admin",
  "password": "Admin@123"
}
```

### Tài khoản seed runtime hiện tại

| Role | Username | Email mẫu | Password |
| --- | --- | --- | --- |
| Admin | `admin` | `admin@resq.vn` | `Admin@123` |
| Coordinator | `coord01` đến `coord05` | `coord01@resq.vn` | `Coordinator@123` |
| Manager | `manager01` đến `manager09` | `manager01@resq.vn` | `Manager@123` |
| Rescuer | `rescuer001` đến `rescuer200` | `rescuer001@resq.vn` | `Rescuer@123` |
| Victim | `victim001` đến `victim140` | `victim001@resq.vn` | `Victim@123` |
| Victim demo PIN | `victim.demo.374745872` | `victim.demo.374745872@resq.vn` | `142200` |

Các tài khoản demo nên dùng nhanh:

| Mục đích | Username | Password |
| --- | --- | --- |
| Admin dashboard | `admin` | `Admin@123` |
| Điều phối viên | `coord01` | `Coordinator@123` |
| Quản lý kho | `manager01` | `Manager@123` |
| Cứu hộ viên | `rescuer001` | `Rescuer@123` |
| Người dân tạo SOS | `victim001` | `Victim@123` |
| Người dân đăng nhập bằng PIN | `victim.demo.374745872` | `142200` |

### Tài khoản seed legacy nếu database đang dùng migration/fixture cũ

Một số DB cũ có thể có thêm các username sau:

| Role | Username | Password |
| --- | --- | --- |
| Coordinator | `coordinator` | `Coordinator@123` |
| Manager | `manager`, `manager2` đến `manager7` | `Manager@123` |
| Rescuer | `rescuer`, `rescuer1` đến `rescuer80` | `Rescuer@123` |
| Victim | `victim`, `applicant1` đến `applicant5` | `Victim@123` |

## 6. Ghi chú bảo mật khi nộp/chạy demo

- Các secret thật như SendGrid API key, Google client secret, PayOS/ZaloPay key và Firebase private key không nên public trong repository production.
- Khi deploy, ưu tiên đặt secret bằng environment variable thay vì hard-code trong `appsettings*.json`.
- Nếu lộ secret, cần rotate key trên dashboard của nhà cung cấp tương ứng trước khi chạy production.
