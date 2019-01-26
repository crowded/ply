using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CS
{
    public class Benchmarks
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Func<int> ArbitraryWork(int work)
        {
            return () => Enumerable.Range(0, work).Sum();
        }

        public static async ValueTask<int> CsTasks(int workFactor, int loopCount)
        {
            await Task.Yield();
            var arb1 = await Task.Run(ArbitraryWork(workFactor));
            var arb2 = await Task.Run(ArbitraryWork(workFactor));
            var arb3 = await Task.Run(ArbitraryWork(workFactor));
            var arb4 = await Task.Run(ArbitraryWork(workFactor));
            var arb5 = await Task.Run(ArbitraryWork(workFactor));
            var arb6 = await Task.Run(ArbitraryWork(workFactor));
            var arb7 = await Task.Run(ArbitraryWork(workFactor));
            var arb8 = await Task.Run(ArbitraryWork(workFactor));
            var arb9 = await Task.Run(ArbitraryWork(workFactor));
            var arbx = await Task.Run(ArbitraryWork(workFactor));
            return arbx;
            // var i = loopCount;
            // while (i > 0)
            // {
            //     var a = await Task.Run(ArbitraryWork(workFactor)).ConfigureAwait(false);
            //     i = i - 1;
            // }

            // Func<ValueTask<int>> x = async () => await new ValueTask<int>(arb);
            // var v = await x();

            // if (v > 0)
            // {
            //     return await new ValueTask<int>(v);
            // }
            // else
            // {
            //     return 0;
            // }
        }
    }
}