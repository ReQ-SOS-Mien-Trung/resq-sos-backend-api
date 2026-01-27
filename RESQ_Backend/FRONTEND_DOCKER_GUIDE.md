# 🐳 Hướng dẫn chạy RESQ Backend bằng Docker

## Yêu cầu
- Docker Desktop đã được cài đặt
- Không cần cài .NET SDK
- Không cần pull source code backend

## 🚀 Quick Start

### Bước 1: Tạo thư mục và file cấu hình

Tạo một thư mục mới và tạo file `docker-compose.yml` với nội dung sau:

```yaml
version: "3.9"

services:
  postgres:
    image: postgis/postgis:15-3.4
    container_name: resq_postgres
    restart: always
    environment:
      POSTGRES_DB: resq
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: 12345
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres -d resq"]
      interval: 5s
      timeout: 5s
      retries: 10
    networks:
      - resq-network

  api:
    # ⚠️ THAY ĐỔI IMAGE NAME THEO DOCKER HUB CỦA TEAM BACKEND
    image: your-dockerhub-username/resq-backend:latest
    container_name: resq_api
    restart: always
    depends_on:
      postgres:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__DefaultConnection: >
        Host=postgres;
        Port=5432;
        Database=resq;
        Username=postgres;
        Password=12345
      Jwt__Key: your-super-secret-jwt-key-at-least-32-chars
      Jwt__Issuer: resq.local
      Jwt__Audience: resq.local
    ports:
      - "5000:8080"
    networks:
      - resq-network

  redis:
    image: redis:7-alpine
    container_name: resq_redis
    restart: always
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data
    networks:
      - resq-network

volumes:
  postgres_data:
  redis_data:

networks:
  resq-network:
    driver: bridge
```

### Bước 2: Chạy Docker Compose

```bash
# Khởi động tất cả services
docker-compose up -d

# Xem logs
docker-compose logs -f api

# Dừng services
docker-compose down

# Dừng và xóa data
docker-compose down -v
```

### Bước 3: Kiểm tra API

API sẽ chạy tại: **http://localhost:5000**

Kiểm tra health:
```bash
curl http://localhost:5000/health
```

Swagger UI (nếu có): **http://localhost:5000/swagger**

---

## 📝 Cấu hình nâng cao

### Sử dụng file .env

Tạo file `.env` cùng thư mục với `docker-compose.yml`:

```env
# Docker image
DOCKER_REGISTRY=your-dockerhub-username
IMAGE_TAG=latest

# Database
POSTGRES_PASSWORD=your-secure-password

# JWT
JWT_KEY=your-super-secret-key-at-least-32-characters
JWT_ISSUER=resq.local
JWT_AUDIENCE=resq.local
```

Cập nhật `docker-compose.yml` để sử dụng biến môi trường:

```yaml
services:
  api:
    image: ${DOCKER_REGISTRY}/resq-backend:${IMAGE_TAG:-latest}
    environment:
      Jwt__Key: ${JWT_KEY}
      # ...
```

---

## 🔧 Troubleshooting

### API không khởi động được
```bash
# Xem logs chi tiết
docker logs resq_api

# Kiểm tra database đã ready chưa
docker logs resq_postgres
```

### Database connection failed
- Đảm bảo postgres container đã healthy
- Kiểm tra connection string trong environment

### Port đã được sử dụng
Thay đổi port mapping trong docker-compose.yml:
```yaml
ports:
  - "5001:8080"  # Đổi 5000 thành 5001
```

---

## 📞 Liên hệ

Nếu có vấn đề với Docker image, liên hệ team Backend.
