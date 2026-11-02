using System.Collections.Generic;
using Healthy_System.Models; // Bổ sung namespace này

namespace Healthy_System.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        // --- DỮ LIỆU US35 (Quản lý người dùng & phân quyền) ---
        public List<User> Users { get; set; } = new List<User>();
        public int TotalUsers { get; set; }

        // --- DỮ LIỆU US39 (KPIs Analytics) ---
        public int TotalPatients { get; set; }
        public int TotalCompletedAppointments { get; set; }
        public double CancellationRate { get; set; }
        public decimal TotalRevenue { get; set; }

        // Biểu đồ
        public List<string> SpecialtyNames { get; set; } = new List<string>();
        public List<int> SpecialtyAppointmentCounts { get; set; } = new List<int>();

        public List<string> DoctorNames { get; set; } = new List<string>();
        public List<int> DoctorAppointmentCounts { get; set; } = new List<int>();

        // Cảnh báo
        public List<EquipmentWarningItem> MaintenanceEquipments { get; set; } = new List<EquipmentWarningItem>();
        public List<MedicineWarningItem> LowStockMedicines { get; set; } = new List<MedicineWarningItem>();
    }

    public class EquipmentWarningItem
    {
        public string Name { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class MedicineWarningItem
    {
        public string Name { get; set; } = string.Empty;
        public int QuantityLeft { get; set; }
        public string ExpiryDate { get; set; } = string.Empty;
    }
}