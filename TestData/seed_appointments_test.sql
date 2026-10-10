-- ==============================================================================
-- Test Data Seed Script: Appointments (Healthy System / MedEase)
-- Purpose: Lab 3 - Configuration Management (Dump/Test Data)
-- ==============================================================================

-- 1. Valid Test Data (3 bản ghi hợp lệ)
INSERT INTO "Appointments" 
("AppointmentCode", "PatientId", "DoctorId", "SpecialtyId", "AppointmentDate", "TimeSlot", "ReasonForVisit", "Status", "CreatedAt")
VALUES 
('APT-20261010-001', 3, 1, 1, '2026-10-15 00:00:00+00', '08:30 - 09:30', 'Tái khám định kỳ tăng huyết áp và theo dõi chỉ số tim mạch', 'Pending', NOW()),
('APT-20261010-002', 4, 2, 2, '2026-10-17 00:00:00+00', '14:00 - 15:00', 'Đau nửa đầu kèm chóng mặt kéo dài 3 ngày gần đây', 'Pending', NOW()),
('APT-20261010-003', 5, 3, 3, '2026-10-20 00:00:00+00', '09:30 - 10:30', 'Khám sức khỏe tổng quát định kỳ và xét nghiệm máu', 'Pending', NOW());

-- 2. Invalid Test Cases (Ghi chú kịch bản kiểm thử biên / không hợp lệ)
-- TC_APPT_004 (Invalid): ReasonForVisit IS NULL / EMPTY -> Vi phạm NOT NULL / Required
-- INSERT INTO "Appointments" ("AppointmentCode", "PatientId", "DoctorId", "SpecialtyId", "AppointmentDate", "TimeSlot", "ReasonForVisit", "Status", "CreatedAt")
-- VALUES ('APT-TEST-ERR1', 3, 1, 1, '2026-10-15', '08:30 - 09:30', '', 'Pending', NOW()); -- Expected: Validation Error

-- TC_APPT_005 (Boundary/Invalid): AppointmentDate trong quá khứ -> Vi phạm Business Rule
-- INSERT INTO "Appointments" ("AppointmentCode", "PatientId", "DoctorId", "SpecialtyId", "AppointmentDate", "TimeSlot", "ReasonForVisit", "Status", "CreatedAt")
-- VALUES ('APT-TEST-ERR2', 3, 1, 1, '2026-01-01', '08:30 - 09:30', 'Khám răng', 'Pending', NOW()); -- Expected: Business Validation Error

-- TC_APPT_006 (Boundary/Invalid): ReasonForVisit > 500 ký tự -> Vi phạm StringLength(500)
-- Expected: DB Exception string data right truncation / ModelState error
