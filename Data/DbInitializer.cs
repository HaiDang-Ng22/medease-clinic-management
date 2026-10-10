using System;
using System.Collections.Generic;
using System.Linq;
using BCrypt.Net;
using Healthy_System.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Healthy_System.Data
{
    public static class DbInitializer
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

            try
            {
                if (context.Database.IsRelational())
                {
                    context.Database.Migrate();
                }
                else
                {
                    context.Database.EnsureCreated();
                }
                SeedData(context);
                logger.LogInformation("Cơ sở dữ liệu Healthy System đã được khởi tạo và nạp dữ liệu mẫu thành công.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi khởi tạo cơ sở dữ liệu");
                throw;
            }
        }

        public static void EnsureDatabaseSchemaCreated(AppDbContext context)
        {
            context.Database.EnsureCreated();

            // Thêm các cột mới vào bảng Appointments nếu chưa có (SQLite không hỗ trợ IF NOT EXISTS nên dùng try-catch)
            var newColumns = new[]
            {
                ("Diagnosis",    "TEXT"),
                ("Prescription", "TEXT"),
                ("DoctorNotes",  "TEXT"),
                ("LabRequest",   "TEXT"),
            };

            foreach (var (colName, colType) in newColumns)
            {
                try
                {
                    context.Database.ExecuteSqlRaw($"ALTER TABLE \"Appointments\" ADD COLUMN \"{colName}\" {colType}");
                }
                catch
                {
                    // Cột đã tồn tại → bỏ qua lỗi
                }
            }
        }

        public static void SeedData(AppDbContext context)
        {
            // Seed Specialties
            if (!context.Specialties.Any())
            {
                var specialties = new List<Specialty>
                {
                    new Specialty
                    {
                        Name = "Tim mạch",
                        Code = "CARDIO",
                        Description = "Chẩn đoán và điều trị toàn diện các bệnh lý tim mạch, cao huyết áp, thiếu máu cơ tim, rối loạn nhịp tim.",
                        Icon = "fa-heart-pulse",
                        ImageUrl = "https://images.unsplash.com/photo-1628348068343-c6a848d2b6dd?w=600&auto=format&fit=crop&q=80",
                        IsActive = true
                    },
                    new Specialty
                    {
                        Name = "Tai Mũi Họng",
                        Code = "ENT",
                        Description = "Khám và điều trị các bệnh lý tai mũi họng với hệ thống nội soi Olympus hiện đại, không đau.",
                        Icon = "fa-head-side-cough",
                        ImageUrl = "https://images.unsplash.com/photo-1579684385127-1ef15d508118?w=600&auto=format&fit=crop&q=80",
                        IsActive = true
                    },
                    new Specialty
                    {
                        Name = "Nhi khoa",
                        Code = "PEDIATRIC",
                        Description = "Chăm sóc sức khỏe toàn diện cho trẻ sơ sinh và trẻ nhỏ, tư vấn dinh dưỡng và tiêm chủng.",
                        Icon = "fa-baby",
                        ImageUrl = "https://images.unsplash.com/photo-1532938911079-1b06ac7ceec7?w=600&auto=format&fit=crop&q=80",
                        IsActive = true
                    },
                    new Specialty
                    {
                        Name = "Nội tổng quát",
                        Code = "INTERNAL",
                        Description = "Khám tổng quát định kỳ, tầm soát bệnh lý mạn tính như tiểu đường, gan mật, tiêu hóa, hô hấp.",
                        Icon = "fa-user-doctor",
                        ImageUrl = "https://images.unsplash.com/photo-1505751172876-fa1923c5c528?w=600&auto=format&fit=crop&q=80",
                        IsActive = true
                    },
                    new Specialty
                    {
                        Name = "Răng Hàm Mặt",
                        Code = "DENTAL",
                        Description = "Nha khoa thẩm mỹ, nhổ răng không đau, cấy ghép implant và điều trị tủy công nghệ cao.",
                        Icon = "fa-tooth",
                        ImageUrl = "https://images.unsplash.com/photo-1588776814546-1ffcf47267a5?w=600&auto=format&fit=crop&q=80",
                        IsActive = true
                    },
                    new Specialty
                    {
                        Name = "Da liễu",
                        Code = "DERMA",
                        Description = "Chẩn đoán và điều trị bệnh lý da liễu, mụn, nám, tàn nhang và chăm sóc phục hồi da chuyên sâu.",
                        Icon = "fa-hand-dots",
                        ImageUrl = "https://images.unsplash.com/photo-1616394584738-fc6e612e71b9?w=600&auto=format&fit=crop&q=80",
                        IsActive = true
                    },
                    new Specialty
                    {
                        Name = "Chấn thương chỉnh hình",
                        Code = "ORTHO",
                        Description = "Điều trị các chấn thương xương khớp, thoái hóa cột sống, thoát vị đĩa đệm và phục hồi chức năng.",
                        Icon = "fa-bone",
                        ImageUrl = "https://images.unsplash.com/photo-1516549655169-df83a0774514?w=600&auto=format&fit=crop&q=80",
                        IsActive = true
                    },
                    new Specialty
                    {
                        Name = "Nhãn khoa (Mắt)",
                        Code = "EYE",
                        Description = "Khám khúc xạ, điều trị viêm giác mạc, đục thủy tinh thể và tầm soát bệnh lý đáy mắt.",
                        Icon = "fa-eye",
                        ImageUrl = "https://images.unsplash.com/photo-1576091160550-2173dba999ef?w=600&auto=format&fit=crop&q=80",
                        IsActive = true
                    }
                };

                context.Specialties.AddRange(specialties);
                context.SaveChanges();
            }

            // Seed Users & Doctors
            if (!context.Users.Any())
            {
                string hashedPass = BCrypt.Net.BCrypt.HashPassword("123456");

                var admin = new User
                {
                    FullName = "Quản Trị Viên Hệ Thống",
                    Email = "admin@healthysystem.vn",
                    PasswordHash = hashedPass,
                    PhoneNumber = "0901234567",
                    Role = "Admin",
                    Gender = "Nam",
                    Address = "Hồ Chí Minh"
                };

                var receptionist = new User
                {
                    FullName = "Nguyễn Thu Thảo (Lễ Tân)",
                    Email = "reception@healthysystem.vn",
                    PasswordHash = hashedPass,
                    PhoneNumber = "0902345678",
                    Role = "Receptionist",
                    Gender = "Nữ",
                    Address = "Quận 10, TP.HCM"
                };

                var patient = new User
                {
                    FullName = "Nguyễn Hải Đăng (Bệnh Nhân)",
                    Email = "benhnhan@gmail.com",
                    PasswordHash = hashedPass,
                    PhoneNumber = "0799192226",
                    Role = "Patient",
                    Gender = "Nam",
                    DateOfBirth = DateTime.SpecifyKind(new DateTime(2003, 5, 15), DateTimeKind.Utc),
                    Address = "Quận 3, TP.HCM"
                };

                context.Users.AddRange(admin, receptionist, patient);
                context.SaveChanges();

                // Add Doctor Users
                var cardio = context.Specialties.First(s => s.Code == "CARDIO");
                var ent = context.Specialties.First(s => s.Code == "ENT");
                var pediatric = context.Specialties.First(s => s.Code == "PEDIATRIC");
                var internalMed = context.Specialties.First(s => s.Code == "INTERNAL");
                var dental = context.Specialties.First(s => s.Code == "DENTAL");
                var derma = context.Specialties.First(s => s.Code == "DERMA");

                var docUsers = new List<(User user, Doctor doc)>
                {
                    (
                        new User
                        {
                            FullName = "PGS. TS. BS. Nguyễn Minh Anh",
                            Email = "bs.minhanh@healthysystem.vn",
                            PasswordHash = hashedPass,
                            PhoneNumber = "0911223344",
                            Role = "Doctor",
                            Gender = "Nam",
                            Address = "Quận 1, TP.HCM",
                            AvatarUrl = "https://images.unsplash.com/photo-1622253692010-333f2da6031d?w=400&auto=format&fit=crop&q=80"
                        },
                        new Doctor
                        {
                            SpecialtyId = cardio.Id,
                            Title = "Phó Giáo Sư - Tiến Sĩ Bác Sĩ",
                            Bio = "Nguyên Trưởng khoa Tim mạch BV Chợ Rẫy, hơn 22 năm kinh nghiệm trong chẩn đoán và điều trị bệnh lý mạch vành, suy tim và can thiệp tim mạch.",
                            ExperienceYears = 22,
                            RoomNumber = "Phòng 201 - Tầng 2",
                            ConsultationFee = 350000,
                            Rating = 4.9,
                            ReviewCount = 142
                        }
                    ),
                    (
                        new User
                        {
                            FullName = "ThS. BS. Trần Quang Huy",
                            Email = "bs.quanghuy@healthysystem.vn",
                            PasswordHash = hashedPass,
                            PhoneNumber = "0911223355",
                            Role = "Doctor",
                            Gender = "Nam",
                            Address = "Quận 5, TP.HCM",
                            AvatarUrl = "https://images.unsplash.com/photo-1537368910025-700350fe46c7?w=400&auto=format&fit=crop&q=80"
                        },
                        new Doctor
                        {
                            SpecialtyId = ent.Id,
                            Title = "Thạc sĩ Bác sĩ Chuyên khoa II",
                            Bio = "Chuyên gia đầu ngành về nội soi Tai Mũi Họng ống mềm, điều trị triệt để viêm xoang mạn tính, viêm amidan và các bệnh thanh quản.",
                            ExperienceYears = 15,
                            RoomNumber = "Phòng 102 - Tầng 1",
                            ConsultationFee = 250000,
                            Rating = 4.8,
                            ReviewCount = 98
                        }
                    ),
                    (
                        new User
                        {
                            FullName = "BS. CKI. Lê Thu Trang",
                            Email = "bs.thutrang@healthysystem.vn",
                            PasswordHash = hashedPass,
                            PhoneNumber = "0911223366",
                            Role = "Doctor",
                            Gender = "Nữ",
                            Address = "Quận Phú Nhuận, TP.HCM",
                            AvatarUrl = "https://images.unsplash.com/photo-1594824813576-46487e954546?w=400&auto=format&fit=crop&q=80"
                        },
                        new Doctor
                        {
                            SpecialtyId = pediatric.Id,
                            Title = "Bác sĩ Chuyên khoa I Nhi",
                            Bio = "Từng công tác tại Bệnh viện Nhi Đồng 1, nhiệt tình, tâm lý, chuyên chẩn đoán các bệnh lý nhiễm trùng, dinh dưỡng và hô hấp ở trẻ em.",
                            ExperienceYears = 12,
                            RoomNumber = "Phòng 105 - Tầng 1",
                            ConsultationFee = 200000,
                            Rating = 5.0,
                            ReviewCount = 210
                        }
                    ),
                    (
                        new User
                        {
                            FullName = "TS. BS. Vũ Hoàng Nam",
                            Email = "bs.hoangnam@healthysystem.vn",
                            PasswordHash = hashedPass,
                            PhoneNumber = "0911223377",
                            Role = "Doctor",
                            Gender = "Nam",
                            Address = "Quận Bình Thạnh, TP.HCM",
                            AvatarUrl = "https://images.unsplash.com/photo-1612349317150-e413f6a5b16d?w=400&auto=format&fit=crop&q=80"
                        },
                        new Doctor
                        {
                            SpecialtyId = internalMed.Id,
                            Title = "Tiến Sĩ Bác Sĩ Nội Khoa",
                            Bio = "Chuyên gia Nội tổng quát và Nội tiết - Chuyển hóa, giàu kinh nghiệm trong kiểm soát tiểu đường, tăng huyết áp, mỡ máu và bệnh lý gan mật.",
                            ExperienceYears = 18,
                            RoomNumber = "Phòng 205 - Tầng 2",
                            ConsultationFee = 250000,
                            Rating = 4.9,
                            ReviewCount = 135
                        }
                    ),
                    (
                        new User
                        {
                            FullName = "BS. CKI. Phạm Thanh Hà",
                            Email = "bs.thanhha@healthysystem.vn",
                            PasswordHash = hashedPass,
                            PhoneNumber = "0911223388",
                            Role = "Doctor",
                            Gender = "Nữ",
                            Address = "Quận 3, TP.HCM",
                            AvatarUrl = "https://images.unsplash.com/photo-1559839734-2b71ea197ec2?w=400&auto=format&fit=crop&q=80"
                        },
                        new Doctor
                        {
                            SpecialtyId = dental.Id,
                            Title = "Bác sĩ Chuyên khoa Răng Hàm Mặt",
                            Bio = "Tốt nghiệp ĐH Y Dược TP.HCM, tu nghiệp chỉnh nha tại Hàn Quốc, khéo léo và tận tâm với các ca răng thẩm mỹ và phục hình.",
                            ExperienceYears = 10,
                            RoomNumber = "Phòng 301 - Tầng 3",
                            ConsultationFee = 250000,
                            Rating = 4.9,
                            ReviewCount = 88
                        }
                    ),
                    (
                        new User
                        {
                            FullName = "ThS. BS. Đỗ Minh Trí",
                            Email = "bs.minhtri@healthysystem.vn",
                            PasswordHash = hashedPass,
                            PhoneNumber = "0911223399",
                            Role = "Doctor",
                            Gender = "Nam",
                            Address = "Quận 7, TP.HCM",
                            AvatarUrl = "https://images.unsplash.com/photo-1582750433449-648ed127bb54?w=400&auto=format&fit=crop&q=80"
                        },
                        new Doctor
                        {
                            SpecialtyId = derma.Id,
                            Title = "Thạc Sĩ Bác Sĩ Da Liễu",
                            Bio = "Chuyên khoa Da liễu & Thẩm mỹ da, ứng dụng công nghệ Laser thế hệ mới điều trị mụn, sắc tố da và trẻ hóa da.",
                            ExperienceYears = 11,
                            RoomNumber = "Phòng 303 - Tầng 3",
                            ConsultationFee = 250000,
                            Rating = 4.8,
                            ReviewCount = 76
                        }
                    )
                };

                foreach (var item in docUsers)
                {
                    context.Users.Add(item.user);
                    context.SaveChanges();

                    item.doc.UserId = item.user.Id;
                    context.Doctors.Add(item.doc);
                    context.SaveChanges();
                }
            }

            // Seed Services
            if (!context.Services.Any())
            {
                var cardio = context.Specialties.FirstOrDefault(s => s.Code == "CARDIO");
                var ent = context.Specialties.FirstOrDefault(s => s.Code == "ENT");
                var pediatric = context.Specialties.FirstOrDefault(s => s.Code == "PEDIATRIC");
                var internalMed = context.Specialties.FirstOrDefault(s => s.Code == "INTERNAL");

                var services = new List<Service>
                {
                    new Service { Name = "Khám Tim mạch Chuyên sâu", Category = "Khám bệnh", Price = 350000, SpecialtyId = cardio?.Id, Description = "Khám lâm sàng, nghe tim phổi, đo huyết áp, đánh giá nguy cơ biến cố tim mạch.", EstimatedDurationMinutes = 30 },
                    new Service { Name = "Siêu âm Tim Doppler màu 4D", Category = "Chẩn đoán hình ảnh", Price = 450000, SpecialtyId = cardio?.Id, Description = "Khảo sát cấu trúc buồng tim, chức năng van tim và dòng máu chảy qua tim.", EstimatedDurationMinutes = 20 },
                    new Service { Name = "Đo điện tâm đồ ECG 12 cần", Category = "Thủ thuật", Price = 120000, SpecialtyId = cardio?.Id, Description = "Ghi lại hoạt động điện học của tim, phát hiện rối loạn nhịp tim và thiếu máu cơ tim.", EstimatedDurationMinutes = 15 },
                    new Service { Name = "Khám Tai Mũi Họng & Nội soi", Category = "Khám bệnh", Price = 250000, SpecialtyId = ent?.Id, Description = "Nội soi ống mềm không đau, tầm soát vòm họng, dây thanh và hốc mũi.", EstimatedDurationMinutes = 25 },
                    new Service { Name = "Khám Nhi khoa Tổng quát", Category = "Khám bệnh", Price = 200000, SpecialtyId = pediatric?.Id, Description = "Đánh giá tăng trưởng chiều cao, cân nặng, thể chất và tư vấn tiêm chủng.", EstimatedDurationMinutes = 30 },
                    new Service { Name = "Khám Nội Tổng Quát Định Kỳ", Category = "Khám bệnh", Price = 200000, SpecialtyId = internalMed?.Id, Description = "Tầm soát sức khỏe tổng thể, tư vấn chế độ sinh hoạt và phòng ngừa bệnh mạn tính.", EstimatedDurationMinutes = 30 },
                    new Service { Name = "Xét nghiệm Công thức máu (CBC 24 thông số)", Category = "Xét nghiệm", Price = 150000, Description = "Phát hiện thiếu máu, nhiễm trùng và các bệnh lý về máu cơ bản.", EstimatedDurationMinutes = 15 },
                    new Service { Name = "Xét nghiệm Sinh hóa Gan - Thận - Đường huyết", Category = "Xét nghiệm", Price = 280000, Description = "Đánh giá chức năng gan (AST, ALT, GGT), chức năng thận (Ure, Creatinin) và Glucose.", EstimatedDurationMinutes = 15 },
                    new Service { Name = "Chụp X-Quang Ngực Thẳng Kỹ thuật số", Category = "Chẩn đoán hình ảnh", Price = 180000, Description = "Khảo sát phổi, bóng tim và xương lồng ngực với lượng bức xạ cực thấp an toàn.", EstimatedDurationMinutes = 10 },
                    new Service { Name = "Siêu âm Bụng Tổng Quát màu", Category = "Chẩn đoán hình ảnh", Price = 200000, Description = "Khảo sát gan, mật, tụy, lách, thận, bàng quang và tiền liệt tuyến.", EstimatedDurationMinutes = 20 }
                };

                context.Services.AddRange(services);
                context.SaveChanges();
            }

            // Seed Equipment
            if (!context.Equipments.Any())
            {
                var cardio = context.Specialties.FirstOrDefault(s => s.Code == "CARDIO");
                var ent = context.Specialties.FirstOrDefault(s => s.Code == "ENT");

                var equipments = new List<Equipment>
                {
                    new Equipment
                    {
                        Name = "Máy Cắt Lớp Vi Tính CT Scanner 128 Dãy",
                        Origin = "Siemens - CHLB Đức",
                        SpecialtyId = cardio?.Id,
                        Description = "Hệ thống chụp cắt lớp đa lát cắt tốc độ cao, dựng hình 3D mạch vành và nội tạng với độ phân giải siêu nét.",
                        ImageUrl = "https://images.unsplash.com/photo-1516549655169-df83a0774514?w=500&auto=format&fit=crop&q=80",
                        Status = "Đang vận hành chuẩn xác"
                    },
                    new Equipment
                    {
                        Name = "Máy Siêu Âm Màu 4D GE Voluson E10",
                        Origin = "GE Healthcare - Hoa Kỳ",
                        SpecialtyId = cardio?.Id,
                        Description = "Công nghệ siêu âm tim và mạch máu chuyên sâu với đầu dò điện tử ma trận cho hình ảnh trung thực và sắc nét.",
                        ImageUrl = "https://images.unsplash.com/photo-1579684385127-1ef15d508118?w=500&auto=format&fit=crop&q=80",
                        Status = "Đang vận hành chuẩn xác"
                    },
                    new Equipment
                    {
                        Name = "Hệ Thống Nội Soi Ống Mềm Olympus EVIS EXERA III",
                        Origin = "Olympus - Nhật Bản",
                        SpecialtyId = ent?.Id,
                        Description = "Nội soi phóng đại nhuộm màu NBI phát hiện sớm tổn thương tiền ung thư vòm họng và thực quản.",
                        ImageUrl = "https://images.unsplash.com/photo-1582750433449-648ed127bb54?w=500&auto=format&fit=crop&q=80",
                        Status = "Đang vận hành chuẩn xác"
                    },
                    new Equipment
                    {
                        Name = "Máy Đo Điện Tim 12 Kênh Kỹ Thuật Số cardiofax",
                        Origin = "Nihon Kohden - Nhật Bản",
                        SpecialtyId = cardio?.Id,
                        Description = "Tự động phân tích và đưa ra cảnh báo sớm rối loạn nhịp tim với độ chính xác cao.",
                        ImageUrl = "https://images.unsplash.com/photo-1505751172876-fa1923c5c528?w=500&auto=format&fit=crop&q=80",
                        Status = "Đang vận hành chuẩn xác"
                    },
                    new Equipment
                    {
                        Name = "Máy Xét Nghiệm Huyết Học Tự Động Sysmex XN-550",
                        Origin = "Sysmex - Nhật Bản",
                        Description = "Phân tích 24 thông số tế bào máu tự động hoàn toàn chỉ trong 60 giây.",
                        ImageUrl = "https://images.unsplash.com/photo-1532938911079-1b06ac7ceec7?w=500&auto=format&fit=crop&q=80",
                        Status = "Đang vận hành chuẩn xác"
                    }
                };

                context.Equipments.AddRange(equipments);
                context.SaveChanges();
            }

            // Seed Doctor Schedules (Next 14 days)
            if (!context.DoctorSchedules.Any())
            {
                var doctors = context.Doctors.ToList();
                var timeSlots = new List<string>
                {
                    "08:00 - 08:30", "08:30 - 09:00",
                    "09:00 - 09:30", "09:30 - 10:00",
                    "10:00 - 10:30", "10:30 - 11:00",
                    "13:30 - 14:00", "14:00 - 14:30",
                    "14:30 - 15:00", "15:00 - 15:30",
                    "15:30 - 16:00", "16:00 - 16:30"
                };

                var schedules = new List<DoctorSchedule>();
                var today = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);

                foreach (var doc in doctors)
                {
                    for (int i = 0; i <= 14; i++)
                    {
                        var date = today.AddDays(i);
                        // Skip Sundays for clinic rest
                        if (date.DayOfWeek == DayOfWeek.Sunday) continue;

                        foreach (var slot in timeSlots)
                        {
                            schedules.Add(new DoctorSchedule
                            {
                                DoctorId = doc.Id,
                                WorkDate = date,
                                TimeSlot = slot,
                                MaxPatients = 1,
                                BookedPatients = 0,
                                IsAvailable = true
                            });
                        }
                    }
                }

                context.DoctorSchedules.AddRange(schedules);
                context.SaveChanges();
            }

            // Seed Sample Appointment for Test Patient
            if (!context.Appointments.Any())
            {
                var patient = context.Users.FirstOrDefault(u => u.Email == "benhnhan@gmail.com");
                var doctor = context.Doctors.Include(d => d.Specialty).FirstOrDefault();

                if (patient != null && doctor != null)
                {
                    var targetDate = DateTime.SpecifyKind(DateTime.Today.AddDays(2), DateTimeKind.Utc);
                    var schedule = context.DoctorSchedules.FirstOrDefault(s => s.DoctorId == doctor.Id && s.WorkDate == targetDate && s.TimeSlot == "09:00 - 09:30");
                    if (schedule != null)
                    {
                        schedule.BookedPatients = 1;
                        schedule.IsAvailable = false;
                    }

                    var appt = new Appointment
                    {
                        AppointmentCode = $"HS-{DateTime.Now:yyMM}-1001",
                        PatientId = patient.Id,
                        DoctorId = doctor.Id,
                        SpecialtyId = doctor.SpecialtyId,
                        AppointmentDate = targetDate,
                        TimeSlot = "09:00 - 09:30",
                        ReasonForVisit = "Đau tức ngực nhẹ khi vận động nhiều, khó thở nhẹ vào ban đêm.",
                        Status = "Confirmed",
                        CreatedAt = DateTime.UtcNow.AddDays(-1)
                    };

                    context.Appointments.Add(appt);

                    // Add Notification
                    context.Notifications.Add(new Notification
                    {
                        UserId = patient.Id,
                        Title = "Nhắc nhở lịch khám sắp tới",
                        Message = $"Bạn có lịch hẹn {appt.AppointmentCode} vào lúc 09:00 ngày {targetDate:dd/MM/yyyy} với {doctor.Title}. Vui lòng đến trước 15 phút.",
                        Type = "Reminder",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    });

                    context.Notifications.Add(new Notification
                    {
                        UserId = patient.Id,
                        Title = "Chào mừng bạn đến với Healthy System",
                        Message = "Chúc mừng bạn đã tạo tài khoản thành công tại Hệ Thống Phòng Khám Healthy System. Bạn có thể chủ động đặt lịch và theo dõi sức khỏe tại đây.",
                        Type = "General",
                        IsRead = true,
                        CreatedAt = DateTime.UtcNow.AddDays(-1)
                    });

                    context.SaveChanges();
                }
            }
        }
    }
}
