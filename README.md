# 🏥 MedEase — Hệ Thống Quản Lý Phòng Khám Tư Nhân

> **Đồ án môn Công nghệ Phần mềm Nâng cao**  
> Case Study 04: Hệ Thống Phòng Khám Tư Nhân

---
# Danh Sách Thành Viên 
Nguyễn Hải Đăng
Nguyễn Phước Sang
Nguyễn Hữu Danh
Lâm Gia Bảo 
## 📋 Giới Thiệu

**MedEase (Healthy System)** là ứng dụng web quản lý phòng khám tư nhân, hỗ trợ đầy đủ quy trình từ đặt lịch hẹn, khám bệnh, đến quản lý hệ thống với 4 vai trò người dùng rõ ràng.

## 🛠️ Công Nghệ Sử Dụng

| Thành phần | Công nghệ |
|---|---|
| Framework | ASP.NET Core 8.0 MVC |
| Ngôn ngữ | C# |
| Database | PostgreSQL (Supabase) |
| ORM | Entity Framework Core 8 |
| Authentication | Cookie Authentication |
| Password Hashing | BCrypt.Net-Next |
| UI | Bootstrap 5, Font Awesome |

## 👥 Vai Trò Người Dùng

| Role | Mô tả | Giao diện |
|---|---|---|
| 🧑‍⚕️ **Bệnh nhân** (Patient) | Đặt lịch, xem lịch sử, huỷ/đổi lịch hẹn | Trang chủ công khai |
| 👨‍⚕️ **Bác sĩ** (Doctor) | Quản lý hàng chờ, khám bệnh, kê đơn | Cổng bác sĩ (dark navy) |
| 📋 **Lễ tân** (Receptionist) | Xác nhận lịch hẹn, đăng ký bệnh nhân vãng lai | Cổng lễ tân (dark green) |
| 🔧 **Quản trị** (Admin) | Quản lý người dùng, phân quyền, báo cáo | Admin dashboard |

## 🔐 Tài Khoản Test

> Mật khẩu mặc định cho tất cả tài khoản: `123456`

| Vai trò | Email |
|---|---|
| Admin | admin@healthysystem.vn |
| Lễ tân | reception@healthysystem.vn |
| Bệnh nhân | benhnhan@gmail.com |
| Bác sĩ 1 | bs.minhanh@healthysystem.vn |
| Bác sĩ 2 | bs.quanghuy@healthysystem.vn |
| Bác sĩ 3 | bs.thutrang@healthysystem.vn |

## 📁 Cấu Trúc Dự Án

```
Healthy System/
├── Controllers/
│   ├── HomeController.cs
│   ├── AccountController.cs
│   ├── AppointmentController.cs
│   ├── DoctorController.cs
│   ├── ReceptionistController.cs
│   └── AdminController.cs
├── Models/
│   ├── User.cs / Doctor.cs / Specialty.cs
│   ├── Appointment.cs / Service.cs / Equipment.cs
│   ├── DoctorSchedule.cs / Notification.cs / ContactMessage.cs
│   └── ViewModels/
├── Views/
│   ├── Home/ (Patient-facing)
│   ├── Account/ (Login, Register, Profile)
│   ├── Appointment/ (Book, MyAppointments, ...)
│   ├── Doctor/ (Dashboard, Queue, Examination, ...)
│   ├── Receptionist/ (Dashboard, Appointments, ...)
│   └── Admin/ (Dashboard)
├── Data/
│   ├── AppDbContext.cs
│   └── DbInitializer.cs
└── Migrations/
```

## ⚙️ Cài Đặt & Chạy

### 1. Clone dự án

```bash
git clone https://github.com/HaiDang-Ng22/medease-clinic-management.git
cd medease-clinic-management
```

### 2. Cấu hình Database

Mở `appsettings.json` và cập nhật connection string với mật khẩu Supabase của bạn:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=aws-0-ap-northeast-2.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.recelqensesdpwzdvslp;Password=YOUR_PASSWORD_HERE;SSL Mode=Require;Trust Server Certificate=true"
  }
}
```

### 3. Chạy Migration

```bash
dotnet ef database update
```

### 4. Chạy ứng dụng

```bash
dotnet run
```

Ứng dụng sẽ chạy tại: `http://localhost:5000`

## 📊 Sprint Planning

### Sprint 1 (Hoàn thành ✅)
- Đăng ký / Đăng nhập / Phân quyền
- Trang chủ bệnh nhân: chuyên khoa, bác sĩ, dịch vụ
- Đặt / Huỷ / Đổi lịch hẹn
- Cổng bác sĩ: Dashboard, Queue, Khám bệnh
- Cổng lễ tân: Xác nhận lịch hẹn
- Admin: Quản lý người dùng & phân quyền

### Sprint 2 (Kế hoạch)
- Hệ thống thông báo email
- Tìm kiếm bác sĩ nâng cao
- Báo cáo thống kê

### Sprint 3 (Kế hoạch)
- Quản lý thiết bị & vật tư
- Xuất báo cáo PDF
- Tối ưu hiệu năng & bảo mật

## 👨‍💻 Nhóm Phát Triển

- **HaiDang-Ng22** — Full-stack Developer

---

> 📌 Đây là đồ án môn học, không dùng cho mục đích thương mại.
