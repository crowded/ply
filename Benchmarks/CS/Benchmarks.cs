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
            var arb = await Task.Run(ArbitraryWork(workFactor));
            async Task<int> Func() => await new ValueTask<int>(arb);
            var v = await Func();

            var i = loopCount;
            while (i > 0)
            {
                if (i % 2 == 0)
                    await Task.Run(ArbitraryWork(workFactor)).ConfigureAwait(false);
                i = i - 1;
            }

            if (v > 0)
            {
                return await new ValueTask<int>(v);
            }
            else
            {
                return 0;
            }
        }
    }
}
