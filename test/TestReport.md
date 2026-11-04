# Báo Cáo Kiểm Thử (Test Report) - MedEase
**Sprint 1, 2 & 3 Integration & Regression Testing**

| Test ID / Loại | Bước thực hiện / Input | Expected Result | Actual Result | Pass/Fail |
| :--- | :--- | :--- | :--- | :--- |
| **IT-01**<br>*(Integration)* | Đặt lịch khám online (US05/#6) → Lễ tân mở Dashboard duyệt xác nhận ca hẹn (US19/#12) | Lịch hẹn chuyển Confirmed, tự động xuất hiện tại hàng chờ Bác sĩ (US21/#22) | Lịch hiển thị đúng trong hàng chờ khám Bác sĩ | ✅ Pass |
| **IT-02**<br>*(Integration)* | Lễ tân tìm bệnh nhân (US16/#18) → Đặt lịch tại quầy (US15/#17) cho bệnh nhân cũ | Hệ thống tự động nhận diện và điền thông tin bệnh nhân, tạo lịch hẹn thành công | Thông tin điền chuẩn xác, tạo phiếu hẹn thành công | ✅ Pass |
| **IT-03**<br>*(Integration)* | Bác sĩ khám ca hẹn (US21/#22) → Kê đơn thuốc (US24/#24) & Lập phiếu xét nghiệm (US22/#23) | Ca khám chuyển Completed, lưu đơn thuốc và phiếu chỉ định vào hồ sơ bệnh án (US06/#27) | Đơn thuốc và phiếu xét nghiệm lưu đúng CSDL | ✅ Pass |
| **RT-01**<br>*(Regression)* | Kiểm tra ràng buộc khung giờ đặt lịch (commit `2f9e964`): Thử chọn ngày quá khứ hoặc trùng giờ | Báo lỗi validation, chặn gửi form, không tạo dữ liệu rác trong CSDL | Báo lỗi đúng validation, chặn submit thành công | ✅ Pass |
| **RT-02**<br>*(Regression)* | Kiểm tra phân quyền tài khoản (US35/#13) và đăng xuất (US13/#10): Đăng nhập bằng tài khoản Bệnh nhân và cố truy cập vào trang `/Admin` | Chặn truy cập trái quyền, tự động chuyển hướng về `/Account/AccessDenied` (HTTP 403) | Chặn truy cập chính xác, chuyển hướng an toàn | ✅ Pass |
