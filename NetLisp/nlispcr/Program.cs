using NetLisp;
using NetLisp.Runtime;
using NetLisp.Data;
using NetLisp.Text;

RuntimeContext runtimeContext = new RuntimeContext();
runtimeContext.SyntaxError += syntaxError;
runtimeContext.RuntimeError += runtimeError;

Random r = new Random();

while (true)
{
    Console.Write("=>");
    string expr = Console.ReadLine();
    string sourceName = "[cons" + r.Next(ushort.MaxValue) + "]";
    IEnumerable<LispToken> evalResults = runtimeContext.EvaluateExpressions(expr, sourceName);
    if (evalResults != null)
    {
        foreach (LispToken evalResult in evalResults)
        {
            Console.WriteLine("==>" + evalResult.ToString());
        }
    }
}

void syntaxError(SyntaxError err)
{
    Console.WriteLine(err.ToString());
}
void runtimeError(RuntimeError err)
{
    Console.WriteLine(err.ToString());
}