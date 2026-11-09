using System.Reflection;
using System.Runtime.InteropServices;

// Shown in Properties > Details, in the SmartScreen / UAC dialogs and used by antivirus reputation.
// Keep Company and Product the same in every release; bump the version for each one (release.ps1 reads it).
[assembly: AssemblyTitle("Phấn Trắng")]
[assembly: AssemblyDescription("Phấn Trắng – bộ công cụ giảng dạy: chỉ, viết, vẽ, chấm bài trên màn hình đang chiếu")]
[assembly: AssemblyCompany("Viên Thế Hưng")]
[assembly: AssemblyProduct("Phấn Trắng")]
[assembly: AssemblyCopyright("© 2026 Viên Thế Hưng")]
[assembly: AssemblyVersion("1.3.1.0")]
[assembly: AssemblyFileVersion("1.3.1.0")]
[assembly: AssemblyInformationalVersion("1.3.1")]
[assembly: ComVisible(false)]
