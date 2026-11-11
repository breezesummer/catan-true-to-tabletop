using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

internal static class CrashReportViewer
{
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            if (args.Length == 2) { NativeCrashReports.Collect(args[0], args[1]); return 0; }
            string data = Directory.GetDirectories(AppDomain.CurrentDomain.BaseDirectory, "Catan*_Data").Single();
            string product = Path.GetFileName(data);
            product = product.Substring(0, product.Length - "_Data".Length);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string reports = Path.Combine(Directory.GetParent(local).FullName, "LocalLow", "CatanDevelopment",
                product.Replace('.', '_'), "CrashReports");
            Directory.CreateDirectory(reports);
            NativeCrashReports.Collect(Path.Combine(Path.GetTempPath(), "CatanDevelopment", product, "Crashes"), reports);
            Process.Start("explorer.exe", "\"" + reports + "\"");
            return 0;
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "无法读取崩溃报告");
            return 1;
        }
    }
}
