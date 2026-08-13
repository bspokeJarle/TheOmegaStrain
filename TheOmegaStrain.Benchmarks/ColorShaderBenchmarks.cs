using BenchmarkDotNet.Attributes;
using Domain;
using Microsoft.VSDiagnostics;

namespace TheOmegaStrain.Benchmarks;
[InProcess]
[CPUUsageDiagnoser]
public class ColorShaderBenchmarks
{
    // Valid full-length hex colors — happy path, cache-miss scenario
    private static readonly string[] ValidColors = ["FF0000", "00FF00", "0000FF", "FFFFFF", "000000", "FF8800", "8800FF", "00FF88", "888888", "123456"];
    // Inputs that trigger the exception storm in the original code:
    // short strings (< 6 chars), strings with '#' prefix, null, empty
    private static readonly string[] MalformedColors = ["#FF0000", "#FFF", "FFF", "F0", "", "#000", "#AABBCC", "AB", "#12", "1"];
    private static readonly float[] Normals = [0f, 0.25f, 0.5f, 0.75f, 1f];
    [Benchmark(Baseline = true)]
    public string ValidColorShading()
    {
        string result = "";
        for (int i = 0; i < ValidColors.Length; i++)
            for (int j = 0; j < Normals.Length; j++)
                result = RenderColorShading.GetShadeOfColorFromNormal(Normals[j], ValidColors[i]);
        return result;
    }

    [Benchmark]
    public string MalformedColorShading()
    {
        string result = "";
        for (int i = 0; i < MalformedColors.Length; i++)
            for (int j = 0; j < Normals.Length; j++)
                result = RenderColorShading.GetShadeOfColorFromNormal(Normals[j], MalformedColors[i]);
        return result;
    }
}
