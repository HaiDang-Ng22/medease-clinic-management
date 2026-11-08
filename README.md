# 🏥 MedEase — Hệ Thống Quản Lý Phòng Khám Tư Nhân

> **Đồ án môn Công nghệ Phần mềm Nâng cao**  
> Case Study 04: Hệ Thống Phòng Khám Tư Nhân

---

## 📋 Giới Thiệu

**MedEase (Healthy System)** là ứng dụng web quản lý phòng khám tư nhân toàn diện, hỗ trợ xuyên suốt quy trình từ đặt lịch hẹn, tiếp nhận tại quầy, khám bệnh, xét nghiệm cận lâm sàng đến thanh toán viện phí, thống kê doanh thu và quản trị hệ thống với các vai trò người dùng được phân quyền chặt chẽ.

## 🛠️ Công Nghệ Sử Dụng

| Thành phần | Công nghệ |
|---|---|
| Framework | ASP.NET Core 8.0 MVC |
| Ngôn ngữ | C# |
| Database | PostgreSQL (Supabase) / SQLite (Local Dev & Fallback) |
| ORM | Entity Framework Core 8 |
| Authentication | Cookie Authentication |
| Password Hashing | BCrypt.Net-Next |
| UI | Bootstrap 5, Font Awesome |

## 👥 Vai Trò Người Dùng

| Role | Mô tả | Giao diện |
|---|---|---|
| 🧑‍⚕️ **Bệnh nhân** (Patient) | Đặt lịch online, xem lịch sử khám & hồ sơ bệnh án, huỷ/đổi lịch hẹn | Trang chủ công khai |
| 📋 **Lễ tân** (Receptionist) | Xác nhận lịch hẹn online, tra cứu bệnh nhân, tạo lịch khám tại quầy | Cổng lễ tân (dark green) |
| 👨‍⚕️ **Bác sĩ** (Doctor) | Quản lý hàng chờ, khám bệnh, kê đơn thuốc điện tử, lập phiếu & xem kết quả XN | Cổng bác sĩ (dark navy) |
| 🔬 **Kỹ thuật viên** (LabTech) | Nhận phiếu chỉ định xét nghiệm, nhập số liệu kết quả, upload ảnh cận lâm sàng | Phân hệ Cận lâm sàng |
| 💰 **Kế toán / Thu ngân** (Accountant / Cashier) | Tính tổng chi phí khám, xác nhận thanh toán, xuất hóa đơn/phiếu thu, xem báo cáo doanh thu & lương | Phân hệ Kế toán & Thu ngân |
| 🔧 **Quản trị** (Admin) | Quản lý người dùng, phân quyền, quản lý danh mục dịch vụ & thiết bị y tế | Admin dashboard |

## 🔐 Tài Khoản Test (Demo)

> Mật khẩu mặc định cho tất cả tài khoản: `123456`

| Vai trò | Email |
|---|---|
| Admin | `admin@healthysystem.vn` |
| Lễ tân | `reception@healthysystem.vn` |
| Bệnh nhân | `benhnhan@gmail.com` |
| Bác sĩ 1 | `bs.minhanh@healthysystem.vn` |
| Bác sĩ 2 | `bs.quanghuy@healthysystem.vn` |
| Bác sĩ 3 | `bs.thutrang@healthysystem.vn` |
| Kỹ thuật viên XN | `labtech@healthysystem.vn` |
| Kế toán / Thu ngân | `ketoan@healthysystem.vn` |

## 📁 Cấu Trúc Dự Án

```text
Healthy System/
├── Controllers/
│   ├── HomeController.cs
│   ├── AccountController.cs
│   ├── AppointmentController.cs
│   ├── DoctorController.cs
│   ├── ReceptionistController.cs
│   ├── LabTechController.cs
│   ├── AccountantController.cs
│   ├── CashierController.cs
│   ├── PayrollController.cs
│   └── AdminController.cs
├── Models/
│   ├── User.cs / Doctor.cs / Specialty.cs
│   ├── Appointment.cs / Service.cs / Equipment.cs
│   ├── DoctorSchedule.cs / Notification.cs / ContactMessage.cs
│   ├── Prescription.cs / LabTest.cs / PatientCost.cs / Invoice.cs
│   └── ViewModels/
├── Views/
│   ├── Home/ (Patient-facing)
│   ├── Account/ (Login, Register, Profile, AccessDenied)
│   ├── Appointment/ (Book, MyAppointments, ...)
│   ├── Doctor/ (Dashboard, Queue, Examination, PrescriptionForm, LabResultView, ...)
│   ├── Receptionist/ (Dashboard, Appointments, CreateAppointment, SearchPatient, ...)
│   ├── LabTech/ (Dashboard, EnterResult, ...)
│   ├── Accountant/ (PatientCosts, RevenueReport, ...)
│   ├── Cashier/ (InvoiceReceipt, ...)
│   └── Admin/ (Dashboard, Services, Equipment, Users, ...)
├── Data/
│   ├── AppDbContext.cs
│   └── DbInitializer.cs
├── Migrations/
├── wwwroot/
│   └── uploads/labresults/ (Lưu trữ hình ảnh kết quả xét nghiệm/X-quang)
└── TestData/ (Dữ liệu kiểm thử & kịch bản test)
```

## 💻 Yêu Cầu Hệ Thống (Prerequisites)

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) trở lên
- Cơ sở dữ liệu: PostgreSQL (Supabase) hoặc SQLite (hỗ trợ chạy offline/local)
- Git

## ⚙️ Cài Đặt & Chạy Ứng Dụng

### 1. Clone dự án

```bash
git clone https://github.com/HaiDang-Ng22/medease-clinic-management.git
cd medease-clinic-management
```

### 2. Cấu hình Database (Bảo mật)

Ứng dụng hỗ trợ chạy ngay với **SQLite** ở môi trường local hoặc kết nối **PostgreSQL (Supabase)**. Để đảm bảo an toàn bảo mật, **không** lưu trực tiếp mật khẩu thật trong `appsettings.json`. Hãy thiết lập chuỗi kết nối thông qua biến môi trường hoặc `dotnet user-secrets`:

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=aws-0-ap-northeast-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.skkryypazfjdqmrnsqna;Password=YOUR_PASSWORD_HERE;SSL Mode=Require;Trust Server Certificate=true"
```

Hoặc cập nhật trực tiếp trong `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=aws-0-ap-northeast-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.skkryypazfjdqmrnsqna;Password=YOUR_PASSWORD_HERE;SSL Mode=Require;Trust Server Certificate=true"
  }
}
```

### 3. Chạy Migration & Khởi tạo dữ liệu mẫu

```bash
dotnet ef database update
```

### 4. Chạy ứng dụng

```bash
dotnet run
```

Ứng dụng sẽ khởi chạy tại địa chỉ: `http://localhost:5000` (hoặc port được cấu hình trong `launchSettings.json`).

## 📊 Tiến Độ Phát Triển (Sprint Summary)

### Sprint 1 — Nền tảng & Đặt lịch khám (Hoàn thành ✅ - 8 US / ~32 SP)
- Đăng ký, đăng nhập, đăng xuất và phân quyền theo vai trò (US04, US13, US35).
- Trang chủ bệnh nhân: tra cứu chuyên khoa, bác sĩ, dịch vụ (US01).
- Đặt lịch khám online, xem và hủy/đổi lịch hẹn (US05, US17).
- Cổng Bác sĩ: Dashboard, Hàng chờ, tiếp nhận khám bệnh (US21).
- Cổng Lễ tân: Duyệt và xác nhận lịch hẹn online (US19).
- Admin Dashboard & quản lý người dùng (US35, US39).

### Sprint 2 — Quy trình tại quầy & Khám lâm sàng (Hoàn thành ✅ - 14 US / 48 SP)
- Lễ tân tra cứu bệnh nhân (US16) và tạo lịch hẹn trực tiếp tại quầy (US15).
- Bác sĩ kê đơn thuốc điện tử (US24) và lập phiếu chỉ định xét nghiệm (US22).
- Lưu trữ và tra cứu hồ sơ bệnh án điện tử (US06).
- Admin quản lý danh mục dịch vụ khám (US37) và thiết bị y tế (US38).
- Hệ thống thông báo nội bộ.

### Sprint 3 — Cận lâm sàng & Kế toán viện phí (Hoàn thành ✅ - ~48 SP)
- **Phân hệ Kỹ thuật viên Cận lâm sàng (US28, US29, US30):** Tiếp nhận phiếu chỉ định, nhập số liệu xét nghiệm và tải lên hình ảnh kết quả (`/wwwroot/uploads/labresults/`).
- **Tích hợp kết quả khám (US23):** Bác sĩ xem trực tiếp kết quả xét nghiệm và ảnh chụp ngay trong ca khám.
- **Phân hệ Kế toán & Thu ngân (US31, US32):** Tự động tổng hợp chi phí (tiền khám + phí xét nghiệm + tiền thuốc), xác nhận thanh toán và xuất hóa đơn/phiếu thu.
- **Báo cáo & Quản trị (US33, US34):** Thống kê báo cáo doanh thu phòng khám theo ngày/tháng và quản lý bảng lương nhân viên.

## 👨‍💻 Nhóm Phát Triển (Team Development Structure)

| STT | Họ và Tên | GitHub Account | Vai trò (Role) | Ngày tham gia |
|---|---|---|---|---|
| 1 | **Nguyễn Hải Đăng** | [@HaiDang-Ng22](https://github.com/HaiDang-Ng22) | Dev / Team Lead | 16/09/2026 |
| 2 | **Lâm Gia Bảo** | [@vodanh1a2b-rgb](https://github.com/vodanh1a2b-rgb) | Dev / Product Owner | 16/09/2026 |
| 3 | **Nguyễn Hữu Danh** | [@nhdanh0412](https://github.com/nhdanh0412) | Dev / Scrum Master | 16/09/2026 |
| 4 | **Nguyễn Phước Sang** | [@PSang275](https://github.com/PSang275) | Dev | 23/09/2026 |

---

> 📌 Đây là đồ án môn học Thực hành Công nghệ Phần mềm Nâng cao, không dùng cho mục đích thương mại.
