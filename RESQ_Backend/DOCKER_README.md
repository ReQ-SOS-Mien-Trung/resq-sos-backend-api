# RESQ Backend - Docker Setup

## 🚀 Quick Start cho Frontend Team

### Yêu cầu
- Docker Desktop đã cài đặt và đang chạy

### Cách sử dụng

1. **Copy file `docker-compose.yml` vào thư mục project frontend**

2. **Chạy lệnh:**
```bash
docker-compose up -d
```

3. **Chờ khoảng 30-60 giây để các services khởi động**

4. **Kiểm tra API:**
   - **API Base URL:** http://localhost:8080
   - **Swagger UI:** http://localhost:8080/swagger
   - **Health Check:** http://localhost:8080/health

### 🔑 Thông tin đăng nhập mẫu

| Role | Username | Password |
|------|----------|----------|
| Admin | `admin` | `Admin@123` |
| Coordinator | `coordinator` | `Coordinator@123` |
| Rescuer | `rescuer` | `Rescuer@123` |
| Manager | `manager` | `Manager@123` |
| Victim | `victim` | `Victim@123` |

### 📦 Services

| Service | Port | Mô tả |
|---------|------|-------|
| resq-api | 8080 | Backend API |
| resq-db | 5432 | PostgreSQL + PostGIS |
| resq-redis | 6379 | Redis Cache |

### Các lệnh Docker hữu ích

```bash
# Khởi động tất cả services
docker-compose up -d

# Dừng tất cả services
docker-compose down

# Xem logs
docker-compose logs -f resq-api

# Khởi động lại backend
docker-compose restart resq-api

# Xóa tất cả data và bắt đầu lại
docker-compose down -v
docker-compose up -d
```

### 🔄 Cập nhật image mới

```bash
docker-compose pull
docker-compose up -d
```

### ⚠️ Troubleshooting

**1. API không khởi động được:**
```bash
# Kiểm tra logs
docker-compose logs resq-api

# Đảm bảo database và redis đã sẵn sàng
docker-compose ps
```

**2. Không kết nối được database:**
```bash
# Xóa volume và tạo lại
docker-compose down -v
docker-compose up -d
```

**3. Port đã bị sử dụng:**
- Đổi port trong `docker-compose.yml`
- Ví dụ: `"8081:8080"` thay vì `"8080:8080"`
