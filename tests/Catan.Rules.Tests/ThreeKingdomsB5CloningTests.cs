using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Catan.Core.ThreeKingdoms;
using Xunit;
using Xunit.Abstractions;

namespace Catan.Rules.Tests;

/// <summary>逐一填充序列化字段，防止新增状态字段遗漏复制或共享可变对象。</summary>
public sealed class ThreeKingdomsB5CloningTests(ITestOutputHelper output)
{
    private static string Serialize(object value)
    {
        using var stream = new MemoryStream();
        new DataContractJsonSerializer(value.GetType()).WriteObject(stream, value);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static object Populate(Type type, string path)
    {
        if (type == typeof(string)) return path;
        if (type == typeof(int)) return path.Length + 7;
        if (type == typeof(long)) return (long)path.Length + 11;
        if (type == typeof(bool)) return true;
        if (type.IsArray)
        {
            var array = Array.CreateInstance(type.GetElementType()!, 2);
            for (int i = 0; i < array.Length; i++) array.SetValue(Populate(type.GetElementType()!, path + i), i);
            return array;
        }
        var value = Activator.CreateInstance(type)!;
        foreach (var p in type.GetProperties().Where(p => p.GetCustomAttribute<DataMemberAttribute>() != null))
            p.SetValue(value, Populate(p.PropertyType, path + "/" + p.Name));
        return value;
    }

    private static void AssertIndependent(object? original, object? copy)
    {
        if (original == null) { Assert.Null(copy); return; }
        Assert.NotNull(copy);
        var type = original.GetType();
        if (type.IsValueType || type == typeof(string)) { Assert.Equal(original, copy); return; }
        Assert.NotSame(original, copy);
        if (original is Array array)
        {
            var copied = (Array)copy!;
            Assert.Equal(array.Length, copied.Length);
            for (int i = 0; i < array.Length; i++) AssertIndependent(array.GetValue(i), copied.GetValue(i));
            return;
        }
        foreach (var p in type.GetProperties().Where(p => p.GetCustomAttribute<DataMemberAttribute>() != null))
            AssertIndependent(p.GetValue(original), p.GetValue(copy));
    }

    [Fact]
    public void EveryB5SerializedMemberIsCopiedAndEveryMutableDescendantIsIndependent()
    {
        var state = ThreeKingdomsEngine.Create(new[] { "P1", "P2", "P3", "P4" }, 42, _ => 0, true, GeneralCatalog.B5PackageVersion);
        state.Board = (B5BoardState)Populate(typeof(B5BoardState), "board");
        state.Military = (B5MilitaryState)Populate(typeof(B5MilitaryState), "military");
        state.CivilB5 = (B5CivilState)Populate(typeof(B5CivilState), "civil");
        state.Skills.Frames = new[] { (B2SkillFrame)Populate(typeof(B2SkillFrame), "skill-frame") };
        string original = Serialize(state);
        var copy = ThreeKingdomsEngine.Clone(state);
        Assert.Equal(original, Serialize(copy));
        AssertIndependent(state.Board, copy.Board);
        AssertIndependent(state.Military, copy.Military);
        AssertIndependent(state.CivilB5, copy.CivilB5);
        AssertIndependent(state.Skills.Frames, copy.Skills.Frames);
        copy.Board.Suppressed[0].Levels[0]++;
        copy.Board.DeferredOperations[0].Cost[0]++;
        copy.Military.ComparisonSource.CivilB5.Selection[0]++;
        copy.Military.SuspendedFrames[0].Tiles[0].AllowedCardTypes[0]++;
        copy.Skills.Frames[0].CivilB5.VirtualUse.TargetPlayerIds[0] = "独立目标";
        copy.CivilB5.Relations[0].PlayerId = "独立关系";
        Assert.Equal(original, Serialize(state));
    }

    [Fact]
    public void LegacyNullB5StatesAndDefaultOptionalPayloadsRemainUnchanged()
    {
        var legacy = ThreeKingdomsEngine.Create(new[] { "P1", "P2", "P3", "P4" }, 42, _ => 0, true, GeneralCatalog.B4PackageVersion);
        Assert.Equal(Serialize(legacy), Serialize(ThreeKingdomsEngine.Clone(legacy)));
        var state = ThreeKingdomsEngine.Create(new[] { "P1", "P2", "P3", "P4" }, 42, _ => 0, true, GeneralCatalog.B5PackageVersion);
        state.Skills.Frames = new[] { new B2SkillFrame { CivilB5 = new B5CivilPayload() } };
        Assert.Equal(Serialize(state), Serialize(ThreeKingdomsEngine.Clone(state)));
    }

    [Fact]
    public void NormalPirateOpeningRetainsDeterministicStateAcrossFortyCommands()
    {
        var game = ThreeKingdomsTestGame.Create("combined", "pirate-islands", 4, skills: true, packageVersion: GeneralCatalog.B5PackageVersion);
        var checkpoints = new HashSet<string>();
        var watch = Stopwatch.StartNew();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int steps = 0; steps < 40; steps++)
        {
            var tk = game.Authority();
            dynamic command = game.Decide(); command.Id = "B5整局-" + steps;
            var decision = tk.Skills.PendingDecision ?? tk.Dice?.PendingDecision ?? tk.Automation?.PendingDecision;
            var checkpoint = decision != null ? "技能:" + decision.Kind + ":" + decision.Title : tk.PendingDecision != null ? "武将:" + tk.PendingDecision.Kind : null;
            if (checkpoint != null && checkpoints.Add(checkpoint)) game.AcceptAndResume(command); else game.Accept(command);
            Assert.Equal(Serialize(game.Authority()), Serialize(ThreeKingdomsEngine.Clone(game.Authority())));
        }
        output.WriteLine($"海盗群岛四人正常局前40命令：{watch.Elapsed.TotalMilliseconds:F0}毫秒，当前线程分配{(GC.GetAllocatedBytesForCurrentThread()-allocated)/1024d/1024d:F1}MiB。");
        Assert.Equal(game.Save(), ThreeKingdomsTestGame.Load("combined", game.Save()).Save());
    }
}
