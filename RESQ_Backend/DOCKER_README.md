# RESQ Backend - Docker Setup

## 🚀 Quick Start cho Frontend Team

### Yêu cầu
- Docker Desktop đã cài đặt và đang chạy

### Cách sử dụng

1. **Copy file `docker-compose.yml` vào thư mục project frontend**

2. **Chạy lệnh:**
```bash
docker compose up -d
```

3. **Chờ khoảng 30-60 giây để các services khởi động**

4. **Kiểm tra API:**
   - **API Base URL:** http://localhost:8080
   - **Swagger UI:** http://localhost:8080/swagger
   - **Health Check:** http://localhost:8080/health

### 🔑 Thông tin đăng nhập mẫu

| Role | Username | Email | Password |
|------|----------|-------|----------|
| Admin | `admin` | — | `Admin@123` |
| Coordinator | `coordinator` | — | `Coordinator@123` |
| Rescuer | `rescuer` | `rescuer@resq.vn` | `Rescuer@123` |
| Manager | `manager` | — | `Manager@123` |
| Victim | `victim` | — | `Victim@123` |

> **Lưu ý:** API login dùng trường `username` (không phải email). Dùng `rescuer` / `Rescuer@123` để đăng nhập.

---

## 🦺 Test luồng nhận nhiệm vụ của Rescuer

Tài khoản `rescuer` đã được cấu hình sẵn trong seed data với đầy đủ dữ liệu để test luồng nhận & thực hiện nhiệm vụ.

### Dữ liệu seed sẵn có
| Mục | Giá trị |
|-----|---------|
| Team | **Team 4** — "Biệt đội Ca nô Hà Tĩnh" (TeamId = 4) |
| Vai trò trong team | Leader, Status = Accepted |
| Mission được giao | **Mission 1** — Rescue tại Lệ Thủy (MissionId = 1, Status = InProgress) |
| MissionTeam | Id = 1, Status = "Assigned" |
| Activity | Activity 1 — EVACUATE (Step 1, Status = InProgress) |
| Conversation | Conversation 1, rescuer là Leader |

### Luồng test từng bước

**Bước 1: Đăng nhập lấy token**
```
POST /auth/login
Body: { "username": "rescuer", "password": "Rescuer@123" }
→ Lấy access_token từ response
```

**Bước 2: Xem danh sách missions của team**
```
GET /operations/missions/my-team
Authorization: Bearer {access_token}
→ Trả về Mission 1 (Rescue, InProgress) cùng thông tin Team 4
```

**Bước 3: Xem chi tiết mission**
```
GET /operations/missions/1
Authorization: Bearer {access_token}
→ Trả về toàn bộ thông tin Mission 1 + activities
```

**Bước 4: Xem danh sách activities**
```
GET /operations/missions/1/activities
Authorization: Bearer {access_token}
→ Trả về Activity 1: EVACUATE tại xã An Thủy (Status = InProgress)
```

**Bước 5: Cập nhật trạng thái activity (bắt đầu / hoàn thành)**
```
PATCH /operations/missions/1/activities/1/status
Authorization: Bearer {access_token}
Body: { "status": "Completed" }
→ Đánh dấu activity hoàn thành
```

**Bước 6: Lấy tuyến đường đến địa điểm**
```
GET /operations/missions/1/activities/1/route?originLat=17.22&originLng=106.78&vehicle=car
Authorization: Bearer {access_token}
→ Trả về tuyến đường từ vị trí rescuer đến xã An Thủy
```

**Bước 7: Xem cuộc hội thoại mission**
```
GET /operations/conversations/mission/1
Authorization: Bearer {access_token}
→ Trả về Conversation 1 của Mission 1
```

---

### 📦 Services

| Service | Port | Mô tả |
|---------|------|-------|
| resq-api | 8080 | Backend API |
| resq-db | 5432 | PostgreSQL + PostGIS |
| resq-redis | 6379 | Redis Cache |

### Các lệnh Docker hữu ích

```bash
# Khởi động tất cả services
docker compose up -d

# Dừng tất cả services
docker compose down

# Xem logs
docker compose logs -f resq-api

# Khởi động lại backend
docker compose restart resq-api

# Xóa tất cả data và bắt đầu lại
docker compose down -v
docker compose up -d
```

### 🔄 Cập nhật image mới

```bash
docker compose pull
docker compose up -d
```

### 💻 Chạy local từ source backend

Nếu đang đứng trong repo backend và muốn build image từ source hiện tại thay vì dùng image Docker Hub:

```bash
docker compose -f docker-compose.local.yml up --build -d
```

File `docker-compose.local.yml` dùng image tag local để tránh đụng với image Docker Hub cũ trong máy.

### ⚠️ Troubleshooting

**1. API không khởi động được:**
```bash
# Kiểm tra logs
docker compose logs resq-api

# Đảm bảo database và redis đã sẵn sàng
docker compose ps
```

**2. Không kết nối được database:**
```bash
# Xóa volume và tạo lại
docker compose down -v
docker compose up -d
```

**3. Lỗi `relation "donations" does not exist`:**

Lỗi này gần như luôn là do schema trong volume Postgres cũ hơn image backend đang chạy, hoặc máy đang giữ image backend cũ chưa chứa migration mới.

```bash
# Nếu đang dùng image prebuilt cho frontend team
docker compose pull
docker compose down -v
docker compose up -d
```

```bash
# Nếu đang chạy backend repo local từ source
docker compose -f docker-compose.local.yml down -v
docker compose -f docker-compose.local.yml up --build -d
```

Nếu vẫn còn lỗi sau khi reset volume, backend image đang dùng chưa được publish kèm migration mới và cần build/push lại image `ntpan04/resq-backend:latest`.

**4. Port đã bị sử dụng:**
- Đổi port trong `docker-compose.yml`
- Ví dụ: `"8081:8080"` thay vì `"8080:8080"`
