using System;
using BenchmarkDotNet.Running;

namespace TheOmegaStrain.Benchmarks
{
    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
