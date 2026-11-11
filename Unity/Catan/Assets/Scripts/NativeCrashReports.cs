using System;
using System.IO;
using System.Linq;
using System.Text;

// Unity 客户端和随包的独立报告工具共用同一份转储解析逻辑。
internal static class NativeCrashReports
{
    public static void Collect(string crashes, string reports)
    {
        if (!Directory.Exists(crashes)) return;
        Directory.CreateDirectory(reports);
        foreach (string dir in Directory.GetDirectories(crashes, "Crash_*")
                     .OrderByDescending(Path.GetFileName).Take(10))
        {
            string output = Path.Combine(reports, Path.GetFileName(dir) + ".txt");
            if (File.Exists(output)) continue;
            try
            {
                string dump = Path.Combine(dir, "crash.dmp");
                var report = new StringBuilder("卡坦岛 Windows 崩溃报告\r\n");
                report.AppendLine("时间（本地） " + File.GetLastWriteTime(dump).ToString("yyyy-MM-dd HH:mm:ss"));
                using (var reader = new BinaryReader(File.OpenRead(dump))) ReadDump(reader, report);
                report.AppendLine("原始转储 " + dump);
                string log = Path.Combine(dir, "Player.log");
                if (File.Exists(log))
                {
                    report.AppendLine("Unity 日志 " + log);
                    AppendEngineLog(log, report);
                }
                string temp = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try { File.WriteAllText(temp, report.ToString(), Encoding.UTF8); File.Move(temp, output); }
                finally { File.Delete(temp); }
            }
            catch (IOException) { } // 转储仍在写入或另一进程已生成报告，下次启动重试。
            catch (UnauthorizedAccessException) { }
        }
        foreach (string path in Directory.GetFiles(reports, "Crash_*.txt").OrderByDescending(Path.GetFileName).Skip(10))
            File.Delete(path);
    }

    static void AppendEngineLog(string log, StringBuilder report)
    {
        string engineError = null;
        bool crashed = false;
        int frames = 0;
        foreach (string line in File.ReadLines(log))
        {
            if (!crashed && line.StartsWith("The file '") && line.Contains("' is corrupted!"))
            {
                int end = line.IndexOf("' is corrupted!", StringComparison.Ordinal);
                engineError = "Unity 报告资源文件损坏：" + Path.GetFileName(line.Substring(10, end - 10));
            }
            else if (!crashed && line == "[Position out of bounds!]" && engineError != null)
                engineError += "（读取位置越界）";
            else if (!crashed && line == "Size overflow in allocator.")
                engineError = "Unity 报告内存分配大小溢出";
            else if (line == "Crash!!!")
            {
                crashed = true;
                if (engineError != null) report.AppendLine(engineError);
            }
            else if (line.StartsWith("0x"))
            {
                report.AppendLine(line);
                if (++frames == 48) break;
            }
        }
    }

    static void ReadDump(BinaryReader reader, StringBuilder report)
    {
        if (reader.ReadUInt32() != 0x504d444d) throw new IOException("转储签名无效");
        reader.ReadUInt32();
        uint count = reader.ReadUInt32();
        long directory = reader.ReadUInt32();
        long exception = 0, modules = 0;
        for (int i = 0; i < Math.Min(count, 4096u); i++)
        {
            reader.BaseStream.Position = directory + 12L * i;
            uint type = reader.ReadUInt32();
            reader.ReadUInt32();
            uint offset = reader.ReadUInt32();
            if (type == 6) exception = offset;
            if (type == 4) modules = offset;
        }
        if (exception == 0) { report.AppendLine("转储未提供异常记录"); return; }
        reader.BaseStream.Position = exception + 8;
        uint code = reader.ReadUInt32();
        report.AppendLine("Windows 异常码 0x" + code.ToString("X8") +
            (code == 0x80000003 ? "（断点）" : code == 0xc0000005 ? "（内存访问冲突）" : code == 0xc00000fd ? "（栈溢出）" : ""));
        reader.BaseStream.Position = exception + 24;
        ulong address = reader.ReadUInt64();
        report.AppendLine("故障地址 0x" + address.ToString("X"));
        if (code == 0xc0000005 && reader.ReadUInt32() >= 2)
        {
            reader.BaseStream.Position = exception + 40;
            ulong operation = reader.ReadUInt64();
            report.AppendLine("内存操作 " + (operation == 0 ? "读" : operation == 1 ? "写" : operation == 8 ? "执行" : "未知") +
                " 0x" + reader.ReadUInt64().ToString("X"));
        }
        if (modules == 0) return;
        reader.BaseStream.Position = modules;
        uint total = reader.ReadUInt32();
        for (int i = 0; i < Math.Min(total, 4096u); i++)
        {
            long entry = modules + 4 + 108L * i;
            reader.BaseStream.Position = entry;
            ulong start = reader.ReadUInt64();
            uint size = reader.ReadUInt32();
            if (address < start || address - start >= size) continue;
            reader.BaseStream.Position = entry + 20;
            uint nameOffset = reader.ReadUInt32();
            reader.BaseStream.Position = nameOffset;
            uint length = reader.ReadUInt32();
            if (length > 65536) throw new IOException("模块名无效");
            string name = Encoding.Unicode.GetString(reader.ReadBytes((int)length));
            report.AppendLine("故障模块 " + Path.GetFileName(name) + "+0x" + (address - start).ToString("X"));
            break;
        }
    }
}
